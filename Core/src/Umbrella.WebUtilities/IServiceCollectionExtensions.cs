using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Umbrella.WebUtilities.Bundling;
using Umbrella.WebUtilities.Bundling.Abstractions;
using Umbrella.WebUtilities.Bundling.Options;
using Umbrella.WebUtilities.Hosting.Options;
using Umbrella.WebUtilities.Http;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;
using Umbrella.WebUtilities.Middleware.Options.LinkHeader;
using Umbrella.WebUtilities.Security;
using Umbrella.WebUtilities.Versioning;
using Umbrella.WebUtilities.Versioning.Abstractions;
using Umbrella.WebUtilities.Versioning.Options;

[assembly: InternalsVisibleTo("Umbrella.WebUtilities.Test")]

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods used to register services for the <see cref="Umbrella.WebUtilities"/> package with a specified
/// <see cref="IServiceCollection"/> dependency injection container builder.
/// </summary>
public static class IServiceCollectionExtensions
{
	/// <summary>
	/// Adds the <see cref="Umbrella.WebUtilities"/> services to the specified <see cref="IServiceCollection"/> dependency injection container builder.
	/// </summary>
	/// <param name="services">The services dependency injection container builder to which the services will be added.</param>
	/// <param name="bundleUtilityOptionsBuilder">The optional <see cref="BundleUtilityOptions"/> builder.</param>
	/// <param name="frontEndCompressionMiddlewareOptionsBuilder">The optional <see cref="FrontEndCompressionMiddlewareOptions"/> builder.</param>
	/// <param name="webpackBundleUtilityOptionsBuilder">The optional <see cref="WebpackBundleUtilityOptions"/> builder.</param>
	/// <param name="internetExplorerCacheHeaderMiddlewareOptionsBuilder">The optional <see cref="InternetExplorerCacheHeadersMiddlewareOptions"/> builder.</param>
	/// <param name="umbrellaWebHostingEnvironmentOptionsBuilder">The optional <see cref="UmbrellaWebHostingEnvironmentOptions"/> builder.</param>
	/// <param name="queryStringParameterToHttpHeaderMiddlewareOptionsBuilder">The optional <see cref="QueryStringParameterToHttpHeaderMiddlewareOptions"/> builder.</param>
	/// <param name="multiTenantSessionContextMiddlewareOptionsBuilder">The optional <see cref="MultiTenantSessionContextMiddlewareOptions"/> builder.</param>
	/// <param name="systemVersionServiceOptionsBuilder">The optional <see cref="SystemVersionServiceOptions"/> builder.</param>
	/// <param name="linkHeaderMiddlewareOptionsBuilder">The optional <see cref="LinkHeaderMiddlewareOptions"/> builder.</param>
	/// <param name="isDevelopmentMode">Specifies if the current application is running in development mode.</param>
	/// <returns>The <see cref="IServiceCollection"/> dependency injection container builder.</returns>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="services"/> is null.</exception>
	public static IServiceCollection AddUmbrellaWebUtilities(
		this IServiceCollection services,
		Action<IServiceProvider, BundleUtilityOptions>? bundleUtilityOptionsBuilder = null,
		Action<IServiceProvider, FrontEndCompressionMiddlewareOptions>? frontEndCompressionMiddlewareOptionsBuilder = null,
		Action<IServiceProvider, WebpackBundleUtilityOptions>? webpackBundleUtilityOptionsBuilder = null,
		Action<IServiceProvider, InternetExplorerCacheHeadersMiddlewareOptions>? internetExplorerCacheHeaderMiddlewareOptionsBuilder = null,
		Action<IServiceProvider, UmbrellaWebHostingEnvironmentOptions>? umbrellaWebHostingEnvironmentOptionsBuilder = null,
		Action<IServiceProvider, QueryStringParameterToHttpHeaderMiddlewareOptions>? queryStringParameterToHttpHeaderMiddlewareOptionsBuilder = null,
		Action<IServiceProvider, MultiTenantSessionContextMiddlewareOptions>? multiTenantSessionContextMiddlewareOptionsBuilder = null,
		Action<IServiceProvider, SystemVersionServiceOptions>? systemVersionServiceOptionsBuilder = null,
		Action<IServiceProvider, LinkHeaderMiddlewareOptions>? linkHeaderMiddlewareOptionsBuilder = null,
		bool isDevelopmentMode = false)
	{
		Guard.IsNotNull(services);

		_ = services.AddSingleton<IBundleUtility, BundleUtility>();
		_ = services.AddSingleton<IHttpHeaderValueUtility, HttpHeaderValueUtility>();
		_ = services.AddSingleton<IWebpackBundleUtility, WebpackBundleUtility>();
		_ = services.AddSingleton<ISystemVersionService, SystemVersionService>();

		_ = services.AddScoped<NonceContext>();

		// Options
		_ = services.ConfigureUmbrellaOptions(bundleUtilityOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(frontEndCompressionMiddlewareOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(webpackBundleUtilityOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(internetExplorerCacheHeaderMiddlewareOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(umbrellaWebHostingEnvironmentOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(queryStringParameterToHttpHeaderMiddlewareOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(multiTenantSessionContextMiddlewareOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(systemVersionServiceOptionsBuilder, isDevelopmentMode);
		_ = services.ConfigureUmbrellaOptions(linkHeaderMiddlewareOptionsBuilder, isDevelopmentMode);

		return services;
	}
}