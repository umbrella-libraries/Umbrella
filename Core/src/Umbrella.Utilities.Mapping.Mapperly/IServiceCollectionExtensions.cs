
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly;
using Umbrella.Utilities.Mapping.Mapperly.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Utilities.Mapping.Mapperly"/> package with a
/// specified <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Adds the <see cref="Umbrella.Utilities.Mapping.Mapperly"/> services to the specified
		/// <see cref="IServiceCollection"/> dependency injection container builder.
		/// </summary>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public IServiceCollection AddUmbrellaUtilitiesMappingMapperly(
			Action<IServiceProvider, UmbrellaMapperOptions> optionsBuilder)
		{
			Guard.IsNotNull(services);

			_ = services.ReplaceSingleton<IUmbrellaMapper, UmbrellaMapper>();
			_ = services.ConfigureUmbrellaOptions(optionsBuilder);

			return services;
		}

		/// <summary>
		/// Registers the UmbrellaMapper and configures UmbrellaMapperOptions with the specified target assembly name prefix.
		/// </summary>
		/// <param name="targetAssemblyNamePrefix">The prefix used to filter target assemblies for mapping.</param>
		/// <returns>The service collection with mapping services registered.</returns>
		public IServiceCollection AddUmbrellaUtilitiesMappingMapperly(
			string targetAssemblyNamePrefix)
		{
			Guard.IsNotNull(services);

			_ = services.ReplaceSingleton<IUmbrellaMapper, UmbrellaMapper>();
			_ = services.ConfigureUmbrellaOptions<UmbrellaMapperOptions>((_, options) => options.TargetAssemblyNamePrefix = targetAssemblyNamePrefix);
			
			return services;
		}
	}
}