using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.Dataverse;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.FileSystem.Dataverse"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.Dataverse"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaDataverseFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDataverseFileStorageProvider(this IServiceCollection services, Action<IServiceProvider, UmbrellaDataverseFileStorageProviderOptions> optionsBuilder)
		=> AddUmbrellaDataverseFileStorageProvider<UmbrellaDataverseFileStorageProvider>(services, optionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.Dataverse"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TFileProvider">
	/// The concrete implementation of <see cref="IUmbrellaDataverseFileStorageProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaFileStorageProvider"/> and <see cref="IUmbrellaDataverseFileStorageProvider"/> interfaces.
	/// </typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaDataverseFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDataverseFileStorageProvider<TFileProvider>(this IServiceCollection services, Action<IServiceProvider, UmbrellaDataverseFileStorageProviderOptions> optionsBuilder)
		where TFileProvider : class, IUmbrellaDataverseFileStorageProvider
		=> AddUmbrellaDataverseFileStorageProvider<TFileProvider, UmbrellaDataverseFileStorageProviderOptions>(services, optionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.Dataverse"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TFileProvider">
	/// The concrete implementation of <see cref="IUmbrellaDataverseFileStorageProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaFileStorageProvider"/> and <see cref="IUmbrellaDataverseFileStorageProvider"/> interfaces.
	/// </typeparam>
	/// <typeparam name="TOptions">The type of the options.</typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <typeparamref name="TOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDataverseFileStorageProvider<TFileProvider, TOptions>(this IServiceCollection services, Action<IServiceProvider, TOptions> optionsBuilder)
		where TFileProvider : class, IUmbrellaDataverseFileStorageProvider
		where TOptions : UmbrellaDataverseFileStorageProviderOptions, new()
	{
		Guard.IsNotNull(services);
		Guard.IsNotNull(optionsBuilder);

		_ = services.AddUmbrellaFileSystemCore();

		_ = services.AddSingleton<IUmbrellaDataverseFileStorageProvider>(x =>
		{
			var factory = x.GetRequiredService<IUmbrellaFileStorageProviderFactory>();
			var options = x.GetRequiredService<TOptions>();

			return factory.CreateProvider<TFileProvider, TOptions>(options, services);
		});
		_ = services.ReplaceSingleton<IUmbrellaFileStorageProvider>(x => x.GetRequiredService<IUmbrellaDataverseFileStorageProvider>());

		// Options
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		_ = services.AddSingleton<IUmbrellaFileStorageProviderOptions>(x => x.GetRequiredService<TOptions>());

		return services;
	}

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.Dataverse"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder,
	/// pre-configured for the standard Dataverse <c>annotation</c> table. Only <see cref="UmbrellaDataverseFileStorageProviderOptions.DataverseClient"/>
	/// and, optionally, <see cref="UmbrellaDataverseFileStorageProviderOptions.MetadataColumnMappings"/> need to be supplied via <paramref name="optionsBuilder"/>.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaDataverseAnnotationFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDataverseAnnotationFileStorageProvider(this IServiceCollection services, Action<IServiceProvider, UmbrellaDataverseAnnotationFileStorageProviderOptions> optionsBuilder)
		=> AddUmbrellaDataverseAnnotationFileStorageProvider<UmbrellaDataverseFileStorageProvider>(services, optionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.FileSystem.Dataverse"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder,
	/// pre-configured for the standard Dataverse <c>annotation</c> table. Only <see cref="UmbrellaDataverseFileStorageProviderOptions.DataverseClient"/>
	/// and, optionally, <see cref="UmbrellaDataverseFileStorageProviderOptions.MetadataColumnMappings"/> need to be supplied via <paramref name="optionsBuilder"/>.
	/// </summary>
	/// <typeparam name="TFileProvider">
	/// The concrete implementation of <see cref="IUmbrellaDataverseFileStorageProvider"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaFileStorageProvider"/> and <see cref="IUmbrellaDataverseFileStorageProvider"/> interfaces.
	/// </typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The <see cref="UmbrellaDataverseAnnotationFileStorageProviderOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="optionsBuilder"/> is null.</exception>
	public static IServiceCollection AddUmbrellaDataverseAnnotationFileStorageProvider<TFileProvider>(this IServiceCollection services, Action<IServiceProvider, UmbrellaDataverseAnnotationFileStorageProviderOptions> optionsBuilder)
		where TFileProvider : class, IUmbrellaDataverseFileStorageProvider
		=> AddUmbrellaDataverseFileStorageProvider<TFileProvider, UmbrellaDataverseAnnotationFileStorageProviderOptions>(services, optionsBuilder);
}
