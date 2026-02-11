
using CommunityToolkit.Diagnostics;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.DataAccess.Abstractions"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Adds the <see cref="Umbrella.DataAccess.Abstractions"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
		/// </summary>
		/// <param name="repoDataServiceOptionsBuilder"></param>
		/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
		public IServiceCollection AddUmbrellaDataAccess(
			Action<IServiceProvider, UmbrellaRepositoryDataServiceOptions>? repoDataServiceOptionsBuilder = null)
		{
			Guard.IsNotNull(services);

			_ = services.AddSingleton<IEntityValidator, EntityValidator>();
			_ = services.AddSingleton<IEntityMappingUtility, EntityMappingUtility>();
			_ = services.AddScoped<IUmbrellaDbContextHelper, UmbrellaDbContextHelper>();
			_ = services.AddScoped(typeof(DbAppTenantSessionContext<>));

			_ = services.AddScoped<IUmbrellaRepositoryCoreDataService, UmbrellaRepositoryCoreDataService>();

			_ = services.ConfigureUmbrellaOptions(repoDataServiceOptionsBuilder);

			return services;
		}
	}
}