using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Security.Extensions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Serves as the base class for file handlers.
/// </summary>
/// <typeparam name="TGroupId">The type of the group id.</typeparam>
public abstract class UmbrellaFileHandler<TGroupId> : IUmbrellaFileHandler<TGroupId>
	where TGroupId : IEquatable<TGroupId>
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected IHybridCache Cache { get; }

	/// <summary>
	/// Gets the cache key utility.
	/// </summary>
	protected ICacheKeyUtility CacheKeyUtility { get; }

	/// <summary>
	/// Gets the file provider.
	/// </summary>
	protected IUmbrellaFileStorageProvider FileProvider { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	public IUmbrellaFileStorageProviderOptions Options { get; }

	/// <inheritdoc/>
	public abstract string DirectoryName { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileHandler{TGroupId}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="options">The options.</param>
	protected UmbrellaFileHandler(
		ILogger logger,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaFileStorageProvider fileProvider,
		IUmbrellaFileStorageProviderOptions options)
	{
		Logger = logger;
		Cache = cache;
		CacheKeyUtility = cacheKeyUtility;
		FileProvider = fileProvider;
		Options = options;
	}

	/// <inheritdoc />
	[Obsolete("This method is not recommended for use and will be removed in a future version as it can negatively impact performance.")]
	public async Task<string?> GetMostRecentUrlByGroupIdAsync(TGroupId groupId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string key = CacheKeyUtility.Create(GetType(), groupId + "");

			return await Cache.GetOrCreateAsync(key, async () =>
			{
				IUmbrellaFileInfo? fileInfo = await GetMostRecentExistingFileByGroupIdAsync(groupId, cancellationToken).ConfigureAwait(false);

				return fileInfo is null ? null : GetWebFilePath(fileInfo.Name, groupId);
			},
			() => TimeSpan.FromHours(1),
			cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting the most recent URL.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<string?> GetUrlByGroupIdAndProviderFileNameAsync(TGroupId groupId, string providerFileName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(providerFileName);

		try
		{
			string key = CacheKeyUtility.Create(GetType(), $"{groupId}:{providerFileName}");

			return await Cache.GetOrCreateAsync(key, async () =>
			{
				string filePath = GetFilePath(providerFileName, groupId);

				IUmbrellaFileInfo? fileInfo = await FileProvider.GetAsync(filePath, cancellationToken).ConfigureAwait(false);

				return fileInfo is null ? null : GetWebFilePath(fileInfo.Name, groupId);
			},
			() => TimeSpan.FromHours(1),
			cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, providerFileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting specified file.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<string> CreateByGroupIdAndTempFileNameAsync(TGroupId groupId, string tempFileName, string? newFileName = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(tempFileName);

		try
		{
			string permFileName = !string.IsNullOrEmpty(newFileName) ? newFileName! : tempFileName;

			// Move the file from the temp folder to the live folder
			string permPath = GetFilePath(permFileName, groupId);
			string tempPath = GetTempFilePath(tempFileName);

			IUmbrellaFileInfo? tempFileInfo = await FileProvider.GetAsync(tempPath, cancellationToken).ConfigureAwait(false);

			if (tempFileInfo is null)
			{
				// It might be the case that the temp file was already moved. Check for it at the permanent path.
				bool exists = await FileProvider.ExistsAsync(permPath, cancellationToken).ConfigureAwait(false);

				if (exists)
					return GetWebFilePath(permFileName, groupId);
			}

			IUmbrellaFileInfo fileInfo = await FileProvider.MoveAsync(tempPath, permPath, cancellationToken).ConfigureAwait(false);

			await AfterSavingAsync(fileInfo, groupId, cancellationToken).ConfigureAwait(false);
			await ApplyPermissionsAsync(fileInfo, groupId, true, cancellationToken).ConfigureAwait(false);

			return GetWebFilePath(permFileName, groupId);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, tempFileName, newFileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem creating the item.", exc);
		}
	}

	/// <inheritdoc />
	public async Task DeleteByGroupIdAndProviderFileNameAsync(TGroupId groupId, string providerFileName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(providerFileName);

		try
		{
			string permPath = GetFilePath(providerFileName, groupId);
			_ = await FileProvider.DeleteAsync(permPath, cancellationToken).ConfigureAwait(false);

			string key = CacheKeyUtility.Create(GetType(), $"{groupId}:{providerFileName}");
			await Cache.RemoveAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, providerFileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the item.", exc);
		}
	}

	/// <inheritdoc />
	public async Task DeleteAllByGroupIdAsync(TGroupId groupId, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string directoryName = GetDirectoryName(groupId);
			await FileProvider.DeleteDirectoryAsync(directoryName, cancellationToken).ConfigureAwait(false);

			// Remove from the cache
			string key = CacheKeyUtility.Create(GetType(), groupId + "");
			await Cache.RemoveAsync<string>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId }))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the files for the specified group.", exc);
		}
	}

	/// <inheritdoc />
	public abstract Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public virtual async Task ApplyPermissionsAsync(IUmbrellaFileInfo fileInfo, TGroupId groupId, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		try
		{
			if (ClaimsPrincipal.Current is not null)
				await fileInfo.SetCreatedByIdAsync(ClaimsPrincipal.Current.GetId<string>(), false, cancellationToken).ConfigureAwait(false);

			if (writeChanges)
				await fileInfo.WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.SubPath, groupId, writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been a problem applying the required file permissions.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> SaveAsync(TGroupId groupId, string fileName, byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string subPath = GetFilePath(fileName, groupId);

			IUmbrellaFileInfo fileInfo = await FileProvider.SaveAsync(subPath, bytes, bufferSizeOverride, cancellationToken).ConfigureAwait(false);

			await AfterSavingAsync(fileInfo, groupId, cancellationToken).ConfigureAwait(false);

			return fileInfo;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, fileName, bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem saving the file.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo?> GetAsync(TGroupId groupId, string fileName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string subPath = GetFilePath(fileName, groupId);

			return await FileProvider.GetAsync(subPath, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, fileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem saving the file.", exc);
		}
	}

	/// <summary>
	/// Called after the file has been saved. This method is called after the file write operation has been completed when the following methods are called:
	/// <list type="bullet">
	/// <item><see cref="CreateByGroupIdAndTempFileNameAsync(TGroupId, string, string?, CancellationToken)"/></item>
	/// <item><see cref="SaveAsync(TGroupId, string, byte[], int?, CancellationToken)"/></item>
	/// </list>
	/// This allows for any additional processing to be performed after the file has been saved, e.g. resizing an image.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="groupId">The group identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected virtual Task AfterSavingAsync(IUmbrellaFileInfo fileInfo, TGroupId groupId, CancellationToken cancellationToken = default) => Task.CompletedTask;

	[Obsolete("This method is not recommended for use and will be removed in a future version as it can negatively impact performance.")]
	private async Task<IUmbrellaFileInfo?> GetMostRecentExistingFileByGroupIdAsync(TGroupId groupId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		string directoryName = GetDirectoryName(groupId);
		IReadOnlyCollection<IUmbrellaFileInfo> lstFile = await FileProvider.EnumerateDirectoryAsync(directoryName, cancellationToken).ConfigureAwait(false);

		return lstFile.OrderByDescending(x => x.LastModified).FirstOrDefault();
	}

	/// <inheritdoc/>
	public string GetTempDirectoryName() => $"/{Options.TempFilesDirectoryName}";

	/// <inheritdoc/>
	public string GetTempFilePath(string fileName) => $"{GetTempDirectoryName()}/{fileName}";

	/// <inheritdoc/>
	public string GetTempWebFilePath(string fileName) => $"/{Options.WebFilesDirectoryName}{GetTempFilePath(fileName)}".ToLowerInvariant();

	/// <inheritdoc/>
	public bool IsTempFilePath(string filePath)
	{
		Guard.IsNotNull(filePath);

		return filePath.StartsWith(GetTempDirectoryName() + "/", StringComparison.OrdinalIgnoreCase);
	}

	/// <inheritdoc/>
	public string GetDirectoryName(TGroupId groupId) => $"/{DirectoryName}/{groupId}";

	/// <inheritdoc/>
	public string GetFilePath(string fileName, TGroupId groupId) => $"{GetDirectoryName(groupId)}/{fileName}";

	/// <inheritdoc/>
	public string GetWebFilePath(string fileName, TGroupId groupId) => $"/{Options.WebFilesDirectoryName}{GetFilePath(fileName, groupId)}".ToLowerInvariant();
}