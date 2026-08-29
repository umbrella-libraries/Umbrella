
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.FileSystem.Disk;
using Umbrella.Utilities.Caching.Abstractions;

namespace Umbrella.DynamicImage.Caching.Disk;

/// <summary>
/// A Dynamic Image cache implementation that is backed by Disk.
/// </summary>
public class DynamicImageDiskCache : DynamicImagePhysicalCache<IUmbrellaDiskFileStorageProvider>, IDynamicImageCache
{
	#region Protected Properties		
	/// <summary>
	/// Gets the disk cache options.
	/// </summary>
	protected DynamicImageDiskCacheOptions DiskCacheOptions { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageDiskCache"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="cacheOptions">The cache options.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="diskCacheOptions">The disk cache options.</param>
	public DynamicImageDiskCache(ILogger<DynamicImageDiskCache> logger,
		ICacheKeyUtility cacheKeyUtility,
		DynamicImageCacheCoreOptions cacheOptions,
		IUmbrellaDiskFileStorageProvider fileProvider,
		DynamicImageDiskCacheOptions diskCacheOptions)
		: base(logger, cacheKeyUtility, cacheOptions, fileProvider)
	{
		Guard.IsNotNull(diskCacheOptions);
		Guard.IsNotNullOrWhiteSpace(diskCacheOptions.DirectoryName);

		DiskCacheOptions = diskCacheOptions;
	}
	#endregion

	#region Overridden Methods
	/// <inheritdoc />
	protected override string GetSubPath(string cacheKey, string fileExtension)
	{
		Guard.IsNotNull(cacheKey);

		return $@"/{DiskCacheOptions.DirectoryName}/{cacheKey.Substring(0, 2)}{base.GetSubPath(cacheKey, fileExtension)}";
	}
	#endregion
}
