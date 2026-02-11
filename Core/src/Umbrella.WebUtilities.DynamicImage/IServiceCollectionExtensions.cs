
using CommunityToolkit.Diagnostics;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.WebUtilities.DynamicImage"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.WebUtilities.DynamicImage"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The optional <see cref="DynamicImageMiddlewareOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaWebUtilitiesDynamicImage(
		this IServiceCollection services,
		Action<IServiceProvider, DynamicImageMiddlewareOptions>? optionsBuilder = null)
	{
		Guard.IsNotNull(services, nameof(services));

		// Options
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		return services;
	}
}