


using CommunityToolkit.Diagnostics;
using Umbrella.Legacy.Utilities.Configuration;
using Umbrella.Utilities.Configuration.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Legacy.Utilities"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Legacy.Utilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaLegacyUtilitiesAppSettings(this IServiceCollection services)
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.AddSingleton<IReadOnlyAppSettingsSource, AppConfigReadOnlyAppSettingsSource>();

		return services;
	}
}