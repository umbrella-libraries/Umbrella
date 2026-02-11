
using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.DynamicImage"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder
	/// with in-memory caching.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="dynamicImageMemoryCacheOptionsBuilder">The optional <see cref="DynamicImageMemoryCacheOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDynamicImageMemoryCache(
		this IServiceCollection services,
		Action<IServiceProvider, DynamicImageMemoryCacheOptions>? dynamicImageMemoryCacheOptionsBuilder = null)
	{
		Guard.IsNotNull(services);

		_ = services.AddUmbrellaDynamicImage();
		_ = services.ReplaceSingleton<IDynamicImageCache, DynamicImageMemoryCache>();
		_ = services.ConfigureUmbrellaOptions(dynamicImageMemoryCacheOptionsBuilder);

		return services;
	}

	/// <summary>
	/// Adds the core <see cref="Umbrella.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder. Caching is disabled by default
	/// which means dynamically generated images are created every time they are requested.
	/// To enable caching, call one of the AddUmbrellaDynamicImage*Cache, e.g. <see cref="AddUmbrellaDynamicImageMemoryCache"/>.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDynamicImage(this IServiceCollection services)
	{
		Guard.IsNotNull(services);

		_ = services.AddSingleton<IDynamicImageUtility, DynamicImageUtility>();
		_ = services.AddSingleton<IDynamicImageCache, DynamicImageNoCache>();
		_ = services.AddSingleton<IUmbrellaDynamicImageCacheFileHandler, UmbrellaDynamicImageCacheFileHandler>();
		_ = services.AddSingleton<DynamicImageCacheCoreOptions>();

		return services;
	}
}