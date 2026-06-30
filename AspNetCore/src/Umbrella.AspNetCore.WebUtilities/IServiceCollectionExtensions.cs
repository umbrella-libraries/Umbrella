
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Umbrella.AspNetCore.WebUtilities.Middleware;
using Umbrella.AspNetCore.WebUtilities.Components;
using Umbrella.AspNetCore.WebUtilities.Components.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Components.Options;
using Umbrella.AspNetCore.Shared.Services.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Cookie;
using Umbrella.AspNetCore.WebUtilities.Cookie.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Hosting;
using Umbrella.AspNetCore.WebUtilities.Hosting.Options;
using Umbrella.AspNetCore.WebUtilities.Identity;
using Umbrella.AspNetCore.WebUtilities.Identity.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Identity.Options;
using Umbrella.AspNetCore.WebUtilities.Middleware.Options;
using Umbrella.AspNetCore.WebUtilities.Mvc.Services;
using Umbrella.AspNetCore.WebUtilities.Razor;
using Umbrella.AspNetCore.WebUtilities.Razor.Abstractions;
using Umbrella.AspNetCore.WebUtilities.Razor.Options;
using Umbrella.AspNetCore.WebUtilities.Security;
using Umbrella.AspNetCore.WebUtilities.Security.Options;
using Umbrella.AspNetCore.WebUtilities.Services;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.WebUtilities.Hosting;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.AspNetCore.WebUtilities"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.WebUtilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="apiIntegrationCookieAuthenticationEventsOptionsBuilder">The optional <see cref="ApiIntegrationCookieAuthenticationEventsOptions"/> builder.</param>
	/// <param name="umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder">The optional <see cref="UmbrellaScheduledHostedServiceWithViewSupportOptions"/> builder.</param>
	/// <param name="fileAccessTokenQueryStringMiddlewareOptions">The optional <see cref="FileAccessTokenQueryStringMiddlewareOptions"/> builder.</param>
	/// <param name="razorViewToStringRendererOptionsBuilder">The optional <see cref="RazorViewToStringRendererOptions"/> builder.</param>
	/// <param name="razorComponentToStringRendererOptionsBuilder">The optional <see cref="RazorComponentToStringRendererOptions"/> builder.</param>
	/// <param name="bundleComponentOptionsBuilder">The optional <see cref="BundleComponentOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaAspNetCoreWebUtilities(
		this IServiceCollection services,
		Action<IServiceProvider, ApiIntegrationCookieAuthenticationEventsOptions>? apiIntegrationCookieAuthenticationEventsOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaScheduledHostedServiceWithViewSupportOptions>? umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder = null,
		Action<IServiceProvider, FileAccessTokenQueryStringMiddlewareOptions>? fileAccessTokenQueryStringMiddlewareOptions = null,
		Action<IServiceProvider, RazorViewToStringRendererOptions>? razorViewToStringRendererOptionsBuilder = null,
		Action<IServiceProvider, RazorComponentToStringRendererOptions>? razorComponentToStringRendererOptionsBuilder = null,
		Action<IServiceProvider, BundleComponentOptions>? bundleComponentOptionsBuilder = null)
		=> services.AddUmbrellaAspNetCoreWebUtilities<UmbrellaWebHostingEnvironment>(
			apiIntegrationCookieAuthenticationEventsOptionsBuilder,
			umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder,
			fileAccessTokenQueryStringMiddlewareOptions,
			razorViewToStringRendererOptionsBuilder,
			razorComponentToStringRendererOptionsBuilder,
			bundleComponentOptionsBuilder);

	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.WebUtilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TUmbrellaWebHostingEnvironment">
	/// The concrete implementation of <see cref="IUmbrellaWebHostingEnvironment"/> to register. This allows consuming applications to override the default implementation and allow it to be
	/// resolved from the container correctly for both the <see cref="IUmbrellaHostingEnvironment"/> and <see cref="IUmbrellaWebHostingEnvironment"/> interfaces.
	/// </typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="apiIntegrationCookieAuthenticationEventsOptionsBuilder">The optional <see cref="ApiIntegrationCookieAuthenticationEventsOptions"/> builder.</param>
	/// <param name="umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder">The optional <see cref="UmbrellaScheduledHostedServiceWithViewSupportOptions"/> builder.</param>
	/// <param name="fileAccessTokenQueryStringMiddlewareOptions">The optional <see cref="FileAccessTokenQueryStringMiddlewareOptions"/> builder.</param>
	/// <param name="razorViewToStringRendererOptionsBuilder">The optional <see cref="RazorViewToStringRendererOptions"/> builder.</param>
	/// <param name="razorComponentToStringRendererOptionsBuilder">The optional <see cref="RazorComponentToStringRendererOptions"/> builder.</param>
	/// <param name="bundleComponentOptionsBuilder">The optional <see cref="BundleComponentOptions"/> builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaAspNetCoreWebUtilities<TUmbrellaWebHostingEnvironment>(
		this IServiceCollection services,
		Action<IServiceProvider, ApiIntegrationCookieAuthenticationEventsOptions>? apiIntegrationCookieAuthenticationEventsOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaScheduledHostedServiceWithViewSupportOptions>? umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder = null,
		Action<IServiceProvider, FileAccessTokenQueryStringMiddlewareOptions>? fileAccessTokenQueryStringMiddlewareOptions = null,
		Action<IServiceProvider, RazorViewToStringRendererOptions>? razorViewToStringRendererOptionsBuilder = null,
		Action<IServiceProvider, RazorComponentToStringRendererOptions>? razorComponentToStringRendererOptionsBuilder = null,
		Action<IServiceProvider, BundleComponentOptions>? bundleComponentOptionsBuilder = null)
		where TUmbrellaWebHostingEnvironment : class, IUmbrellaWebHostingEnvironment
	{
		Guard.IsNotNull(services, nameof(services));

		_ = services.AddUmbrellaAspNetCoreShared();

		// Add the hosting environment as a singleton and then ensure the same instance is bound to both interfaces
		_ = services.AddSingleton<TUmbrellaWebHostingEnvironment>();
		_ = services.ReplaceSingleton<IUmbrellaHostingEnvironment>(x => x.GetRequiredService<TUmbrellaWebHostingEnvironment>());
		_ = services.ReplaceSingleton<IUmbrellaWebHostingEnvironment>(x => x.GetRequiredService<TUmbrellaWebHostingEnvironment>());

		_ = services.AddSingleton<ApiIntegrationCookieAuthenticationEvents>();
		_ = services.AddScoped<IRazorComponentToStringRenderer, RazorComponentToStringRenderer>();
		_ = services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();

		_ = services.AddScoped<IHttpContextService, HttpContextService>();
		_ = services.AddScoped<IJsonCookieService, JsonCookieService>();

		// NB: Registering the below as transient because internally they use transient services.
		_ = services.AddTransient<IUmbrellaAuthorizationService, UmbrellaAspNetAuthorizationService>();

		_ = services.ConfigureUmbrellaOptions(apiIntegrationCookieAuthenticationEventsOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(umbrellaScheduledHostedServiceWithViewSupportOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(fileAccessTokenQueryStringMiddlewareOptions);
		_ = services.ConfigureUmbrellaOptions(razorViewToStringRendererOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(razorComponentToStringRendererOptionsBuilder);
		_ = services.ConfigureUmbrellaOptions(bundleComponentOptionsBuilder);

		return services;
	}

	/// <summary>
	/// Adds the implementation of the <see cref="IAnonymousPhoneNumberVerificationCodeGenerator"/> service to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <typeparam name="TUserManager">The type of the user manager.</typeparam>
	/// <typeparam name="TUser">The type of the user.</typeparam>
	/// <typeparam name="TUserKey">The type of the user key.</typeparam>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaAspNetCoreWebUtilitiesAnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey>(
		this IServiceCollection services,
		Action<IServiceProvider, AnonymousPhoneNumberVerificationCodeGeneratorOptions>? optionsBuilder = null)
		where TUser : IdentityUser<TUserKey>, new()
		where TUserManager : UserManager<TUser>
		where TUserKey : IEquatable<TUserKey>
	{
		Guard.IsNotNull(services);

		_ = services.AddScoped<IAnonymousPhoneNumberVerificationCodeGenerator, AnonymousPhoneNumberVerificationCodeGenerator<TUserManager, TUser, TUserKey>>();
		_ = services.ConfigureUmbrellaOptions(optionsBuilder);

		return services;
	}

	/// <summary>
	/// Adds the <see cref="Umbrella.AspNetCore.WebUtilities.Middleware.BrowserLinkNonceMiddleware"/> to the pipeline via an <see cref="IStartupFilter"/>,
	/// registering it as the outermost middleware so it can inject CSP nonces onto BrowserLink and
	/// ASP.NET Core hot-reload script tags injected by development tooling.
	/// This should only be called in Development environments.
	/// </summary>
	/// <param name="services">The services dependency injection container builder.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaBrowserLinkNonce(this IServiceCollection services)
	{
		Guard.IsNotNull(services, nameof(services));

		// Insert at 0 so our startup filter is resolved first and therefore runs outermost in the pipeline.
		// ASP.NET Core applies startup filters in reverse DI registration order, so index 0 = outermost wrapper.
		services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, BrowserLinkNonceStartupFilter>());

		return services;
	}
}
