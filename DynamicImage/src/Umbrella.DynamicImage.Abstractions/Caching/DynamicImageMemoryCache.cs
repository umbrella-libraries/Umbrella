using Microsoft.Extensions.Logging;
#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
using Umbrella.Utilities.Caching.Abstractions;

namespace Umbrella.DynamicImage.Abstractions.Caching;

/// <summary>
/// A Dynamic Image cache implementation that is backed by In-Memory storage.
/// </summary>
/// <seealso cref="DynamicImageCache" />
/// <seealso cref="IDynamicImageCache" />
public class DynamicImageMemoryCache : DynamicImageCache, IDynamicImageCache
{
	#region Private Members
	private readonly PlatformCache _cache;
	private readonly DynamicImageMemoryCacheOptions _memoryCacheOptions;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageMemoryCache"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="cacheOptions">The cache options.</param>
	/// <param name="memoryCacheOptions">The memory cache options.</param>
	public DynamicImageMemoryCache(
		ILogger<DynamicImageMemoryCache> logger,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		DynamicImageCacheCoreOptions cacheOptions,
		DynamicImageMemoryCacheOptions memoryCacheOptions)
		: base(logger, cacheKeyUtility, cacheOptions)
	{
		_cache = cache;
		_memoryCacheOptions = memoryCacheOptions;
	}
	#endregion

	#region IDynamicImageCache Members
	/// <inheritdoc />
	public async Task AddAsync(DynamicImageItem dynamicImage, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		CommunityToolkit.Diagnostics.Guard.IsNotNull(dynamicImage);

		try
		{
			if (!_memoryCacheOptions.CacheEnabled)
				return;

			string rawKey = GenerateCacheKey(dynamicImage.ImageOptions);
			string cacheKey = GenerateMemoryCacheKey(rawKey);
			ReadOnlyMemory<byte> content = await dynamicImage.GetContentAsync(cancellationToken).ConfigureAwait(false);
			var entry = new DynamicImageMemoryCacheEntry(
				dynamicImage.LastModified,
				DynamicImageOptionsCacheEntry.From(dynamicImage.ImageOptions),
				content.ToArray());

#if NET9_0_OR_GREATER
			await _cache.SetAsync(cacheKey, entry, CreateHybridCacheEntryOptions(), cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			await _cache.SetAsync(cacheKey, entry, CreateDistributedCacheEntryOptions(), cancellationToken).ConfigureAwait(false);
#endif
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { dynamicImage.ImageOptions }))
		{
			throw new UmbrellaDynamicImageException($"There was a problem adding the {nameof(DynamicImageItem)} to the cache.", exc, dynamicImage.ImageOptions);
		}
	}

	/// <inheritdoc />
	public async Task<DynamicImageItem?> GetAsync(DynamicImageOptions options, DateTimeOffset sourceLastModified, string fileExtension, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!_memoryCacheOptions.CacheEnabled)
				return null;

			string fileKey = GenerateCacheKey(options);
			string cacheKey = GenerateMemoryCacheKey(fileKey);

#if NET9_0_OR_GREATER
			DynamicImageMemoryCacheEntry? cacheEntry = await _cache.GetOrCreateAsync<DynamicImageMemoryCacheEntry?>(
				cacheKey,
				static _ => ValueTask.FromResult<DynamicImageMemoryCacheEntry?>(null),
				CreateHybridCacheEntryOptions(),
				cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(_, DynamicImageMemoryCacheEntry? cacheEntry, _) = await _cache.TryGetValueAsync<DynamicImageMemoryCacheEntry>(cacheKey, cancellationToken).ConfigureAwait(false);
#endif

			if (cacheEntry is not null)
			{
				//If the file does not exist or has been modified since the IDynamicImage was generated,
				//evict it from the cache
				if (sourceLastModified > cacheEntry.LastModified)
				{
					await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

					return null;
				}
			}

			return cacheEntry is null
				? null
				: new DynamicImageItem
				{
					LastModified = cacheEntry.LastModified,
					ImageOptions = cacheEntry.ImageOptions.ToOptions(),
					Content = cacheEntry.Content
				};
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options, sourceLastModified, fileExtension }))
		{
			throw new UmbrellaDynamicImageException("There was problem retrieving the image from the cache.", exc);
		}
	}

	/// <inheritdoc />
	public async Task RemoveAsync(DynamicImageOptions options, string fileExtension, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			string key = GenerateCacheKey(options);
			string cacheKey = GenerateMemoryCacheKey(key);

			await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options, fileExtension }))
		{
			throw new UmbrellaDynamicImageException("There was a problem removing the image from the cache.", exc);
		}
	}
	#endregion

	#region Private Methods
	private string GenerateMemoryCacheKey(string key) => CacheKeyUtility.Create<DynamicImageMemoryCache>(key);

#if NET9_0_OR_GREATER
	private HybridCacheEntryOptions CreateHybridCacheEntryOptions()
		=> new()
		{
			Expiration = _memoryCacheOptions.CacheTimeout,
			LocalCacheExpiration = _memoryCacheOptions.CacheTimeout,
			Flags = _memoryCacheOptions.CacheEntryFlags
		};
#else
	private DistributedCacheEntryOptions CreateDistributedCacheEntryOptions()
		=> new DistributedCacheEntryOptions().SetAbsoluteExpiration(_memoryCacheOptions.CacheTimeout);
#endif

	private sealed record DynamicImageMemoryCacheEntry(DateTimeOffset? LastModified, DynamicImageOptionsCacheEntry ImageOptions, byte[] Content);

	private sealed record DynamicImageOptionsCacheEntry(
		string SourcePath,
		int Width,
		int Height,
		DynamicResizeMode ResizeMode,
		DynamicImageFormat Format,
		DynamicImageFilterQuality FilterQuality,
		int QualityRequest,
		double? FocalPointX,
		double? FocalPointY,
		string? VersionToken)
	{
		public static DynamicImageOptionsCacheEntry From(in DynamicImageOptions options)
			=> new(
				options.SourcePath,
				options.Width,
				options.Height,
				options.ResizeMode,
				options.Format,
				options.FilterQuality,
				options.QualityRequest,
				options.FocalPointX,
				options.FocalPointY,
				options.VersionToken);

		public DynamicImageOptions ToOptions()
			=> new(SourcePath, Width, Height, ResizeMode, Format, FilterQuality, QualityRequest, FocalPointX, FocalPointY, VersionToken);
	}
	#endregion
}
