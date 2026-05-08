using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.SharePoint;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.FileSystem.SharePoint"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.SharePoint"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaSharePointFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaSharePointFileStorageProvider(this IServiceCollection services, Action<IServiceProvider, UmbrellaSharePointFileStorageProviderOptions> optionsBuilder)
		=> AddUmbrellaSharePointFileStorageProvider<UmbrellaSharePointFileStorageProvider>(services, optionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.SharePoint"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TFileProvider">
	/// The concrete implementation of <see cref="IUmbrellaSharePointFileStorageProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaFileStorageProvider"/> and <see cref="IUmbrellaSharePointFileStorageProvider"/> interfaces.
	/// </typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaSharePointFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaSharePointFileStorageProvider<TFileProvider>(this IServiceCollection services, Action<IServiceProvider, UmbrellaSharePointFileStorageProviderOptions> optionsBuilder)
		where TFileProvider : class, IUmbrellaSharePointFileStorageProvider
		=> AddUmbrellaSharePointFileStorageProvider<TFileProvider, UmbrellaSharePointFileStorageProviderOptions>(services, optionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.SharePoint"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TFileProvider">
	/// The concrete implementation of <see cref="IUmbrellaSharePointFileStorageProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaFileStorageProvider"/> and <see cref="IUmbrellaSharePointFileStorageProvider"/> interfaces.
	/// </typeparam>
	/// <typeparam name="TOptions">The type of the options.</typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <typeparamref name="TOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaSharePointFileStorageProvider<TFileProvider, TOptions>(this IServiceCollection services, Action<IServiceProvider, TOptions> optionsBuilder)
		where TFileProvider : class, IUmbrellaSharePointFileStorageProvider
		where TOptions : UmbrellaSharePointFileStorageProviderOptions, new()
	{
		Guard.IsNotNull(services);
		Guard.IsNotNull(optionsBuilder);

		_ = services.AddUmbrellaFileSystemCore();

		_ = services.AddSingleton<IUmbrellaSharePointFileStorageProvider>(x =>
		{
			var factory = x.GetRequiredService<IUmbrellaFileStorageProviderFactory>();
			var options = x.GetRequiredService<TOptions>();

			return factory.CreateProvider<TFileProvider, TOptions>(options, services);
		});
		_ = services.ReplaceSingleton<IUmbrellaFileStorageProvider>(x => x.GetRequiredService<IUmbrellaSharePointFileStorageProvider>());

		// Options
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		_ = services.AddSingleton<IUmbrellaFileStorageProviderOptions>(x => x.GetRequiredService<TOptions>());

		return services;
	}
}
