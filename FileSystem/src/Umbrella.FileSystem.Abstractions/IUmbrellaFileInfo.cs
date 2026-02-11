
namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A file used in conjunction with the Umbrella File System which provides an abstraction of
/// an underlying file, e.g. a file stored on disk, a blob stored in Azure Storage, etc.
/// </summary>
public interface IUmbrellaFileInfo
{
	/// <summary>
	/// Gets a value indicating whether this file is new.
	/// </summary>
	bool IsNew { get; }

	/// <summary>
	/// Gets the name.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets the sub path relative to the file provider root, e.g. /images/my-house.png
	/// </summary>
	string SubPath { get; }

	/// <summary>
	/// Gets the length.
	/// </summary>
	long Length { get; }

	/// <summary>
	/// Gets the last modified date.
	/// </summary>
	DateTimeOffset? LastModified { get; }

	/// <summary>
	/// Gets the MIME type of the file content.
	/// </summary>
	string? ContentType { get; }

	/// <summary>
	/// Checks if the current file exists using the file provider from which it was loaded.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which will return <see langword="true" /> if it exists; otherwise <see langword="false"/>.</returns>
	Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the file content as a byte array.
	/// </summary>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file content as byte array.</returns>
	Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes the content of the file to the specified target stream.
	/// </summary>
	/// <param name="target">The target stream to write the file's content to.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes to the file using the specified byte array.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Writes to the file by reading from the specified stream.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the file content as a <see cref="Stream"/>.
	/// </summary>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The stream used to read the file content.</returns>
	Task<Stream> ReadAsStreamAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the file.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true" /> if the file was deleted successfully; otherwise <see langword="false" />.</returns>
	Task<bool> DeleteAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Copies the file to the specified path relative to the provider root.
	/// </summary>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file.</returns>
	Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Copies the file to the specified destination file.
	/// </summary>
	/// <param name="destinationFile">The destination file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file.</returns>
	Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Moves the file to the specified path relative to the provider root.
	/// </summary>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file.</returns>
	Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Moves the file to the specified destination file.
	/// </summary>
	/// <param name="destinationFile">The destination file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file.</returns>
	Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the metadata value with the specified <paramref name="key"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value being retrieved.</typeparam>
	/// <param name="key">The key.</param>
	/// <param name="fallback">The fallback value where the metadata <paramref name="key"/> does not exist.</param>
	/// <param name="customValueConverter">The custom value converter to transform the value before returning it.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The metadata value for the specified <paramref name="key"/>.</returns>
	Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Sets the metadata value with the specified <paramref name="key"/>.
	/// </summary>
	/// <typeparam name="T">The type of the value being set.</typeparam>
	/// <param name="key">The key.</param>
	/// <param name="value">The value.</param>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Removes the metadata value with the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all metadata for the file and optionally persists the changes to the underlying data store.
	/// </summary>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Persists the changes made to the file's metadata.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the data has been persisted.</returns>
	Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default);
}