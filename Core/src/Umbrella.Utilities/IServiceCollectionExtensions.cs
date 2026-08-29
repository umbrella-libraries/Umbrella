using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Data;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.DataAnnotations;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Options;
using Umbrella.Utilities.Dating;
using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.DependencyInjection;
using Umbrella.Utilities.Email;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Encryption;
using Umbrella.Utilities.Encryption.Abstractions;
using Umbrella.Utilities.Encryption.Options;
using Umbrella.Utilities.FriendlyUrl;
using Umbrella.Utilities.FriendlyUrl.Abstractions;
using Umbrella.Utilities.Hosting;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.Utilities.Hosting.Options;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Extensions;
using Umbrella.Utilities.Http.Handlers;
using Umbrella.Utilities.Http.Options;
using Umbrella.Utilities.Imaging;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.Utilities.Mapping;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mime;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.Numerics;
using Umbrella.Utilities.Numerics.Abstractions;
using Umbrella.Utilities.Options;
using Umbrella.Utilities.Options.Abstractions;
using Umbrella.Utilities.Options.Exceptions;
using Umbrella.Utilities.Security;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Spatial;
using Umbrella.Utilities.Spatial.Abstractions;
using Umbrella.Utilities.Threading;
using Umbrella.Utilities.Threading.Abstractions;
using Umbrella.Utilities.TypeConverters;
using Umbrella.Utilities.TypeConverters.Abstractions;

[assembly: InternalsVisibleTo("Umbrella.AspNetCore.WebUtilities")]
//[assembly: InternalsVisibleTo("Umbrella.AspNetCore.Blazor")]
//[assembly: InternalsVisibleTo("Umbrella.AspNetCore.DynamicImage")]
//[assembly: InternalsVisibleTo("Umbrella.DataAccess.Abstractions")]
[assembly: InternalsVisibleTo("Umbrella.DynamicImage.Caching.AzureStorage.Test")]
[assembly: InternalsVisibleTo("Umbrella.DynamicImage.Impl.Test")]
[assembly: InternalsVisibleTo("Umbrella.DynamicImage.Test")]
[assembly: InternalsVisibleTo("Umbrella.FileSystem.Test")]
[assembly: InternalsVisibleTo("Umbrella.Utilities.Benchmark")]
[assembly: InternalsVisibleTo("Umbrella.Utilities.Test")]

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.Utilities"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// Extension methods are also provided to allow for registrations to be removed and replaced.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.Utilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="emailFactoryOptionsBuilder">The optional <see cref="EmailFactoryOptions"/> builder.</param>
	/// <param name="emailSenderOptionsBuilder">The optional <see cref="EmailSenderOptions"/> builder.</param>
	/// <param name="httpResourceInfoUtilityOptionsBuilder">The optional <see cref="HttpResourceInfoUtilityOptions"/> builder.</param>
	/// <param name="secureRandomStringGeneratorOptionsBuilder">The optional <see cref="SecureRandomStringGeneratorOptions"/> builder.</param>
	/// <param name="umbrellaConsoleHostingEnvironmentOptionsBuilder">The optional <see cref="UmbrellaHostingEnvironmentOptions"/> builder.</param>
	/// <param name="objectGraphValidatorOptionsBuilder">The optional <see cref="ObjectGraphValidatorOptions"/> builder.</param>
	/// <param name="httpServicesBuilder">The optional builder for all Http Services.</param>
	/// <param name="httpServicesDefaultTimeOutSeconds">The default timeout in seconds.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	/// <remarks>On target frameworks below .NET 9, applications must register an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> implementation. On .NET 9 and later, this method registers Microsoft Hybrid Cache.</remarks>
	public static IServiceCollection AddUmbrellaUtilities(
		this IServiceCollection services,
		Action<IServiceProvider, EmailFactoryOptions>? emailFactoryOptionsBuilder = null,
		Action<IServiceProvider, EmailSenderOptions>? emailSenderOptionsBuilder = null,
		Action<IServiceProvider, HttpResourceInfoUtilityOptions>? httpResourceInfoUtilityOptionsBuilder = null,
		Action<IServiceProvider, SecureRandomStringGeneratorOptions>? secureRandomStringGeneratorOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaConsoleHostingEnvironmentOptions>? umbrellaConsoleHostingEnvironmentOptionsBuilder = null,
		Action<IServiceProvider, ObjectGraphValidatorOptions>? objectGraphValidatorOptionsBuilder = null,
		Action<Dictionary<Type, IHttpClientBuilder>>? httpServicesBuilder = null,
		int httpServicesDefaultTimeOutSeconds = 20)
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.AddSingleton<ICacheKeyUtility, CacheKeyUtility>();
		_ = services.AddSingleton<ICertificateUtility, CertificateUtility>();
		_ = services.AddSingleton<IConcurrentRandomGenerator, ConcurrentRandomGenerator>();
		_ = services.AddSingleton<IEmailFactory, EmailFactory>();
		_ = services.AddSingleton<IEmailSender, EmailSender>();
		_ = services.AddSingleton<IFriendlyUrlGenerator, FriendlyUrlGenerator>();
		_ = services.AddSingleton<IGenericTypeConverter, GenericTypeConverter>();
#if NET9_0_OR_GREATER
		_ = services.AddHybridCache();
#endif
		_ = services.AddSingleton<IDataLookupNormalizer, UpperInvariantLookupNormalizer>();
		_ = services.AddSingleton<IMimeTypeUtility, MimeTypeUtility>();
		_ = services.AddSingleton<INonceGenerator, NonceGenerator>();
		_ = services.AddSingleton<ISecureRandomStringGenerator, SecureRandomStringGenerator>();
		_ = services.AddTransient(typeof(Lazy<>), typeof(LazyProxy<>));
		_ = services.AddSingleton<IUmbrellaHostingEnvironment, UmbrellaConsoleHostingEnvironment>();
		_ = services.AddSingleton<IObjectGraphValidator, ObjectGraphValidator>();
		_ = services.AddSingleton<IUmbrellaValidator, UmbrellaValidator>();
		_ = services.AddSingleton<IDataExpressionFactory, DataExpressionFactory>();
		_ = services.AddSingleton<IJwtUtility, JwtUtility>();
		_ = services.AddSingleton<IGenericHttpServiceUtility, GenericHttpServiceUtility>();
		_ = services.AddSingleton<IResponsiveImageHelper, ResponsiveImageHelper>();
		_ = services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
		_ = services.AddSingleton<IOptionsInitializer, OptionsInitializer>();

		_ = services.AddSingleton<ISynchronizationManager, MemorySynchronizationManager>();
		_ = services.AddSingleton<IUmbrellaMapper, UmbrellaNoopMapper>();

		// HTTP Delegating Handlers
		_ = services.AddScoped<AcceptCurrentCultureLanguageHandler>();

		if (httpServicesBuilder is not null)
		{
			var dict = new Dictionary<Type, IHttpClientBuilder>
			{
				[typeof(IGenericHttpService)] = services.AddHttpClient<IGenericHttpService, GenericHttpService>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds)),
				[typeof(IHttpResourceInfoUtility)] = services.AddHttpClient<IHttpResourceInfoUtility, HttpResourceInfoUtility>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds)),
				[typeof(IGeocodingService)] = services.AddHttpClient<IGeocodingService, PostcodesIOGeocodingService>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds))
			};

			httpServicesBuilder(dict);
		}
		else
		{
			_ = services.AddHttpClient<IGenericHttpService, GenericHttpService>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds)).AddUmbrellaPolicyHandlers();
			_ = services.AddHttpClient<IHttpResourceInfoUtility, HttpResourceInfoUtility>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds)).AddUmbrellaPolicyHandlers();
			_ = services.AddHttpClient<IGeocodingService, PostcodesIOGeocodingService>().ConfigureHttpClient(x => x.Timeout = TimeSpan.FromSeconds(httpServicesDefaultTimeOutSeconds)).AddUmbrellaPolicyHandlers();
		}

		// Options
		_ = services.ConfigureUmbrellaOptions(emailFactoryOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(emailSenderOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(httpResourceInfoUtilityOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(secureRandomStringGeneratorOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(umbrellaConsoleHostingEnvironmentOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(objectGraphValidatorOptionsBuilder);

		return services;
	}

	/// <summary>
	/// Configures the specified Umbrella Options denoted by <typeparamref name="TOptions"/>.
	/// </summary>
	/// <typeparam name="TOptions">The type of the options.</typeparam>
	/// <param name="services">The services.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <param name="isDevelopmentMode">Specifies if the current application is running in development mode.</param>
	/// <returns>
	/// The same instance of <see cref="IServiceCollection"/> as passed in but with the Umbrella Options type specified by
	/// <typeparamref name="TOptions"/> added to it.
	/// </returns>
	public static IServiceCollection ConfigureUmbrellaOptions<TOptions>(this IServiceCollection services, Action<IServiceProvider, TOptions>? optionsBuilder, bool isDevelopmentMode = false)
		where TOptions : class, new()
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.ReplaceSingleton(serviceProvider =>
		{
			try
			{
				var options = new TOptions();

				optionsBuilder?.Invoke(serviceProvider, options);

				var optionsInitializer = serviceProvider.GetRequiredService<IOptionsInitializer>();

				optionsInitializer.Initialize(options, services, isDevelopmentMode);

				return options;
			}
			catch (Exception exc)
			{
				throw new UmbrellaOptionsException("An error has occurred during options configuration.", exc);
			}
		});

		return services;
	}

	// TODO: Really need have an encryption utility options class with properties for key and iv and register with DI
	// It's a breaking change though so leave until v3.
	//public static IServiceCollection AddUmbrellaEncryptionUtility<T>(this IServiceCollection services, string encryptionKey, string iv)
	//	where T : class, IEncryptionUtility
	//{
	//	services.AddSingleton<IEncryptionUtility, T>

	//	return services;
	//}

	/// <summary>
	/// Replaces the transient service registration with the new service.
	/// </summary>
	/// <typeparam name="TService">The type of the service.</typeparam>
	/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
	/// <param name="services">The services.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
		=> services.Remove<TService>().AddTransient<TService, TImplementation>();

	/// <summary>
	/// Replaces the transient service registration with the new service.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="serviceType">Type of the service.</param>
	/// <param name="implementationType">Type of the implementation.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceTransient(this IServiceCollection services, Type serviceType, Type implementationType)
		=> services.Remove(serviceType).AddTransient(serviceType, implementationType);

	/// <summary>
	/// Replaces the scoped service registration with the new service.
	/// </summary>
	/// <typeparam name="TService">The type of the service.</typeparam>
	/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
	/// <param name="services">The services.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
		=> services.Remove<TService>().AddScoped<TService, TImplementation>();

	/// <summary>
	/// Replaces the scoped service registration with the new service.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="serviceType">Type of the service.</param>
	/// <param name="implementationType">Type of the implementation.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceScoped(this IServiceCollection services, Type serviceType, Type implementationType)
		=> services.Remove(serviceType).AddScoped(serviceType, implementationType);

	/// <summary>
	/// Replaces the singleton service registration with the new service.
	/// </summary>
	/// <typeparam name="TService">The type of the service.</typeparam>
	/// <typeparam name="TImplementation">The type of the implementation.</typeparam>
	/// <param name="services">The services.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
		=> services.Remove<TService>().AddSingleton<TService, TImplementation>();

	/// <summary>
	/// Replaces the singleton service registration with the new service.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="serviceType">Type of the service.</param>
	/// <param name="implementationType">Type of the implementation.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceSingleton(this IServiceCollection services, Type serviceType, Type implementationType)
		=> services.Remove(serviceType).AddSingleton(serviceType, implementationType);

	/// <summary>
	/// Replaces the singleton service registration with the new service.
	/// </summary>
	/// <typeparam name="TService">The type of the service.</typeparam>
	/// <param name="services">The services.</param>
	/// <param name="implementationFactory">The implementation factory.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
		where TService : class
		=> services.Remove<TService>().AddSingleton(implementationFactory);

	/// <summary>
	/// Removes all occurrences of the specified <typeparamref name="TService"/>.
	/// </summary>
	/// <typeparam name="TService">The type of the service.</typeparam>
	/// <param name="services">The services.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection Remove<TService>(this IServiceCollection services)
		=> services.Remove(typeof(TService));

	/// <summary>
	/// Removes all occurrences of the specified service type.
	/// </summary>
	/// <param name="services">The services.</param>
	/// <param name="serviceType">Type of the service.</param>
	/// <returns>The services.</returns>
	public static IServiceCollection Remove(this IServiceCollection services, Type serviceType)
	{
		Guard.IsNotNull(services, nameof(services));
		Guard.IsNotNull(serviceType, nameof(serviceType));

		foreach (ServiceDescriptor serviceDescriptor in services.Where(x => x.ServiceType == serviceType).ToArray())
		{
			_ = services.Remove(serviceDescriptor);
		}

		return services;
	}
}
