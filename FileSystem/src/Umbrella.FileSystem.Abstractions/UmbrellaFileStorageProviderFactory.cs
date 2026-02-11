
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A factory used to create instances of Umbrella File Providers.
/// </summary>
/// <seealso cref="IUmbrellaFileStorageProviderFactory" />
public class UmbrellaFileStorageProviderFactory : IUmbrellaFileStorageProviderFactory
{
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger<UmbrellaFileStorageProviderFactory> Log { get; }

	/// <summary>
	/// Gets the MIME type utility.
	/// </summary>
	protected IMimeTypeUtility MimeTypeUtility { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }

	/// <summary>
	/// Gets the DI services container.
	/// </summary>
	protected IServiceProvider ServiceProvider { get; }

	/// <summary>
	/// Gets the options initializer.
	/// </summary>
	public IOptionsInitializer OptionsInitializer { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileStorageProviderFactory"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	/// <param name="serviceProvider">The services.</param>
	/// <param name="optionsInitializer">The options initializer.</param>
	public UmbrellaFileStorageProviderFactory(
		ILogger<UmbrellaFileStorageProviderFactory> logger,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		IServiceProvider serviceProvider,
		IOptionsInitializer optionsInitializer)
	{
		Log = logger;
		MimeTypeUtility = mimeTypeUtility;
		GenericTypeConverter = genericTypeConverter;
		ServiceProvider = serviceProvider;
		OptionsInitializer = optionsInitializer;
	}

	/// <inheritdoc />
	public TProvider CreateProvider<TProvider, TOptions>(TOptions options, IServiceCollection services)
		where TProvider : IUmbrellaFileStorageProvider
		where TOptions : UmbrellaFileStorageProviderOptionsBase
	{
		OptionsInitializer.Initialize(options, services);

		var provider = ActivatorUtilities.CreateInstance<TProvider>(ServiceProvider);
		provider.InitializeOptions(options);

		return provider;
	}
}