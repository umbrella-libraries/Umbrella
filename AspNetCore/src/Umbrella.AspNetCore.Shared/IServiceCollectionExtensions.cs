using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.Shared.Services;
using Umbrella.AspNetCore.Shared.Services.Abstractions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AspNetCore.Shared"/> package with a
/// specified <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.Shared"/> services to the specified <see cref="IServiceCollection"/>
	/// dependency injection container builder.
	/// </summary>
	/// <returns>The services builder.</returns>
	public static IServiceCollection AddUmbrellaAspNetCoreShared(this IServiceCollection services)
	{
		Guard.IsNotNull(services);

		// Security
		_ = services.AddScoped<IClaimsPrincipalAccessorService, ClaimsPrincipalAccessorService>();

		return services;
	}
}