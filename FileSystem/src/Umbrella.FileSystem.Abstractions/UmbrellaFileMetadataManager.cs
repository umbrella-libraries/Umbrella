using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>Coordinates a metadata session with file authorization and lifecycle operations.</summary>
public sealed class UmbrellaFileMetadataManager
{
	private readonly IUmbrellaFileMetadataProvider _provider;
	private readonly UmbrellaFileMetadataContext _context;
	private readonly UmbrellaFileAccessAuthorizor _authorize;
	private readonly IUmbrellaFileMetadataSession _session;
	private bool _authorizingMissingFileDeletion;

	/// <summary>Creates a session before the file's first authorization check.</summary>
	public UmbrellaFileMetadataManager(IUmbrellaFileMetadataProvider provider, UmbrellaFileMetadataContext context, UmbrellaFileAccessAuthorizor authorize)
	{
		Guard.IsNotNull(provider);
		Guard.IsNotNull(context);
		Guard.IsNotNull(authorize);
		_provider = provider;
		_context = context;
		_authorize = authorize;
		_session = provider.CreateSession(context);
	}

	/// <summary>Gets a value without recursively authorizing metadata reads.</summary>
	public Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
		=> ExecuteAsync(() => _session.GetMetadataValueAsync(key, fallback, customValueConverter, cancellationToken), cancellationToken, _authorizingMissingFileDeletion);

	/// <summary>Authorizes cleanup of an absent file, allowing authorization to read its surviving metadata.</summary>
	public async Task<bool> AuthorizeMissingFileDeletionAsync(CancellationToken cancellationToken = default)
	{
		_authorizingMissingFileDeletion = true;
		try
		{
			return await _authorize(_context.FileInfo, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_authorizingMissingFileDeletion = false;
		}
	}

	/// <summary>Sets a value and optionally authorizes and persists all changes.</summary>
	public Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> MutateAsync(() => _session.SetMetadataValueAsync(key, value, false, cancellationToken), writeChanges, cancellationToken);

	/// <summary>Removes a value and optionally authorizes and persists all changes.</summary>
	public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> MutateAsync(() => _session.RemoveMetadataValueAsync(key, false, cancellationToken), writeChanges, cancellationToken);

	/// <summary>Clears values and optionally authorizes and persists all changes.</summary>
	public Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
		=> MutateAsync(() => _session.ClearMetadataAsync(false, cancellationToken), writeChanges, cancellationToken);

	/// <summary>Authorizes and persists changes.</summary>
	public Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
		=> MutateAsync(() => Task.CompletedTask, true, cancellationToken);

	/// <summary>Reads persisted values for content operations without committing pending changes.</summary>
	public Task<IReadOnlyDictionary<string, object?>> ReadPersistedMetadataAsync(CancellationToken cancellationToken = default)
		=> ExecuteAsync(() => _session.ReadPersistedMetadataAsync(cancellationToken), cancellationToken);

	/// <summary>Authorizes the destination's original create/update operation before copying content, then replaces its metadata under that authorization.</summary>
	public async Task CopyToAsync(UmbrellaFileMetadataManager destination, Func<CancellationToken, Task> copyContentAsync, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(destination);
		Guard.IsNotNull(copyContentAsync);
		var operation = destination._context.FileInfo.IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update;
		if (!await destination._authorize(destination._context.FileInfo, operation, cancellationToken).ConfigureAwait(false))
			throw new UmbrellaFileAccessDeniedException(destination._context.SubPath);

		IReadOnlyDictionary<string, object?> values = _session.IsSupported
			? await ReadPersistedMetadataAsync(cancellationToken).ConfigureAwait(false)
			: new Dictionary<string, object?>();
		if (!destination._session.IsSupported && values.Count > 0)
			throw new NotSupportedException("The destination cannot store the source file's metadata.");

		await copyContentAsync(cancellationToken).ConfigureAwait(false);
		if (!destination._session.IsSupported)
			return;

		// Content and its metadata are one authorized copy operation. A new destination
		// is no longer IsNew here, but must not acquire an additional Update requirement.
		_ = await destination.ExecuteAsync(async () =>
		{
			await destination._session.ClearMetadataAsync(false, cancellationToken).ConfigureAwait(false);
			foreach (var pair in values)
				await destination._session.SetMetadataValueAsync(pair.Key, pair.Value, false, cancellationToken).ConfigureAwait(false);
			await destination._session.WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			return true;
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>Cleans metadata after deletion has been authorized and content is confirmed absent.</summary>
	public async Task DeleteAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await _provider.DeleteFileAsync(_context, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException)
		{
			throw new UmbrellaFileSystemException($"Metadata cleanup failed for '{_context.SubPath}'.", exc);
		}
	}

	private async Task MutateAsync(Func<Task> action, bool writeChanges, CancellationToken cancellationToken, [CallerMemberName] string operation = "")
	{
		_ = await ExecuteAsync(async () =>
		{
			await action().ConfigureAwait(false);
			if (writeChanges)
			{
				if (!await _authorize(_context.FileInfo, UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
					throw new UmbrellaFileAccessDeniedException(_context.SubPath);
				await _session.WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}

			return true;
		}, cancellationToken, operation: operation).ConfigureAwait(false);
	}

	private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken, bool allowNewFileRead = false, [CallerMemberName] string operation = "")
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_session.IsSupported)
			throw new NotSupportedException("Metadata is not supported by the configured file provider.");
		if (_context.FileInfo.IsNew && !allowNewFileRead)
			throw new InvalidOperationException("Cannot perform this operation on a newly created file. The file must first be written to.");
		try
		{
			return await action().ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException)
		{
			throw new UmbrellaFileSystemException($"Metadata {operation} failed for '{_context.SubPath}'.", exc);
		}
	}
}