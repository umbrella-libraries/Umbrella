using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses SharePoint via Microsoft Graph as the
/// underlying storage mechanism.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaSharePointFileInfo : IUmbrellaFileInfo
{
	#region Private Members
	private readonly GraphServiceClient _graphServiceClient;
	private readonly string _driveId;
	private readonly string _sharePointRelativePath;
	private long _length = -1;
	private DateTimeOffset? _lastModified;
	private string? _contentType;
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the file provider that created this file.
	/// </summary>
	protected IUmbrellaSharePointFileStorageProvider Provider { get; }

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
	public DateTimeOffset? LastModified => IsNew ? null : _lastModified;

	/// <inheritdoc />
	public string? ContentType
	{
		get => _contentType;
		set => _contentType = value;
	}
	#endregion

	#region Constructors
	internal UmbrellaSharePointFileInfo(
		ILogger<UmbrellaSharePointFileInfo> logger,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		string logicalSubPath,
		string sharePointRelativePath,
		IUmbrellaSharePointFileStorageProvider provider,
		UmbrellaFileAccessAuthorizor accessAuthorizor,
		GraphServiceClient graphServiceClient,
		string driveId,
		bool isNew)
	{
		Logger = logger;
		Provider = provider;
		AccessAuthorizor = accessAuthorizor;
		GenericTypeConverter = genericTypeConverter;

		_graphServiceClient = graphServiceClient;
		_driveId = driveId;
		_sharePointRelativePath = sharePointRelativePath;

		SubPath = logicalSubPath;
		Name = Path.GetFileName(logicalSubPath);
		IsNew = isNew;

		_contentType = mimeTypeUtility.GetMimeType(Name);
	}
	#endregion

	#region Internal Methods
	internal Task InitializeAsync(CancellationToken cancellationToken, DriveItem? preLoadedItem = null)
	{
		if (IsNew)
			return Task.CompletedTask;

		if (preLoadedItem is not null)
		{
			PopulateFromDriveItem(preLoadedItem);
			return Task.CompletedTask;
		}

		return FetchPropertiesAsync(cancellationToken);
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

			await _graphServiceClient.Drives[_driveId].Root
				.ItemWithPath(_sharePointRelativePath)
				.DeleteAsync(cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return true;
		}
		catch (ODataError odataError) when (odataError.ResponseStatusCode == 404)
		{
			return false;
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

			if (IsNew)
				return false;

			_ = await _graphServiceClient.Drives[_driveId].Root
				.ItemWithPath(_sharePointRelativePath)
				.GetAsync(config =>
				{
					config.QueryParameters.Select = ["id"];
				}, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return true;
		}
		catch (ODataError odataError) when (odataError.ResponseStatusCode == 404)
		{
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
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			using Stream stream = await ReadAsStreamAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);
			using var ms = new MemoryStream();

			await stream.CopyToAsync(ms, bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize, cancellationToken).ConfigureAwait(false);

			return ms.ToArray();
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

			using Stream spStream = await GetContentStreamAsync(cancellationToken).ConfigureAwait(false);

			await spStream.CopyToAsync(target, bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize, cancellationToken).ConfigureAwait(false);
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

		using var ms = new MemoryStream(bytes);
		await WriteFromStreamAsync(ms, bufferSizeOverride, cancellationToken).ConfigureAwait(false);
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

			// Non-seekable streams (e.g. HTTP response streams used during copy) cannot be rewound.
			if (stream.CanSeek)
				stream.Position = 0;

			// Wrap to prevent the Graph SDK's PutAsync from disposing the caller-owned stream.
			using var wrapper = new NonDisposingStream(stream);

			var result = await _graphServiceClient.Drives[_driveId].Root
				.ItemWithPath(_sharePointRelativePath)
				.Content
				.PutAsync(wrapper, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			IsNew = false;

			if (result is not null)
				PopulateFromDriveItem(result);
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
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			var destinationFile = (UmbrellaSharePointFileInfo)await Provider.CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

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
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaSharePointFileInfo>(destinationFile);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Create, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			using Stream sourceStream = await ReadAsStreamAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			await destinationFile.WriteFromStreamAsync(sourceStream, cancellationToken: cancellationToken).ConfigureAwait(false);

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
		Guard.IsOfType<UmbrellaSharePointFileInfo>(destinationFile);

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

			return await GetContentStreamAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been an error reading the SharePoint file as a stream.", exc);
		}
	}

	/// <inheritdoc />
	public Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Metadata is not supported by the SharePoint file provider.");

	/// <inheritdoc />
	public Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Metadata is not supported by the SharePoint file provider.");

	/// <inheritdoc />
	public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Metadata is not supported by the SharePoint file provider.");

	/// <inheritdoc />
	public Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Metadata is not supported by the SharePoint file provider.");

	/// <inheritdoc />
	public Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Metadata is not supported by the SharePoint file provider.");
	#endregion

	#region Private Methods
	private async Task FetchPropertiesAsync(CancellationToken cancellationToken)
	{
		var item = await _graphServiceClient.Drives[_driveId].Root
			.ItemWithPath(_sharePointRelativePath)
			.GetAsync(config =>
			{
				config.QueryParameters.Select = ["size", "lastModifiedDateTime", "file"];
			}, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		if (item is not null)
			PopulateFromDriveItem(item);
	}

	private void PopulateFromDriveItem(DriveItem item)
	{
		_length = item.Size ?? -1;
		_lastModified = item.LastModifiedDateTime;

		if (!string.IsNullOrWhiteSpace(item.File?.MimeType))
			_contentType = item.File.MimeType;
	}

	private async Task<Stream> GetContentStreamAsync(CancellationToken cancellationToken)
	{
		Stream? stream = await _graphServiceClient.Drives[_driveId].Root
			.ItemWithPath(_sharePointRelativePath)
			.Content
			.GetAsync(cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		return stream ?? throw new UmbrellaFileNotFoundException(SubPath);
	}

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot perform this operation on a newly created file. The file must first be written to.");
	}

	private sealed class NonDisposingStream(Stream inner) : Stream
	{
		public override bool CanRead => inner.CanRead;
		public override bool CanSeek => inner.CanSeek;
		public override bool CanWrite => inner.CanWrite;
		public override long Length => inner.Length;
		public override long Position { get => inner.Position; set => inner.Position = value; }
		public override void Flush() => inner.Flush();
		public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
		public override void SetLength(long value) => inner.SetLength(value);
		public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);
		public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => inner.CopyToAsync(destination, bufferSize, cancellationToken);
#pragma warning disable CA2215 // Dispose methods should call base class dispose
		protected override void Dispose(bool disposing) { /* intentionally leave inner stream open */ }
		public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
#pragma warning restore CA2215 // Dispose methods should call base class dispose
	}
	#endregion
}