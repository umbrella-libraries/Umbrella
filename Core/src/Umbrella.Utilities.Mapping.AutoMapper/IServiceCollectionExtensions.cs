


using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.AutoMapper;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Utilities.Mapping.AutoMapper"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Utilities.Mapping.AutoMapper"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaUtilitiesMappingAutoMapper(this IServiceCollection services)
	{
		Guard.IsNotNull(services);

		_ = services.ReplaceSingleton<IUmbrellaMapper, UmbrellaMapper>();

		return services;
	}
}