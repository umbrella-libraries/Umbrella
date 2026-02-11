
namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A file provider abstraction that wraps an underlying storage mechanism, e.g. Disk storage, Azure Storage.
/// </summary>
public interface IUmbrellaFileStorageProvider
{
	/// <summary>
	/// Initializes the options of the provider.
	/// </summary>
	/// <param name="options">The options.</param>
	void InitializeOptions(UmbrellaFileStorageProviderOptionsBase options);

	/// <summary>
	/// Creates a new file at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The created file.</returns>
	Task<IUmbrellaFileInfo> CreateAsync(string subpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the file at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file.</returns>
	Task<IUmbrellaFileInfo?> GetAsync(string subpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the file at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true" /> if the file has been deleted; otherwise <see langword="false" />.</returns>
	Task<bool> DeleteAsync(string subpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the specified file.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true" /> if the file has been deleted; otherwise <see langword="false" />.</returns>
	Task<bool> DeleteAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default);

	/// <summary>
	/// Copies the file located at the <paramref name="sourceSubpath"/> to the <paramref name="destinationSubpath"/>.
	/// </summary>
	/// <param name="sourceSubpath">The source subpath.</param>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The copied file.</returns>
	Task<IUmbrellaFileInfo> CopyAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Copies the <paramref name="sourceFile"/> to the <paramref name="destinationSubpath"/>.
	/// </summary>
	/// <param name="sourceFile">The source file.</param>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The copied file.</returns>
	Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Copies the <paramref name="sourceFile"/> to the <paramref name="destinationFile"/>.
	/// </summary>
	/// <param name="sourceFile">The source file.</param>
	/// <param name="destinationFile">The destination file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The copied file.</returns>
	Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves the file content from the byte array to the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The saved file.</returns>
	Task<IUmbrellaFileInfo> SaveAsync(string subpath, byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves the file content from the Stream to the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="stream">The stream.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The saved file.</returns>
	Task<IUmbrellaFileInfo> SaveAsync(string subpath, Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Check if the file at the specified <paramref name="subpath"/> exists.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true" /> if the file exists; otherwise <see langword="false" />.</returns>
	Task<bool> ExistsAsync(string subpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the directory at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	Task DeleteDirectoryAsync(string subpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Moves the file located at the specified <paramref name="sourceSubpath"/> to the <paramref name="destinationSubpath"/>.
	/// </summary>
	/// <param name="sourceSubpath">The source subpath.</param>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The moved file.</returns>
	Task<IUmbrellaFileInfo> MoveAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Moves the <paramref name="sourceFile"/> to the <paramref name="destinationSubpath"/>.
	/// </summary>
	/// <param name="sourceFile">The source file.</param>
	/// <param name="destinationSubpath">The destination subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The moved file.</returns>
	Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default);

	/// <summary>
	/// Moves the <paramref name="sourceFile"/> to the <paramref name="destinationFile"/>.
	/// </summary>
	/// <param name="sourceFile">The source file.</param>
	/// <param name="destinationFile">The destination file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The moved file.</returns>
	Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enumerates all files in the specified directory.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A collection of the files in the directory.</returns>
	Task<IReadOnlyCollection<IUmbrellaFileInfo>> EnumerateDirectoryAsync(string subpath, CancellationToken cancellationToken = default);
}