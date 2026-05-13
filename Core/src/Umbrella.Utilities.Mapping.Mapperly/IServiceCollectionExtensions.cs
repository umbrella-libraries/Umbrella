using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: InternalsVisibleTo("Umbrella.Utilities.Mapping.Mapperly.Test")]

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
		/// <see cref="IServiceCollection"/> dependency injection container builder using one or more
		/// source-generated Mapperly catalogs.
		/// </summary>
		/// <remarks>
		/// Generated catalogs are typically emitted by the <c>Umbrella.Generators.Mapperly</c> package and exposed as an
		/// <c>Instance</c> property from the consuming assembly, e.g.
		/// <c>services.AddUmbrellaUtilitiesMappingMapperly(MyAppUmbrellaMapperlyCatalog.Instance);</c>
		/// </remarks>
		/// <param name="catalogs">The source-generated Mapperly catalogs to compose.</param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
		public IServiceCollection AddUmbrellaUtilitiesMappingMapperly(
			params IUmbrellaMapperlyCatalog[] catalogs)
		{
			Guard.IsNotNull(services);
			Guard.IsNotNull(catalogs);

			if (catalogs.Length is 0)
				throw new ArgumentException("At least one Mapperly catalog must be specified.", nameof(catalogs));

			UmbrellaMapperRegistryBuilder builder = new();

			foreach (IUmbrellaMapperlyCatalog catalog in catalogs)
			{
				Guard.IsNotNull(catalog);
				catalog.AddServices(services);
				catalog.AddMappings(builder);
			}

			UmbrellaMapperRegistry registry = builder.Build();

			_ = services.ReplaceSingleton<IUmbrellaMapper>(serviceProvider =>
			{
				ILogger<UmbrellaMapper> logger = serviceProvider.GetRequiredService<ILogger<UmbrellaMapper>>();
				return new UmbrellaMapper(logger, serviceProvider, registry);
			});

			return services;
		}
	}
}