using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// This Middleware is used to populate the values of the scoped instance of <see cref="DbAppTenantSessionContext{TAppTenantKey}"/> on the application's dependency
/// injection container with the value stored in the claims for the current user for the current HTTP request. The name of the claim type is injected into the constructor
/// by the ASP.NET Core infrastructure using the value registered when configuring the <see cref="IApplicationBuilder"/> in Startup.cs.
/// </summary>
/// <typeparam name="TAppTenantKey">The type of the application tenant key.</typeparam>
public class MultiTenantSessionContextMiddleware<TAppTenantKey>
{
	private readonly RequestDelegate _next;
	private readonly ILogger _log;
	private readonly MultiTenantSessionContextMiddlewareOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTenantSessionContextMiddleware{TAppTenantKey}"/> class.
	/// </summary>
	/// <param name="next">The next piece of middleware to be executed.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public MultiTenantSessionContextMiddleware(
		RequestDelegate next,
		ILogger<MultiTenantSessionContextMiddleware<TAppTenantKey>> logger,
		MultiTenantSessionContextMiddlewareOptions options)
	{
		_next = next;
		_log = logger;
		_options = options;
	}

	/// <summary>
	/// Invokes the middleware in the context of the current request. This method is called by the ASP.NET Core infrastructure.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	/// <param name="dbAppAuthSessionContext">The database application authentication session context.</param>
	public async Task InvokeAsync(HttpContext context, DbAppTenantSessionContext<TAppTenantKey> dbAppAuthSessionContext)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(dbAppAuthSessionContext);

		try
		{
			if (context.User.Identity?.IsAuthenticated is true)
			{
				string? strAppTenantId = context.User.Claims.SingleOrDefault(x => x.Type == _options.TenantClaimType)?.Value;

				if (!string.IsNullOrWhiteSpace(strAppTenantId))
				{
					var id = (TAppTenantKey)Convert.ChangeType(strAppTenantId, typeof(TAppTenantKey), CultureInfo.InvariantCulture);

					dbAppAuthSessionContext.AppTenantId = id;
				}

				dbAppAuthSessionContext.IsAuthenticated = true;
			}
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw;
		}

		await _next.Invoke(context);
	}
}

/// <summary>
/// This Middleware is used to populate the values of the scoped instances of <see cref="DbAppTenantSessionContext{TAppTenantKey}"/> and <see cref="DbAppTenantSessionContext{TNullableAppTenantKey}"/>
/// on the application's dependency injection container with the value stored in the claims for the current user for the current HTTP request. The name of the claim type is injected into the constructor
/// by the ASP.NET Core infrastructure using the value registered when configuring the <see cref="IApplicationBuilder"/> in Startup.cs.
/// </summary>
/// <typeparam name="TAppTenantKey">The type of the application tenant key.</typeparam>
/// <typeparam name="TNullableAppTenantKey">The type of the nullable application tenant key.</typeparam>
public class MultiTenantSessionContextMiddleware<TAppTenantKey, TNullableAppTenantKey>
{
	private readonly RequestDelegate _next;
	private readonly ILogger _log;
	private readonly MultiTenantSessionContextMiddlewareOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="MultiTenantSessionContextMiddleware{TAppTenantKey, TNullableAppTenantKey}"/> class.
	/// </summary>
	/// <param name="next">The next piece of middleware to be executed.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public MultiTenantSessionContextMiddleware(
		RequestDelegate next,
		ILogger<MultiTenantSessionContextMiddleware<TAppTenantKey, TNullableAppTenantKey>> logger,
		MultiTenantSessionContextMiddlewareOptions options)
	{
		_next = next;
		_log = logger;
		_options = options;
	}

	/// <summary>
	/// Invokes the middleware in the context of the current request. This method is called by the ASP.NET Core infrastructure.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	/// <param name="dbAppAuthSessionContext">The database application authentication session context.</param>
	/// <param name="dbNullableAppAuthSessionContext">The nullable database application authentication session context.</param>
	public async Task InvokeAsync(HttpContext context, DbAppTenantSessionContext<TAppTenantKey> dbAppAuthSessionContext, DbAppTenantSessionContext<TNullableAppTenantKey> dbNullableAppAuthSessionContext)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(dbAppAuthSessionContext);
		ArgumentNullException.ThrowIfNull(dbNullableAppAuthSessionContext);

		try
		{
			if (context.User.Identity?.IsAuthenticated is true)
			{
				string? strAppTenantId = context.User.Claims.SingleOrDefault(x => x.Type == _options.TenantClaimType)?.Value;

				if (!string.IsNullOrWhiteSpace(strAppTenantId))
				{
					var id = (TAppTenantKey)Convert.ChangeType(strAppTenantId, typeof(TAppTenantKey), CultureInfo.InvariantCulture);

					dbAppAuthSessionContext.AppTenantId = id;

					if (!id.Equals(default))
						dbNullableAppAuthSessionContext.AppTenantId = (dynamic)id;
				}

				dbAppAuthSessionContext.IsAuthenticated = true;
				dbNullableAppAuthSessionContext.IsAuthenticated = true;
			}

			await _next.Invoke(context);
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw;
		}
	}
}
