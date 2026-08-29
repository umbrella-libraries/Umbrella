using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Imaging;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

#if NET9_0_OR_GREATER
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif

namespace Umbrella.Internal.Mocks;

public static class CoreUtilitiesMocks
{
	public static IDataLookupNormalizer CreateILookupNormalizer()
	{
		var lookupNormalizer = new Mock<IDataLookupNormalizer>();
		_ = lookupNormalizer.Setup(x => x.Normalize(It.IsAny<string>(), It.IsAny<bool>())).Returns<string, bool>((value, trim) =>
		{
			return trim ? value.Trim().ToUpperInvariant() : value.ToUpperInvariant();
		});

		return lookupNormalizer.Object;
	}

	public static ICacheKeyUtility CreateCacheKeyUtility() => new CacheKeyUtility(new Mock<ILogger<CacheKeyUtility>>().Object, CreateILookupNormalizer());

	public static PlatformCache CreateCache()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDistributedMemoryCache();
#if NET9_0_OR_GREATER
		_ = services.AddHybridCache();
#endif

		return services.BuildServiceProvider().GetRequiredService<PlatformCache>();
	}

	public static IGenericTypeConverter CreateGenericTypeConverter()
	{
		var genericTypeConverter = new Mock<IGenericTypeConverter>();
		_ = genericTypeConverter.Setup(x => x.Convert(It.IsAny<string>(), (string)null!, null)).Returns<string, string, Func<string, string>>((x, y, z) => x);

		return genericTypeConverter.Object;
	}

	public static ILogger<T> CreateLogger<T>() => new Mock<ILogger<T>>().Object;

	public static ILoggerFactory CreateLoggerFactory<T>()
	{
		var loggerFactory = new Mock<ILoggerFactory>();
		_ = loggerFactory.Setup(x => x.CreateLogger(typeof(T).FullName ?? "Default")).Returns(CreateLogger<T>());

		return loggerFactory.Object;
	}

	public static IMimeTypeUtility CreateMimeTypeUtility(params (string extension, string mimeType)[] mappings)
	{
		var mimeTypeUtility = new Mock<IMimeTypeUtility>();
		mappings.ForEach(mapping => mimeTypeUtility.Setup(x => x.GetMimeType(It.Is<string>(y => !string.IsNullOrEmpty(y) && y.Trim().ToLowerInvariant().EndsWith(mapping.extension)))).Returns(mapping.mimeType));

		return mimeTypeUtility.Object;
	}

	public static IResponsiveImageHelper CreateResponsiveImageHelper() => new ResponsiveImageHelper(CreateLogger<ResponsiveImageHelper>());
}
