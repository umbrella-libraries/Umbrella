
using System.Security.Claims;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A handler used to access files stored using an implementation of the <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/>.
/// </summary>
/// <typeparam name="TGroupId">The type of the group identifier.</typeparam>
public interface IUmbrellaFileHandler<TGroupId> : IUmbrellaFileAuthorizationHandler
	where TGroupId : IEquatable<TGroupId>
{
	/// <summary>
	/// Applies permissions to the specified <paramref name="fileInfo"/> in the form of metadata.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="groupId">The optional group identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="writeChanges">if set to <see langword="true" /> writes changes to the file metadata.</param>
	/// <returns>A task that completes when the operation has completed.</returns>
	/// <remarks>
	/// File permissions are applied as file metadata. Checks to determine if the file can be accessed in a specified context
	/// e.g. by the current <see cref="ClaimsPrincipal"/> are <c>not</c> done automatically.
	/// It is the responsibility of consuming code to call the <see cref="IUmbrellaFileAuthorizationHandler.CanAccessAsync(IUmbrellaFileInfo, CancellationToken)"/> before allowing
	/// access to a file.
	/// </remarks>
	Task ApplyPermissionsAsync(IUmbrellaFileInfo fileInfo, TGroupId groupId, bool writeChanges = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new file by moving the specified temporary file from the temporary files directory to the top-level folder
	/// that this handler accesses inside a sub-folder specified by the <paramref name="groupId"/>.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="tempFileName">Name of the temporary file to be moved.</param>
	/// <param name="newFileName">An optional new name for the file when it is moved. If not provided, the <paramref name="tempFileName"/> will be used.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The web-relative path the file can be accessed from a public URL.</returns>
	/// <remarks>
	/// Internally, handler implementations should do the following:
	/// <list type="bullet">
	/// <item>Check if the file already exists inside the target folder for this handler. If it does, just return the existing web relative path.</item>
	/// <item>Apply permissions to the moved file using the <see cref="ApplyPermissionsAsync(IUmbrellaFileInfo, TGroupId, bool, CancellationToken)"/> method.</item>
	/// <item>Return the web relative path to the moved file.</item>
	/// </list>
	/// </remarks>
	Task<string> CreateByGroupIdAndTempFileNameAsync(TGroupId groupId, string tempFileName, string? newFileName = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all files from the top-level folder for this handler contained within the sub-folder
	/// specified by the <paramref name="groupId"/>.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task which completes when the files have been deleted.</returns>
	Task DeleteAllByGroupIdAsync(TGroupId groupId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes a single file specified by the <paramref name="providerFileName"/> contained within the sub-folder specified
	/// by the <paramref name="groupId"/> inside the top-level folder for this handler.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="providerFileName">Name of the provider file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task which completes when the files have been deleted.</returns>
	Task DeleteByGroupIdAndProviderFileNameAsync(TGroupId groupId, string providerFileName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the URL to the most recently created file inside the sub-folder specified by the <paramref name="groupId"/> within the top-level folder
	/// associated with this handler.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The web relative path to the file if one exists; otherwise <see langword="null"/>.</returns>
	[Obsolete("This method is not recommended for use and will be removed in a future version as it can negatively impact performance.")]
	Task<string?> GetMostRecentUrlByGroupIdAsync(TGroupId groupId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the URL to the file with the specified <paramref name="providerFileName"/> contained with the sub-folder specified by <paramref name="groupId"/>
	/// within the top-level folder associated with this handler.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="providerFileName">Name of the provider file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The web relative path to the file if one exists; otherwise <see langword="null"/>.</returns>
	Task<string?> GetUrlByGroupIdAndProviderFileNameAsync(TGroupId groupId, string providerFileName, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the name of the directory based on the current directory including the optional <paramref name="groupId"/>.
	/// </summary>
	/// <param name="groupId">The optional group identifier.</param>
	/// <returns>The directory name, e.g. /documents or /documents/10 where the <paramref name="groupId"/> is 10.</returns>
	string GetDirectoryName(TGroupId groupId);

	/// <summary>
	/// Gets the file path based on the current directory, <paramref name="fileName"/> and optional <paramref name="groupId"/>.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="groupId">The optional group identifier.</param>
	/// <returns>The file path, e.g. /documents/file.png or /documents/10/file.png where the <paramref name="groupId"/> is 10.</returns>
	string GetFilePath(string fileName, TGroupId groupId);

	/// <summary>
	/// Gets the file path as it can be accessed using a URL based on the current directory, <paramref name="fileName"/> and optional <paramref name="groupId"/>.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="groupId">The optional group identifier.</param>
	/// <returns>The file path, e.g. /files/documents/file.png or /files/documents/10/file.png where the <paramref name="groupId"/> is 10.</returns>
	string GetWebFilePath(string fileName, TGroupId groupId);

	/// <summary>
	/// Determines whether the specified <paramref name="filePath"/> is stored in the temporary files directory.
	/// </summary>
	/// <param name="filePath">Path of the file.</param>
	/// <returns><see langword="true"/> if the file can be accessed; otherwise <see langword="false"/></returns>
	bool IsTempFilePath(string filePath);

	/// <summary>
	/// Gets the name of the temporary directory.
	/// </summary>
	/// <returns>The directory name, e.g. /temp-files</returns>
	string GetTempDirectoryName();

	/// <summary>
	/// Gets the path of the specified <paramref name="fileName"/> combined with the temporary files directory.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <returns>The file path, e.g. /temp-files/file.png</returns>
	string GetTempFilePath(string fileName);

	/// <summary>
	/// Gets the path of the specified <paramref name="fileName"/> stored in the temporary files directory to be accessed via a URL.
	/// </summary>
	/// <param name="fileName">Name of the file.</param>
	/// <returns>The file path, e.g. /files/temp-files/file.png</returns>
	string GetTempWebFilePath(string fileName);

	/// <summary>
	/// Saves the file content from the byte array to the underlying storage provider.
	/// The path is determined from an internal call to <see cref="GetFilePath(string, TGroupId)"/>.
	/// </summary>
	/// <param name="groupId">The group id.</param>
	/// <param name="fileName">The file name.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="bufferSizeOverride">The buffer size override.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The saved file.</returns>
	Task<IUmbrellaFileInfo> SaveAsync(TGroupId groupId, string fileName, byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the file from the underlying storage provider.
	/// </summary>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="fileName">Name of the file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file, if it exists; otherwise <see langword="null"/>.</returns>
	Task<IUmbrellaFileInfo?> GetAsync(TGroupId groupId, string fileName, CancellationToken cancellationToken = default);
}