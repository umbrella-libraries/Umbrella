
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Disk;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses the physical disk as the underlying storage mechanism.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaDiskFileInfo : IUmbrellaRangeReadableFileInfo
{
	private readonly UmbrellaFileMetadataManager _metadata;

	internal Task<bool> AuthorizeMissingFileDeletionAsync(CancellationToken cancellationToken)
		=> _metadata.AuthorizeMissingFileDeletionAsync(cancellationToken);

	#region Private Members
	#endregion

	#region Protected Properties		
	/// <summary>
	/// Gets the <see cref="ILogger" />.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the provider used to load this file instance.
	/// </summary>
	protected IUmbrellaDiskFileStorageProvider Provider { get; }

	/// <summary>
	/// Gets the file access authorizor.
	/// </summary>
	protected UmbrellaFileAccessAuthorizor AccessAuthorizor { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }
	#endregion

	#region Internal Properties
	internal FileInfo PhysicalFileInfo { get; }
	#endregion

	#region Public Properties
	/// <inheritdoc />
	public bool IsNew { get; private set; }

	/// <inheritdoc />
	public string Name => PhysicalFileInfo.Name;

	/// <inheritdoc />
	public string SubPath { get; }

	/// <inheritdoc />
	public long Length => PhysicalFileInfo.Exists && !IsNew ? PhysicalFileInfo.Length : -1;

	/// <inheritdoc />
	public DateTimeOffset? LastModified => PhysicalFileInfo.Exists && !IsNew ? PhysicalFileInfo.LastWriteTimeUtc : null;

	/// <inheritdoc />
	public string ContentType { get; }
	#endregion

	#region Constructors
	internal UmbrellaDiskFileInfo(
		ILogger<UmbrellaDiskFileInfo> logger,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		string subpath,
		IUmbrellaDiskFileStorageProvider provider,
	  UmbrellaFileAccessAuthorizor accessAuthorizor,
		FileInfo physicalFileInfo,
		bool isNew,
		IUmbrellaFileMetadataProvider metadataProvider,
		string? metadataNamespace)
	{
		if (subpath.EndsWith(UmbrellaDiskFileStorageConstants.MetadataFileExtension, StringComparison.OrdinalIgnoreCase))
			throw new UmbrellaFileSystemException($"Files with the extension '{UmbrellaDiskFileStorageConstants.MetadataFileExtension}' are not permitted.");

		Logger = logger;
		Provider = provider;
		AccessAuthorizor = accessAuthorizor;
		GenericTypeConverter = genericTypeConverter;
		PhysicalFileInfo = physicalFileInfo;
		IsNew = isNew;
		SubPath = subpath;

		ContentType = mimeTypeUtility.GetMimeType(Name);
		_metadata = new(metadataProvider, new(this, metadataNamespace, SubPath), accessAuthorizor);

	}
	#endregion

	#region IUmbrellaFileInfo Members
	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			var destinationFile = (UmbrellaDiskFileInfo)await Provider.CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There was a problem copying the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaDiskFileInfo>(destinationFile);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Create, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			var target = (UmbrellaDiskFileInfo)destinationFile;

			await _metadata.CopyToAsync(target._metadata, token =>
			{
				token.ThrowIfCancellationRequested();
				Guard.IsNotNull(target.PhysicalFileInfo.Directory);
				if (!target.PhysicalFileInfo.Directory.Exists)
					target.PhysicalFileInfo.Directory.Create();
				File.Copy(PhysicalFileInfo.FullName, target.PhysicalFileInfo.FullName, true);
				target.IsNew = false;
				target.PhysicalFileInfo.Refresh();
				return Task.CompletedTask;
			}, cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There was a problem copying the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo destinationFile = await CopyAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There was a problem moving the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaDiskFileInfo>(destinationFile);

		try
		{
			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There was a problem moving the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			PhysicalFileInfo.Delete();
			await _metadata.DeleteAsync(cancellationToken).ConfigureAwait(false);

			return true;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There was a problem deleting the current file.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return PhysicalFileInfo.Exists;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There was a problem determining if the current file exists.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			byte[] bytes = new byte[PhysicalFileInfo.Length];

			using (var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize, true))
			{
#if NET6_0_OR_GREATER
				_ = await fs.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
				_ = await fs.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
			}

			return bytes;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNull(target, nameof(target));

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			int bufferSize = bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize;

			using var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
			await fs.CopyToAsync(target, bufferSize, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(bytes);
		Guard.HasSizeGreaterThan(bytes, 0);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			using var ms = new MemoryStream(bytes);
			await WriteFromStreamAsync(ms, bufferSizeOverride, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(stream, nameof(stream));

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			Guard.IsNotNull(PhysicalFileInfo.Directory);

			if (!PhysicalFileInfo.Directory.Exists)
				PhysicalFileInfo.Directory.Create();

			int bufferSize = bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize;

			using (var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Write, bufferSize, true))
			{
				stream.Position = 0;
				await stream.CopyToAsync(fs, bufferSize, cancellationToken).ConfigureAwait(false);
			}

			IsNew = false;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task<Stream> ReadRangeAsStreamAsync(long offset, long length, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		UmbrellaFileRangeStream.Validate(Length, offset, length, bufferSizeOverride);

		Stream source = await ReadAsStreamAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);

		try
		{
			_ = source.Seek(offset, SeekOrigin.Begin);
			return new UmbrellaFileRangeStream(source, length);
		}
		catch
		{
#if NETSTANDARD2_0
			source.Dispose();
#else
			await source.DisposeAsync().ConfigureAwait(false);
#endif
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<Stream> ReadAsStreamAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize, true);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
		=> _metadata.GetMetadataValueAsync(key, fallback, customValueConverter, cancellationToken);

	/// <inheritdoc />
	public Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> _metadata.SetMetadataValueAsync(key, value, writeChanges, cancellationToken);

	/// <inheritdoc />
	public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> _metadata.RemoveMetadataValueAsync(key, writeChanges, cancellationToken);

	/// <inheritdoc />
	public Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
		=> _metadata.ClearMetadataAsync(writeChanges, cancellationToken);

	/// <inheritdoc />
	public Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
		=> _metadata.WriteMetadataChangesAsync(cancellationToken);

	/// <inheritdoc />
	public async Task<TUserId> GetCreatedByIdAsync<TUserId>(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await GetMetadataValueAsync<TUserId>(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the id.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetCreatedByIdAsync<TUserId>(TUserId value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await SetMetadataValueAsync(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the id.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<string> GetFileNameAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await GetMetadataValueAsync<string>(UmbrellaFileSystemConstants.FileNameMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the file name.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetFileNameAsync(string value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await SetMetadataValueAsync(UmbrellaFileSystemConstants.FileNameMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the file name.", exc);
		}
	}
	#endregion

	#region Private Methods

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot read the contents of a newly created file. The file must first be written to.");
	}
	#endregion
}