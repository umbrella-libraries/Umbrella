
using CommunityToolkit.Diagnostics;
using Umbrella.FileSystem.Abstractions;
using Umbrella.WebUtilities.FileSystem.Middleware.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.WebUtilities.FileSystem"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.WebUtilities.FileSystem"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The optional <see cref="FileSystemMiddlewareOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaWebUtilitiesFileSystem(
		this IServiceCollection services,
		Action<IServiceProvider, FileSystemMiddlewareOptions>? optionsBuilder = null)
	{
		Guard.IsNotNull(services);

		// Options
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		return services;
	}

	/// <summary>
	/// Adds Umbrella Web Utilities file system middleware and related services to the specified service collection.
	/// </summary>
	/// <remarks>This method configures the file system middleware to use the specified path prefix and directory
	/// mappings. It should be called during application startup as part of service registration.</remarks>
	/// <param name="services">The service collection to which the file system middleware and related services will be added. Cannot be null.</param>
	/// <param name="fileSystemPathPrefix">The path prefix to use for file system middleware routing. This value determines the base URL segment for file
	/// system requests.</param>
	/// <param name="directoryNames">A collection of directory names to be mapped for file system access. Each directory name will be included in the
	/// middleware's file provider mapping.</param>
	/// <returns>The same instance of <see cref="IServiceCollection"/> that was provided, to support method chaining.</returns>
	public static IServiceCollection AddUmbrellaWebUtilitiesFileSystem(
		this IServiceCollection services,
		string fileSystemPathPrefix,
		IEnumerable<string> directoryNames)
	{
		Guard.IsNotNull(services);

		// Options
		Action<IServiceProvider, FileSystemMiddlewareOptions> optionsBuilder = (services, options) =>
		{
			options.FileSystemPathPrefix = fileSystemPathPrefix;
			options.Mappings =
			[
				new FileSystemMiddlewareMapping
				{
					FileProviderMapping = new UmbrellaFileStorageProviderMapping(services.GetRequiredService<IUmbrellaFileStorageProvider>(), [.. directoryNames])
				}
			];
		};

		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		return services;
	}
}