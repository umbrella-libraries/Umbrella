
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Umbrella.Legacy.WebUtilities.Hosting;
using Umbrella.Legacy.WebUtilities.Middleware;
using Umbrella.Legacy.WebUtilities.Mvc.Bundles;
using Umbrella.Legacy.WebUtilities.Mvc.Bundles.Abstractions;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.WebUtilities.Hosting;

[assembly: InternalsVisibleTo("Umbrella.Legacy.WebUtilities.Test")]
[assembly: InternalsVisibleTo("Umbrella.Legacy.WebUtilities.Benchmark")]

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Legacy.WebUtilities"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Legacy.WebUtilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaLegacyWebUtilities(this IServiceCollection services)
		=> services.AddUmbrellaLegacyWebUtilities<UmbrellaWebHostingEnvironment>();

	/// <summary>
	/// Adds the <see cref="Umbrella.Legacy.WebUtilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TUmbrellaWebHostingEnvironment">
	/// The concrete implementation of <see cref="IUmbrellaWebHostingEnvironment"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaHostingEnvironment"/> and <see cref="IUmbrellaWebHostingEnvironment"/> interfaces.
	/// </typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaLegacyWebUtilities<TUmbrellaWebHostingEnvironment>(this IServiceCollection services)
		where TUmbrellaWebHostingEnvironment : class, IUmbrellaWebHostingEnvironment
	{
		Guard.IsNotNull(services);

		// Add the hosting environment as a singleton and then ensure the same instance is bound to both interfaces
		_ = services.AddSingleton<TUmbrellaWebHostingEnvironment>();
		_ = services.ReplaceSingleton<IUmbrellaHostingEnvironment>(x => x.GetRequiredService<TUmbrellaWebHostingEnvironment>());
		_ = services.ReplaceSingleton<IUmbrellaWebHostingEnvironment>(x => x.GetRequiredService<TUmbrellaWebHostingEnvironment>());

		_ = services.AddSingleton<IMvcBundleUtility, MvcBundleUtility>();
		_ = services.AddSingleton<IMvcWebpackBundleUtility, MvcWebpackBundleUtility>();

		_ = services.AddSingleton<DebugRequestMiddleware>();
		_ = services.AddSingleton<FrontEndCompressionMiddleware>();

		return services;
	}
}