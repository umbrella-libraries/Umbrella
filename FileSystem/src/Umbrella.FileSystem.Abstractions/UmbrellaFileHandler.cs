using System.Security.Claims;
using CommunityToolkit.Diagnostics;
#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
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
	protected PlatformCache Cache { get; }

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
		PlatformCache cache,
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

			async Task<string?> CreateAsync(CancellationToken token)
			{
				IUmbrellaFileInfo? fileInfo = await GetMostRecentExistingFileByGroupIdAsync(groupId, token).ConfigureAwait(false);

				return fileInfo is null ? null : GetWebFilePath(fileInfo.Name, groupId);
			}

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(key, async token => await CreateAsync(token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string? item, _) = await Cache.GetOrCreateAsync(
				key,
				async () => await CreateAsync(cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
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

			async Task<string?> CreateAsync(CancellationToken token)
			{
				string filePath = GetFilePath(providerFileName, groupId);

				IUmbrellaFileInfo? fileInfo = await FileProvider.GetAsync(filePath, token).ConfigureAwait(false);

				return fileInfo is null ? null : GetWebFilePath(fileInfo.Name, groupId);
			}

#if NET9_0_OR_GREATER
			return await Cache.GetOrCreateAsync(key, async token => await CreateAsync(token).ConfigureAwait(false), CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(string? item, _) = await Cache.GetOrCreateAsync(
				key,
				async () => await CreateAsync(cancellationToken).ConfigureAwait(false),
				CreateDistributedCacheEntryOptions,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, providerFileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting specified file.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<UmbrellaVersionedUrl?> GetVersionedWebFilePathAsync(TGroupId groupId, string providerFileName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(providerFileName);

		try
		{
			string filePath = GetFilePath(providerFileName, groupId);
			IUmbrellaFileInfo? fileInfo = await FileProvider.GetAsync(filePath, cancellationToken).ConfigureAwait(false);

			return fileInfo is null ? null : await CreateVersionedUrlAsync(fileInfo, groupId, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId, providerFileName }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting the URL and version token for the specified file.", exc);
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
			await Cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
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
			await Cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { groupId }))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the files for the specified group.", exc);
		}
	}

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

	/// <inheritdoc />
	public async Task<string?> GetVersionTokenAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			IUmbrellaFileInfo? fileInfo = await FileProvider.GetAsync(subpath, cancellationToken).ConfigureAwait(false);

			return fileInfo is null ? null : await GetVersionTokenAsync(fileInfo, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting the version token for the specified file.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<string?> GetVersionTokenAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		try
		{
			return await UmbrellaFileVersionTokenUtility.CreateAsync(fileInfo, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.SubPath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem getting the version token for the specified file.", exc);
		}
	}

	private async Task<UmbrellaVersionedUrl> CreateVersionedUrlAsync(IUmbrellaFileInfo fileInfo, TGroupId groupId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		string url = GetWebFilePath(fileInfo.Name, groupId);
		string? versionToken = await GetVersionTokenAsync(fileInfo, cancellationToken).ConfigureAwait(false);

		return new UmbrellaVersionedUrl(url, versionToken);
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

#if NET9_0_OR_GREATER
	private static HybridCacheEntryOptions CreateHybridCacheEntryOptions()
		=> new()
		{
			Expiration = TimeSpan.FromHours(1),
			LocalCacheExpiration = TimeSpan.FromHours(1)
		};
#else
	private static DistributedCacheEntryOptions CreateDistributedCacheEntryOptions()
		=> new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));
#endif

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
	public virtual string GetDirectoryName(TGroupId groupId) => $"/{DirectoryName}/{groupId}";

	/// <inheritdoc/>
	public string GetFilePath(string fileName, TGroupId groupId) => $"{GetDirectoryName(groupId)}/{fileName}";

	/// <inheritdoc/>
	public string GetWebFilePath(string fileName, TGroupId groupId) => $"/{Options.WebFilesDirectoryName}{GetFilePath(fileName, groupId)}".ToLowerInvariant();

}

/// <summary>
/// Serves as the base class for file handlers that do not require a group identifier.
/// Files are stored directly under the handler's top-level directory without a group sub-folder.
/// </summary>
public abstract class UmbrellaFileHandler : UmbrellaFileHandler<NoGroupId>
{
	/// <inheritdoc />
	protected UmbrellaFileHandler(
		ILogger logger,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaFileStorageProvider fileProvider,
		IUmbrellaFileStorageProviderOptions options)
		: base(logger, cache, cacheKeyUtility, fileProvider, options)
	{
	}

	/// <inheritdoc />
	public override string GetDirectoryName(NoGroupId groupId) => $"/{DirectoryName}";
}
