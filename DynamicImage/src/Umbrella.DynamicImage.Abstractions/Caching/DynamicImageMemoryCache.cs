using Microsoft.Extensions.Logging;
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
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility,
		DynamicImageCacheCoreOptions cacheOptions,
		DynamicImageMemoryCacheOptions memoryCacheOptions)
		: base(logger, cache, cacheKeyUtility, cacheOptions)
	{
		_memoryCacheOptions = memoryCacheOptions;
	}
	#endregion

	#region IDynamicImageCache Members
	/// <inheritdoc />
	public Task AddAsync(DynamicImageItem dynamicImage, CancellationToken cancellationToken = default)
	{
		CommunityToolkit.Diagnostics.Guard.IsNotNull(dynamicImage);

		try
		{
			string rawKey = GenerateCacheKey(dynamicImage.ImageOptions);
			string cacheKey = GenerateMemoryCacheKey(rawKey);

			_ = Cache.SetValue(cacheKey, dynamicImage, _memoryCacheOptions);

			return Task.CompletedTask;
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
			string fileKey = GenerateCacheKey(options);
			string cacheKey = GenerateMemoryCacheKey(fileKey);

			var (itemFound, cacheItem) = await Cache.TryGetValueAsync<DynamicImageItem>(cacheKey, cancellationToken: cancellationToken);

			if (cacheItem is not null)
			{
				//If the file does not exist or has been modified since the IDynamicImage was generated,
				//evict it from the cache
				if (sourceLastModified > cacheItem.LastModified)
				{
					await Cache.RemoveAsync<DynamicImageItem>(cacheKey, cancellationToken: cancellationToken).ConfigureAwait(false);

					return null;
				}
			}

			return cacheItem;
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

			await Cache.RemoveAsync<DynamicImageItem>(cacheKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options, fileExtension }))
		{
			throw new UmbrellaDynamicImageException("There was a problem removing the image from the cache.", exc);
		}
	}
	#endregion

	#region Private Methods
	private string GenerateMemoryCacheKey(string key) => CacheKeyUtility.Create<DynamicImageMemoryCache>(key);
	#endregion
}
