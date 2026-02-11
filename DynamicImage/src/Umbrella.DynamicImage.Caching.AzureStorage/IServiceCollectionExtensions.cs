


using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.DynamicImage.Caching.AzureStorage;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.DynamicImage.Caching.AzureStorage"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.DynamicImage.Caching.AzureStorage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder
	/// with Azure Blob Storage caching.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="dynamicImageAzureBlobStorageCacheOptionsBuilder">The optional <see cref="DynamicImageAzureBlobStorageCacheOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDynamicImageAzureBlobStorageCache(
		this IServiceCollection services,
		Action<IServiceProvider, DynamicImageAzureBlobStorageCacheOptions>? dynamicImageAzureBlobStorageCacheOptionsBuilder = null)
	{
		Guard.IsNotNull(services);

		_ = services.AddUmbrellaDynamicImage();
		_ = services.ReplaceSingleton<IDynamicImageCache, DynamicImageAzureBlobStorageCache>();
		_ = services.ConfigureUmbrellaOptions(dynamicImageAzureBlobStorageCacheOptionsBuilder);

		_ = services.ReplaceSingleton<DynamicImageCacheCoreOptions>(x => x.GetRequiredService<DynamicImageAzureBlobStorageCacheOptions>());

		return services;
	}
}