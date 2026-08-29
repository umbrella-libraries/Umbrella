using Microsoft.Extensions.Logging;
#if NET9_0_OR_GREATER
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;

namespace Umbrella.DynamicImage.Abstractions.Caching;

/// <summary>
/// A file handler for accessing files stored in the Dynamic Image cache directory.
/// </summary>
/// <seealso cref="UmbrellaFileHandler{Int32}" />
/// <seealso cref="IUmbrellaDynamicImageCacheFileHandler" />
public class UmbrellaDynamicImageCacheFileHandler : UmbrellaFileHandler<int>, IUmbrellaDynamicImageCacheFileHandler
{
	private readonly DynamicImageCacheCoreOptions _cacheCoreOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDynamicImageCacheFileHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="options">The options.</param>
	/// <param name="cacheCoreOptions">The cache core options.</param>
	public UmbrellaDynamicImageCacheFileHandler(
		ILogger<UmbrellaDynamicImageCacheFileHandler> logger,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaFileStorageProvider fileProvider,
		IUmbrellaFileStorageProviderOptions options,
		DynamicImageCacheCoreOptions cacheCoreOptions)
		: base(logger, cache, cacheKeyUtility, fileProvider, options)
	{
		_cacheCoreOptions = cacheCoreOptions;
	}

	/// <inheritdoc/>
	public override string DirectoryName => _cacheCoreOptions.DirectoryName;
}
