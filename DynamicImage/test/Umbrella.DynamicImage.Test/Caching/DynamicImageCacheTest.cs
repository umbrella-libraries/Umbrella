
using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.DynamicImage.Caching.AzureStorage;
using Umbrella.DynamicImage.Caching.Disk;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Disk;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Helpers;

namespace Umbrella.DynamicImage.Test.Caching;

public class DynamicImageCacheTest : IClassFixture<DynamicImageAzuriteContainerFixture>
{
	private const string TestFileName = "aspnet-mvc-logo.png";
	private static string _storageConnectionString = null!;

	private static readonly List<Func<IDynamicImageCache>> _cacheList =
	[
		CreateDynamicImageMemoryCache(),
		CreateDynamicImageDiskCache(),
		CreateDynamicImageAzureBlobStorageCache()
	];

	public static List<object[]> CacheListMemberData = _cacheList.Select(x => new object[] { x }).ToList();

	public DynamicImageCacheTest(DynamicImageAzuriteContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		_storageConnectionString = fixture.ConnectionString;
	}

	private static string? _baseDirectory;

	private static string BaseDirectory
	{
		get
		{
			if (string.IsNullOrEmpty(_baseDirectory))
			{
				string baseDirectory = AppContext.BaseDirectory;
				int indexToEndAt = baseDirectory.IndexOf(PathHelper.PlatformNormalize($@"\bin\{DebugUtility.BuildConfiguration}\net10.0"), StringComparison.OrdinalIgnoreCase);
				_baseDirectory = baseDirectory.Remove(indexToEndAt, baseDirectory.Length - indexToEndAt);
			}

			return _baseDirectory;
		}
	}

	[Theory]
	[MemberData(nameof(CacheListMemberData))]
	public async Task AddAsync_RemoveAsync_BytesAsync(Func<IDynamicImageCache> cacheFactory)
	{
		Guard.IsNotNull(cacheFactory);
		IDynamicImageCache cache = cacheFactory();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		var item = new DynamicImageItem
		{
			ImageOptions = new DynamicImageOptions("/sometestpath/image.png", 100, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			LastModified = DateTime.UtcNow
		};

		byte[] sourceBytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		item.Content = sourceBytes;

		await cache.AddAsync(item, TestContext.Current.CancellationToken);

		DynamicImageItem? cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.UtcNow.AddMinutes(-5), "jpg", TestContext.Current.CancellationToken);

		Assert.NotNull(cachedItem);
		Assert.Equal(item.ImageOptions, cachedItem!.ImageOptions);

		ReadOnlyMemory<byte> cachedBytes = await cachedItem.GetContentAsync(TestContext.Current.CancellationToken);

		Assert.Equal(sourceBytes.Length, cachedBytes!.Length);

		//Perform cleanup by removing the file from the cache
		await cache.RemoveAsync(item.ImageOptions, "jpg", TestContext.Current.CancellationToken);

		cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.UtcNow.AddMinutes(-5), "jpg", TestContext.Current.CancellationToken);

		Assert.Null(cachedItem);
	}

	[Theory]
	[MemberData(nameof(CacheListMemberData))]
	public async Task AddAsync_RemoveAsync_StreamAsync(Func<IDynamicImageCache> cacheFactory)
	{
		Guard.IsNotNull(cacheFactory);
		IDynamicImageCache cache = cacheFactory();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		var item = new DynamicImageItem
		{
			ImageOptions = new DynamicImageOptions("/sometestpath/image.png", 100, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			LastModified = DateTime.UtcNow
		};

		byte[] sourceBytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		item.Content = sourceBytes;

		await cache.AddAsync(item, TestContext.Current.CancellationToken);

		DynamicImageItem? cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.UtcNow.AddMinutes(-5), "jpg", TestContext.Current.CancellationToken);

		Assert.NotNull(cachedItem);
		Assert.Equal(item.ImageOptions, cachedItem!.ImageOptions);

		byte[]? cachedBytes = null;

		using (var ms = new MemoryStream())
		{
			await cachedItem.WriteContentToStreamAsync(ms, TestContext.Current.CancellationToken);
			cachedBytes = ms.ToArray();
		}

		Assert.Equal(sourceBytes.Length, cachedBytes.Length);

		//Perform cleanup by removing the file from the cache
		await cache.RemoveAsync(item.ImageOptions, "jpg", TestContext.Current.CancellationToken);

		cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.UtcNow.AddMinutes(-5), "jpg", TestContext.Current.CancellationToken);

		Assert.Null(cachedItem);
	}

	[Theory]
	[MemberData(nameof(CacheListMemberData))]
	public async Task GetAsync_NotExistsAsync(Func<IDynamicImageCache> cacheFactory)
	{
		Guard.IsNotNull(cacheFactory);
		IDynamicImageCache cache = cacheFactory();

		string path = PathHelper.PlatformNormalize($@"{BaseDirectory}\doesnotexist.png");

		var item = new DynamicImageItem
		{
			ImageOptions = new DynamicImageOptions(path, 200, 200, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			LastModified = DateTime.UtcNow
		};

		DynamicImageItem? cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.MinValue, "jpg", TestContext.Current.CancellationToken);

		Assert.Null(cachedItem);
	}

	[Theory]
	[MemberData(nameof(CacheListMemberData))]
	public async Task AddAsync_GetAsync_ExpiredAsync(Func<IDynamicImageCache> cacheFactory)
	{
		Guard.IsNotNull(cacheFactory);
		IDynamicImageCache cache = cacheFactory();

		string path = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		var item = new DynamicImageItem
		{
			ImageOptions = new DynamicImageOptions(path, 100, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg),
			LastModified = DateTime.UtcNow
		};

		byte[] sourceBytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

		item.Content = sourceBytes;

		await cache.AddAsync(item, TestContext.Current.CancellationToken);

		DynamicImageItem? cachedItem = await cache.GetAsync(item.ImageOptions, DateTime.UtcNow.AddMinutes(5), "jpg", TestContext.Current.CancellationToken);

		Assert.Null(cachedItem);
	}

	private static Func<IDynamicImageCache> CreateDynamicImageDiskCache() => () =>
	{
		var options = new UmbrellaDiskFileStorageProviderOptions
		{
			RootPhysicalPath = BaseDirectory,
			AllowUnhandledFileAuthorizationChecks = true
		};

		var provider = new UmbrellaDiskFileStorageProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaDiskFileStorageProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg,", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());

		provider.InitializeOptions(options);

		return new DynamicImageDiskCache(
			CoreUtilitiesMocks.CreateLogger<DynamicImageDiskCache>(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			new DynamicImageCacheCoreOptions(),
			provider,
			new DynamicImageDiskCacheOptions());
	};

	private static Func<IDynamicImageCache> CreateDynamicImageMemoryCache() => () => new DynamicImageMemoryCache(
			CoreUtilitiesMocks.CreateLogger<DynamicImageMemoryCache>(),
			CoreUtilitiesMocks.CreateCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			new DynamicImageCacheCoreOptions(),
			new DynamicImageMemoryCacheOptions());

	private static Func<IDynamicImageCache> CreateDynamicImageAzureBlobStorageCache() => () =>
	{
		var options = new UmbrellaAzureBlobStorageFileProviderOptions
		{
			StorageConnectionString = _storageConnectionString,
			AllowUnhandledFileAuthorizationChecks = true
		};

		var provider = new UmbrellaAzureBlobStorageFileProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaAzureBlobStorageFileProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg,", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());

		provider.InitializeOptions(options);

		var blobStorageCacheOptions = new DynamicImageAzureBlobStorageCacheOptions();

		return new DynamicImageAzureBlobStorageCache(
			CoreUtilitiesMocks.CreateLogger<DynamicImageAzureBlobStorageCache>(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			new DynamicImageCacheCoreOptions(),
			provider,
			blobStorageCacheOptions);
	};

	private static UmbrellaFileAuthorizationHandlerRegistry CreateAuthorizationHandlerRegistry()
		=> new UmbrellaFileAuthorizationHandlerRegistry([]);
}
