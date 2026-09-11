using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses a Microsoft Dataverse table column
/// as the underlying storage mechanism, encoding file content as a base64 string.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaDataverseFileInfo : IUmbrellaRangeReadableFileInfo
{
	private readonly UmbrellaFileMetadataManager _metadata;

	internal Task<bool> AuthorizeMissingFileDeletionAsync(CancellationToken cancellationToken)
		=> _metadata.AuthorizeMissingFileDeletionAsync(cancellationToken);

	#region Private Members
	private readonly Guid _recordId;
	private readonly UmbrellaDataverseFileStorageProviderOptions _options;
	private long _length = -1;
	private string? _cachedBase64;
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the file access authorizor.
	/// </summary>
	protected UmbrellaFileAccessAuthorizor AccessAuthorizor { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }
	#endregion

	#region Public Properties
	/// <inheritdoc />
	public bool IsNew { get; private set; }

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string SubPath { get; }

	/// <inheritdoc />
	public long Length => _length;

	/// <inheritdoc />
	public DateTimeOffset? LastModified { get; private set; }

	/// <inheritdoc />
	public string? ContentType { get; set; }
	#endregion

	#region Constructors
	internal UmbrellaDataverseFileInfo(
		ILogger<UmbrellaDataverseFileInfo> logger,
		IGenericTypeConverter genericTypeConverter,
		string subPath,
		string name,
		UmbrellaDataverseFileStorageProviderOptions options,
		UmbrellaFileAccessAuthorizor accessAuthorizor,
		Guid recordId,
		bool isNew,
		IUmbrellaFileMetadataProvider metadataProvider,
		string? metadataNamespace)
	{
		Logger = logger;
		GenericTypeConverter = genericTypeConverter;
		SubPath = subPath;
		Name = name;
		_options = options;
		AccessAuthorizor = accessAuthorizor;
		_recordId = recordId;
		IsNew = isNew;
		_metadata = new(metadataProvider, new(this, metadataNamespace, SubPath), accessAuthorizor);
	}
	#endregion

	internal Guid RecordId => _recordId;
	internal UmbrellaDataverseFileStorageProviderOptions MetadataOptions => _options;

	#region Internal Methods
	internal void Initialize(string? base64Content, DateTimeOffset? lastModified, string? contentType, long? length = null)
	{
		_cachedBase64 = base64Content;
		LastModified = lastModified;
		ContentType = contentType;

		if (_cachedBase64 is not null)
			_length = ComputeByteLength(_cachedBase64);
		else if (length.HasValue)
			_length = length.Value;
	}
	#endregion

	#region IUmbrellaFileInfo Members
	/// <inheritdoc />
	public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (IsNew)
				return false;

			if (_cachedBase64 is not null)
				return true;

			Entity entity = await _options.DataverseClient.RetrieveAsync(
				_options.TableName,
				_recordId,
				new ColumnSet(_options.DataColumnName),
				cancellationToken).ConfigureAwait(false);

			string? base64 = entity.GetAttributeValue<string>(_options.DataColumnName);

			if (base64 is null)
				return false;

			_cachedBase64 = base64;
			_length = ComputeByteLength(base64);

			return true;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem determining if the file exists.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (_options.DeleteRecordOnFileDelete)
			{
				await _options.DataverseClient.DeleteAsync(_options.TableName, _recordId, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				var entity = new Entity(_options.TableName, _recordId);
				entity[_options.DataColumnName] = null;
				entity[_options.FileNameColumnName] = null;
				await _options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
			}

			await _metadata.DeleteAsync(cancellationToken).ConfigureAwait(false);
			_cachedBase64 = null;
			_length = -1;

			return true;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the file.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			string base64 = await GetBase64Async(cancellationToken).ConfigureAwait(false);

			return Convert.FromBase64String(base64);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem reading the file to a byte array.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNull(target);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			byte[] bytes = await ReadAsByteArrayAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);
			await target.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing the file to the specified stream.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(bytes);
		Guard.HasSizeGreaterThan(bytes, 0);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			string base64 = Convert.ToBase64String(bytes);

			var entity = new Entity(_options.TableName, _recordId);
			entity[_options.DataColumnName] = base64;
			entity[_options.FileNameColumnName] = Name;

			if (!string.IsNullOrWhiteSpace(_options.MimeTypeColumnName))
				entity[_options.MimeTypeColumnName] = ContentType;

			var request = new UpsertRequest { Target = entity };
			_ = await _options.DataverseClient.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

			_cachedBase64 = base64;
			_length = bytes.LongLength;
			LastModified = DateTimeOffset.UtcNow;
			IsNew = false;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified byte array.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(stream);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			using var ms = new MemoryStream();

			if (stream.CanSeek)
				stream.Position = 0;

			await stream.CopyToAsync(ms, bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize, cancellationToken).ConfigureAwait(false);

			await WriteFromByteArrayAsync(ms.ToArray(), bufferSizeOverride, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified stream.", exc);
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
			await source.DisposeAsync().ConfigureAwait(false);
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

			byte[] bytes = await ReadAsByteArrayAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);

			return new MemoryStream(bytes);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem reading the file as a stream.", exc);
		}
	}

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Copy is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Copy is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Move is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Move is not supported by the Dataverse file provider.");

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

	#endregion

	#region Private Methods
	private async Task<string> GetBase64Async(CancellationToken cancellationToken)
	{
		if (_cachedBase64 is not null)
			return _cachedBase64;

		if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
			throw new UmbrellaFileAccessDeniedException(SubPath);

		Entity entity = await _options.DataverseClient.RetrieveAsync(
			_options.TableName,
			_recordId,
			new ColumnSet(_options.DataColumnName),
			cancellationToken).ConfigureAwait(false);

		string? base64 = entity.GetAttributeValue<string>(_options.DataColumnName);

		if (string.IsNullOrEmpty(base64))
			throw new UmbrellaFileNotFoundException(SubPath);

		_cachedBase64 = base64;
		_length = ComputeByteLength(base64);

		return base64;
	}

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot perform this operation on a newly created file. The file must first be written to.");
	}

	private static long ComputeByteLength(string base64)
	{
		int padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2
			: base64.EndsWith("=", StringComparison.Ordinal) ? 1
			: 0;

		return (base64.Length * 3L / 4L) - padding;
	}

	#endregion
}