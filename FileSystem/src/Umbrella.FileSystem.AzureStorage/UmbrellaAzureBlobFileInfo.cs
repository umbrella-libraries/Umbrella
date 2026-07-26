
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.AzureStorage;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses Azure Blob Storage as the underlying storage
/// mechanism.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaAzureBlobFileInfo : IUmbrellaFileInfo
{
	#region Private Members
	private long _length = -1;
	private string? _contentType;
	private BlobProperties? _blobProperties;
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the file provider used to create this file.
	/// </summary>
	protected IUmbrellaAzureBlobFileStorageProvider Provider { get; }

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
	internal BlobClient Blob { get; }
	#endregion

	#region Public Properties
	/// <inheritdoc />
	public bool IsNew { get; private set; }

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string SubPath { get; }

	/// <inheritdoc />
	public long Length
	{
		get => IsNew ? _length : _blobProperties?.ContentLength ?? -1;
		private set => _length = value;
	}

	/// <inheritdoc />
	public DateTimeOffset? LastModified => IsNew ? null : _blobProperties?.LastModified;

	/// <inheritdoc />
	public string? ContentType
	{
		get => IsNew ? _contentType : _blobProperties?.ContentType;
		set => _contentType = value;
	}
	#endregion

	#region Constructors
	internal UmbrellaAzureBlobFileInfo(ILogger<UmbrellaAzureBlobFileInfo> logger,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		string subpath,
		IUmbrellaAzureBlobFileStorageProvider provider,
		UmbrellaFileAccessAuthorizor accessAuthorizor,
		BlobClient blob,
		bool isNew)
	{
		Logger = logger;
		Provider = provider;
		AccessAuthorizor = accessAuthorizor;
		GenericTypeConverter = genericTypeConverter;

		Blob = blob;
		IsNew = isNew;
		SubPath = subpath;
		Name = Path.GetFileName(subpath);

		ContentType = mimeTypeUtility.GetMimeType(Name);
	}
	#endregion

	#region Internal Methods
	internal async Task InitializeAsync(CancellationToken cancellationToken)
	{
		// We can only get the properties for a Blob if it exists. The method call will throw
		// an exception otherwise.
		if (IsNew)
		{
			_blobProperties = new BlobProperties();
			return;
		}

		try
		{
			var result = await Blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			if (result is not null)
				_blobProperties = result.Value;
		}
		catch (RequestFailedException exc) when (Logger.WriteWarning(exc))
		{
			throw;
		}
	}
	#endregion

	#region IUmbrellaFileInfo Members
	/// <inheritdoc />
	public virtual async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return await Blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the file.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return await Blob.ExistsAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (RequestFailedException exc) when (exc.ErrorCode == BlobErrorCode.ContainerBeingDeleted)
		{
			// The container is in the process of being deleted which is fine and means the Blob is on its way to Blob heaven.
			return false;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem determining if the file exists.", exc);
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
			// Encapsulates the Read access authorizor check and the existence check.
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			using Stream stream = await ReadAsStreamAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);
			using var ms = new MemoryStream();

			await stream.CopyToAsync(ms, bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize, cancellationToken).ConfigureAwait(false);

			byte[] bytes = ms.ToArray();

			return bytes;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
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

			_ = await Blob.DownloadToAsync(target, transferOptions: CreateStorageTransferOptions(bufferSizeOverride), cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
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
			using var ms = new MemoryStream(bytes);
			await WriteFromStreamAsync(ms, bufferSizeOverride, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified bytes.", exc);
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
			if (!await AccessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			stream.Position = 0;

			_ = await Blob.UploadAsync(
				stream,
				new BlobHttpHeaders { ContentType = ContentType },
				_blobProperties?.Metadata,
				transferOptions: CreateStorageTransferOptions(bufferSizeOverride),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			IsNew = false;

			// Trigger a call to this to ensure property population
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified stream.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			// Encapsulates the Read access authorizor check and the existence check.
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			var destinationFile = (UmbrellaAzureBlobFileInfo)await Provider.CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

			// Encapsulates the Create access authorizor check.
			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem copying the file to the specified destination path.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		// Copying between file providers of different types should not be permitted. Might be possible in future.
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaAzureBlobFileInfo>(destinationFile);

		try
		{
			// Encapsulates the Read access authorizor check and the existence check.
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Create, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			var blobDestinationFile = (UmbrellaAzureBlobFileInfo)destinationFile;

			_ = await blobDestinationFile.Blob.StartCopyFromUriAsync(Blob.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);

			// In order to ensure we know the size of the destination file we can set it here
			// and then use the real value from the Blob once it becomes available after the copy operation
			// has completed.
			blobDestinationFile.Length = Length;
			blobDestinationFile.IsNew = false;

			await blobDestinationFile.InitializeAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There has been a problem copying the file to the specified destination file.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo destinationFile = await CopyAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem moving the file to the specified destination path.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsOfType<UmbrellaAzureBlobFileInfo>(destinationFile);

		try
		{
			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There has been a problem moving the specified file to the specified destination file.", exc);
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

			// The buffer size can't be controlled here. For chunking we should use the WriteToStreamAsync method.
			BlobDownloadInfo response = await Blob.DownloadAsync(cancellationToken).ConfigureAwait(false);

			return response.Content;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been an error reading the Blob as a Stream.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_blobProperties is not null && _blobProperties.Metadata.TryGetValue(key, out string? rawValue))
				return await Task.FromResult(GenericTypeConverter.Convert(rawValue, fallback, customValueConverter)!).ConfigureAwait(false);

			return default!;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, fallback, customValueConverter }))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_blobProperties is not null)
			{
				if (value is null)
				{
					_ = _blobProperties.Metadata.Remove(key);
				}
				else
				{
					_blobProperties.Metadata[key] = value.ToString();
				}

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, value, writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_blobProperties is not null)
			{
				_ = _blobProperties.Metadata.Remove(key);

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error removing the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		try
		{
			if (_blobProperties is not null)
			{
				_blobProperties.Metadata.Clear();

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error clearing the metadata.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (_blobProperties is not null)
				_ = await Blob.SetMetadataAsync(_blobProperties.Metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error writing the metadata changes.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<TUserId> GetCreatedByIdAsync<TUserId>(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await GetMetadataValueAsync<TUserId>(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
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
		catch (Exception exc) when (Logger.WriteError(exc))
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
		catch (Exception exc) when (Logger.WriteError(exc))
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
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the file name.", exc);
		}
	}
	#endregion

	#region Private Methods
	private static StorageTransferOptions CreateStorageTransferOptions(int? bufferSizeOverride = null)
		=> new()
		{
			MaximumTransferLength = bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize
		};

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot perform this operation on a newly created file. The file must first be written to.");
	}
	#endregion
}
