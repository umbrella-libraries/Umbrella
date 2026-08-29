using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.Utilities.Caching.Abstractions;

[assembly: InternalsVisibleTo("Umbrella.DynamicImage.Caching.AzureStorage.Test")]

namespace Umbrella.DynamicImage.Caching.AzureStorage;

/// <summary>
/// A Dynamic Image cache implementation that is backed by Azure Blob Storage.
/// </summary>
public class DynamicImageAzureBlobStorageCache : DynamicImagePhysicalCache<IUmbrellaAzureBlobFileStorageProvider>, IDynamicImageCache
{
	#region Protected Properties		
	/// <summary>
	/// Gets the BLOB storage cache options.
	/// </summary>
	protected DynamicImageAzureBlobStorageCacheOptions BlobStorageCacheOptions { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageAzureBlobStorageCache"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="cacheOptions">The cache options.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="blobStorageCacheOptions">The BLOB storage cache options.</param>
	public DynamicImageAzureBlobStorageCache(
		ILogger<DynamicImageAzureBlobStorageCache> logger,
		ICacheKeyUtility cacheKeyUtility,
		DynamicImageCacheCoreOptions cacheOptions,
		IUmbrellaAzureBlobFileStorageProvider fileProvider,
		DynamicImageAzureBlobStorageCacheOptions blobStorageCacheOptions)
		: base(logger, cacheKeyUtility, cacheOptions, fileProvider)
	{
		BlobStorageCacheOptions = blobStorageCacheOptions;
	}
	#endregion

	#region Overridden Methods
	/// <inheritdoc />
	protected override string GetSubPath(string cacheKey, string fileExtension)
		=> $"/{BlobStorageCacheOptions.DirectoryName}{base.GetSubPath(cacheKey, fileExtension)}";
	#endregion
}
