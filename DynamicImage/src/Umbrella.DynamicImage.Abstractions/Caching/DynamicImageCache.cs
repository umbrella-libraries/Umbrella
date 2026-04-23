using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;

namespace Umbrella.DynamicImage.Abstractions.Caching;

/// <summary>
/// Serves as the base class for all Dynamic Image caches and provides the core functionality for all derived types.
/// </summary>
public abstract class DynamicImageCache
{
	#region Protected Properties		
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
	/// Gets the cache options.
	/// </summary>
	protected DynamicImageCacheCoreOptions CacheOptions { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageCache"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="cacheOptions">The cache options.</param>
	protected DynamicImageCache(
		ILogger logger,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility,
		DynamicImageCacheCoreOptions cacheOptions)
	{
		Logger = logger;
		Cache = cache;
		CacheKeyUtility = cacheKeyUtility;
		CacheOptions = cacheOptions;
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Generates the cache key.
	/// </summary>
	/// <param name="options">The image options used to generate the key.</param>
	/// <returns>The generated cache key.</returns>
	/// <exception cref="UmbrellaDynamicImageException">There was a problem generating the cache key.</exception>
	protected virtual string GenerateCacheKey(DynamicImageOptions options)
	{
		try
		{
			string cacheKey = CacheKeyUtility.Create<DynamicImageOptions>(options.ToString());

			return Cache.GetOrCreate(cacheKey, () =>
			{
				string rawKey = string.Format(CultureInfo.InvariantCulture, "{0}-W-{1}-H-{2}-M-{3}-F-{4}-FPX-{5}-FPY-{6}-P", options.Width, options.Height, options.ResizeMode, options.Format, options.SourcePath, options.FocalPointX?.ToString("G4", CultureInfo.InvariantCulture) ?? "null", options.FocalPointY?.ToString("G4", CultureInfo.InvariantCulture) ?? "null");

				using var hasher = SHA256.Create();
				byte[] bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(rawKey));

				var stringBuilder = new StringBuilder(bytes.Length * 2);

				foreach (byte num in bytes)
					_ = stringBuilder.Append(num.ToString("x", CultureInfo.InvariantCulture).PadLeft(2, '0'));

				return stringBuilder.ToString();
			},
			CacheOptions);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options }))
		{
			throw new UmbrellaDynamicImageException("There was a problem generating the cache key.", exc, options);
		}
	}

	/// <summary>
	/// Gets the sub path.
	/// </summary>
	/// <param name="cacheKey">The cache key.</param>
	/// <param name="fileExtension">The file extension.</param>
	/// <returns>The sub path.</returns>
	/// <exception cref="NotImplementedException"></exception>
	protected virtual string GetSubPath(string cacheKey, string fileExtension) => throw new NotImplementedException();
	#endregion
}