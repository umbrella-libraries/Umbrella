


using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Umbrella.AspNetCore.DataAnnotations;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AspNetCore.DataAnnotations"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.DataAnnotations"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaAspNetCoreDataAnnotations(this IServiceCollection services)
	{
		Guard.IsNotNull(services, nameof(services));

		// Adding this as an additional implementation rather than replacing so the in-built provider will still be used in addition
		// to our custom one.
		_ = services.AddSingleton<IValidationAttributeAdapterProvider, UmbrellaValidationAttributeAdapterProvider>();

		return services;
	}
}