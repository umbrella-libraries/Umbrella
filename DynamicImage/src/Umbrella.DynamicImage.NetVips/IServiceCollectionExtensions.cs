using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.NetVips;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register NetVips Dynamic Image services.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Umbrella Dynamic Image NetVips services to the <see cref="IServiceCollection"/>.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
	public static IServiceCollection AddUmbrellaDynamicImageNetVips(this IServiceCollection services)
	{
		Guard.IsNotNull(services);

		_ = services.AddSingleton<IDynamicImageResizer, DynamicImageResizer>();

		return services;
	}
}
