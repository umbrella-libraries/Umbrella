


using Microsoft.Extensions.DependencyInjection;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A factory used to create instances of Umbrella File Storage Providers.
/// </summary>
public interface IUmbrellaFileStorageProviderFactory
{
	/// <summary>
	/// Creates a provider of the specified type using the specified options.
	/// </summary>
	/// <typeparam name="TProvider">The type of the provider.</typeparam>
	/// <typeparam name="TOptions">The type of the options.</typeparam>
	/// <param name="options">The options.</param>
	/// <param name="services">The services.</param>
	/// <returns>The file provider.</returns>
	TProvider CreateProvider<TProvider, TOptions>(TOptions options, IServiceCollection services)
		where TProvider : IUmbrellaFileStorageProvider
		where TOptions : UmbrellaFileStorageProviderOptionsBase;
}