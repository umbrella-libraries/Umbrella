


using Microsoft.Extensions.DependencyInjection;

namespace Umbrella.Utilities.Options.Abstractions;

/// <summary>
/// Allows an options class to access the <see cref="IServiceCollection"/> used to build the specified <see cref="IServiceProvider"/>.
/// </summary>
public interface IServicesResolverUmbrellaOptions
{
	/// <summary>
	/// Performs initialization.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="serviceProvider">The service provider.</param>
	void Initialize(IServiceCollection services, IServiceProvider serviceProvider);
}