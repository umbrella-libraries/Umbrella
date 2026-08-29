using System.Globalization;
using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.AspNetCore.WebUtilities.Extensions;
using Umbrella.AspNetCore.WebUtilities.Middleware;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Helpers;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extentions methods for the <see cref="IApplicationBuilder" /> type.
/// These methods will usually be called when configuring the middleware pipeline in Startup.cs.
/// </summary>
public static class IApplicationBuilderExtensions
{
	/// <summary>
	/// Adds the <see cref="QueryStringParameterToHttpHeaderMiddleware" /> to the pipeline.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaQueryStringParameterToHttpHeader(this IApplicationBuilder builder) => builder.UseMiddleware<QueryStringParameterToHttpHeaderMiddleware>();

	/// <summary>
	/// Adds the <see cref="InternetExplorerCacheHeadersMiddleware" /> to the pipeline.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaInternetExplorerCacheHeaders(this IApplicationBuilder builder) => builder.UseMiddleware<InternetExplorerCacheHeadersMiddleware>();

	/// <summary>
	/// Adds the <see cref="MultiTenantSessionContextMiddleware{TAppTenantKey}"/> to the pipeline.
	/// </summary>
	/// <typeparam name="TAppTenantKey">The type of the application tenant key.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaMultiTenantSessionContext<TAppTenantKey>(this IApplicationBuilder builder) => builder.UseMiddleware<MultiTenantSessionContextMiddleware<TAppTenantKey>>();

	/// <summary>
	/// Adds the <see cref="MultiTenantSessionContextMiddleware{TAppTenantKey, TNullableAppTenantKey}"/> to the pipeline.
	/// </summary>
	/// <typeparam name="TAppTenantKey">The type of the application tenant key.</typeparam>
	/// <typeparam name="TNullableAppTenantKey">The type of the nullable application tenant key.</typeparam>
	/// <param name="builder">The builder.</param>
	/// <returns>The application builder.</returns>
	/// <remarks>
	/// The ability to allow a <typeparamref name="TNullableAppTenantKey"/> to be specified allows for a <see cref="DbAppTenantSessionContext{TNullableAppTenantKey}"/>
	/// to be accessed in a context where the current user may or may not be associated with a specific tenant. For example, application users will be stored in a database
	/// and each user will normally be associated with a tenant. However, the same database table may contain users not associated to a single tenant, e.g. system administrators,
	/// and those users will need to perform actions outside the context of a single tenant. The provision of a nullable key allows for this under these specialized circumstances.
	/// </remarks>
	public static IApplicationBuilder UseUmbrellaMultiTenantSessionContext<TAppTenantKey, TNullableAppTenantKey>(this IApplicationBuilder builder) => builder.UseMiddleware<MultiTenantSessionContextMiddleware<TAppTenantKey, TNullableAppTenantKey>>();

	/// <summary>
	/// Adds the <see cref="ApiExceptionMiddleware"/> to the pipeline for all requests
	/// that have the specified <paramref name="pathPrefix"/>.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="pathPrefix">The path prefix for API endpoints.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaApiException(this IApplicationBuilder builder, string pathPrefix = "/api") => builder.UseWhen(x => x.Request.Path.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase), app => app.UseMiddleware<ApiExceptionMiddleware>());

	/// <summary>
	/// Adds the <see cref="FileAccessTokenQueryStringMiddleware"/> to the pipeline for all requests
	/// that have a specified QueryString parameter with name <see cref="AppQueryStringKeys.FileAccessToken"/>.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseUmbrellaFileAccessTokenQueryString(this IApplicationBuilder builder) => builder.UseWhen(ctx => ctx.Request.Query.ContainsKey(AppQueryStringKeys.FileAccessToken), app => app.UseMiddleware<FileAccessTokenQueryStringMiddleware>());

	/// <summary>
	/// Ensures that the <see cref="IHostEnvironment.ContentRootPath"/>,
	/// <see cref="IHostEnvironment.ContentRootFileProvider"/>
	/// and <see cref="IWebHostEnvironment.WebRootPath"/> have been set correctly
	/// when developing a Blazor application hosted using ASP.NET Core where the client project contains the primary wwwroot folder.
	/// </summary>
	/// <remarks>
	/// <c>This is only required for older projects where static assets are hosted in the wwwroot folder in the client project.</c>
	/// </remarks>
	/// <param name="app">The application.</param>
	/// <param name="clientProjectSuffix">The client project suffix.</param>
	/// <param name="serverProjectSuffix">The server project suffix.</param>
	/// <returns>The <see cref="WebApplication"/> instance.</returns>
	[Obsolete("Please do not use this in new projects; only legacy projects that were created in the .NET Core 3.1 days.")]
	public static WebApplication EnsureBlazorDevelopmentPaths(this WebApplication app, string clientProjectSuffix = "Client", string serverProjectSuffix = "Server")
	{
		Guard.IsNotNull(app);

		if (app.Environment.ContentRootPath.LastIndexOf(clientProjectSuffix, StringComparison.InvariantCulture) > 0)
		{
			string contentRootPath = $@"{app.Environment.ContentRootPath[..app.Environment.ContentRootPath.LastIndexOf(clientProjectSuffix, StringComparison.InvariantCulture)]}{serverProjectSuffix}";
			app.Environment.ContentRootPath = PathHelper.PlatformNormalize(contentRootPath);
			app.Environment.ContentRootFileProvider = new PhysicalFileProvider(app.Environment.ContentRootPath);
		}

		if (string.IsNullOrEmpty(app.Environment.WebRootPath) || app.Environment.WebRootPath.LastIndexOf(serverProjectSuffix, StringComparison.InvariantCulture) > 0)
		{
			string webRootPath = $@"{app.Environment.ContentRootPath[..app.Environment.ContentRootPath.LastIndexOf(serverProjectSuffix, StringComparison.InvariantCulture)]}{clientProjectSuffix}\wwwroot";
			app.Environment.WebRootPath = PathHelper.PlatformNormalize(webRootPath);
		}

		return app;
	}

	/// <summary>
	/// Branches the request pipeline to a terminal state, resulting in a 400 Bad Request, if the host domain being requested is for an <c>azurewebsites.net</c> domain
	/// and the request path is not listed in the <paramref name="pathExceptions"/>.
	/// </summary>
	/// <param name="builder">The application builder.</param>
	/// <param name="pathExceptions">The list of path exceptions.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseBlockAllAzureWebsitesNetDomainTraffic(this IApplicationBuilder builder, params string[] pathExceptions)
		=> builder.MapWhen(x => !pathExceptions.Contains(x.Request.Path.Value, StringComparer.OrdinalIgnoreCase) && x.Request.Host.Host.EndsWith("azurewebsites.net", StringComparison.OrdinalIgnoreCase), app => app.Use((HttpContext context, Func<Task> next) =>
		{
			context.Response.SendStatusCode(HttpStatusCode.BadRequest);

			return Task.CompletedTask;
		}));

	/// <summary>
	/// Adds the <see cref="LinkHeaderMiddleware"/> to the pipeline.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
	public static IApplicationBuilder UseUmbrellaLinkHeader(this IApplicationBuilder builder) => builder.UseMiddleware<LinkHeaderMiddleware>();

	/// <summary>
	/// Adds middleware to the pipeline that will set the current culture based on the Accept-Language header for all requests that have the specified <paramref name="pathPrefix"/>
	/// using the specified <paramref name="fallbackLanguageCode"/> if no Accept-Language header is present.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="pathPrefix">The path prefix for API endpoints.</param>
	/// <param name="fallbackLanguageCode">The fallback language code.</param>
	/// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
	[Obsolete("Use the Request Localization Middleware in ASP.NET")]
	public static IApplicationBuilder UseUmbrellaApiAcceptLanguage(this IApplicationBuilder builder, string pathPrefix = "/api", string fallbackLanguageCode = "en")
	{
		return builder.UseWhen(x => x.Request.Path.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase), app => app.Use((context, next) =>
		{
			var lstAcceptLanguages = context.Request.Headers.GetOrderedAcceptLanguages();

			string languageCode = lstAcceptLanguages.FirstOrDefault() ?? fallbackLanguageCode;

			var cultureInfo = CultureInfo.GetCultureInfo(languageCode);

			CultureInfo.CurrentCulture = cultureInfo;
			CultureInfo.CurrentUICulture = cultureInfo;

			return next(context);
		}));
	}

	/// <summary>
	/// Adds middleware to the pipeline that will set the Content-Language response header based on the current culture with a value of either the two-letter ISO language name or the full culture name
	/// depending on the value of <paramref name="useTwoLetterISOLanguageName"/> for all requests.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="useTwoLetterISOLanguageName">Set to <see langword="true" /> to use the two-letter ISO language name; otherwise the full culture name will be used.</param>
	/// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
	[Obsolete("Use the Request Localization Middleware in ASP.NET")]
	public static IApplicationBuilder UseUmbrellaContentLanguage(this IApplicationBuilder builder, bool useTwoLetterISOLanguageName = true)
	{
		return builder.Use((context, next) =>
		{
			context.Response.OnStarting(() =>
			{
				string value = useTwoLetterISOLanguageName
					? CultureInfo.CurrentCulture.TwoLetterISOLanguageName
					: CultureInfo.CurrentCulture.Name;

				context.Response.Headers.ContentLanguage = value;

				return Task.CompletedTask;
			});

			return next(context);
		});
	}

	/// <summary>
	/// Ensures that the <see cref="ClaimsPrincipal"/> on the current <see cref="HttpContext"/> is propagated to <see cref="Thread.CurrentPrincipal"/>.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
	/// <remarks>This needs to be done manually and is not done automatically by ASP.NET Core.</remarks>
	public static IApplicationBuilder UseUmbrellaPropagateClaimsPrincipal(this IApplicationBuilder builder) => builder.Use((context, next) =>
	{
		Thread.CurrentPrincipal = context.User;

		return next(context);
	});
}
