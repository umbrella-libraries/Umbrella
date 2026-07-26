namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Extensions for use with <see cref="IUmbrellaFileInfo"/> instances.
/// </summary>
public static class IUmbrellaFileInfoExtensions
{
	/// <summary>
	/// Gets the id of the user that created the file, if the id exists.
	/// </summary>
	/// <typeparam name="TUserId">The type of the user id.</typeparam>
	/// <param name="fileInfo">The file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The user id, if it exists.</returns>
	public static async Task<TUserId> GetCreatedByIdAsync<TUserId>(this IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		return await fileInfo.GetMetadataValueAsync<TUserId>(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Sets the id of the user that created the file.
	/// </summary>
	/// <typeparam name="TUserId">The type of the user id.</typeparam>
	/// <param name="fileInfo">The file.</param>
	/// <param name="value">The user id.</param>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	public static async Task SetCreatedByIdAsync<TUserId>(this IUmbrellaFileInfo fileInfo, TUserId value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		await fileInfo.SetMetadataValueAsync(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Get the file name, if the file name exists.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The file name, if it exists.</returns>
	public static async Task<string> GetFileNameAsync(this IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		return await fileInfo.GetMetadataValueAsync<string>(UmbrellaFileSystemConstants.FileNameMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Sets the file name.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="value">The file name.</param>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	public static async Task SetFileNameAsync(this IUmbrellaFileInfo fileInfo, string value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		await fileInfo.SetMetadataValueAsync(UmbrellaFileSystemConstants.FileNameMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the file upload type.
	/// </summary>
	/// <typeparam name="TFileUpload">The type of the file upload.</typeparam>
	/// <param name="fileInfo">The file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <typeparamref name="TFileUpload"/>.</returns>
	public static async Task<TFileUpload> GetFileUploadTypeAsync<TFileUpload>(this IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
		where TFileUpload : struct, Enum
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		string strFileUploadType = await fileInfo.GetMetadataValueAsync<string>("FileUploadType", cancellationToken: cancellationToken).ConfigureAwait(false);

		if (Enum.TryParse(strFileUploadType, true, out TFileUpload fileUploadType))
			return fileUploadType;

		return default;
	}

	/// <summary>
	/// Sets the file upload type.
	/// </summary>
	/// <typeparam name="TFileUpload">The type of the file upload.</typeparam>
	/// <param name="fileInfo"></param>
	/// <param name="value">The value.</param>
	/// <param name="writeChanges">if set to <see langword="true" />, the changes will be persisted.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> which completes when the operation has been completed.</returns>
	public static async Task SetFileUploadTypeAsync<TFileUpload>(this IUmbrellaFileInfo fileInfo, TFileUpload value, bool writeChanges = true, CancellationToken cancellationToken = default)
		where TFileUpload : struct, Enum
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (fileInfo is null)
			throw new ArgumentNullException(nameof(fileInfo));

		await fileInfo.SetMetadataValueAsync("FileUploadType", value.ToString(), writeChanges, cancellationToken).ConfigureAwait(false);
	}
}
