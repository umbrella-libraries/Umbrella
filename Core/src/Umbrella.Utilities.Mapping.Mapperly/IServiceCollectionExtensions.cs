
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly;
using Umbrella.Utilities.Mapping.Mapperly.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Utilities.Mapping.Mapperly"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Utilities.Mapping.Mapperly"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaUtilitiesMappingMapperly(
		this IServiceCollection services,
		Action<IServiceProvider, UmbrellaMapperOptions> optionsBuilder)
	{
		Guard.IsNotNull(services);

		_ = services.ReplaceSingleton<IUmbrellaMapper, UmbrellaMapper>();
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		return services;
	}
}