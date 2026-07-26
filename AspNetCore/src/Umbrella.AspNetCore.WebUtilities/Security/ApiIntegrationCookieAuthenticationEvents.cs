using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Security.Options;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Security;

/// <summary>
/// A customized version of the default <see cref="CookieAuthenticationEvents" /> type with customizations that intercept redirects
/// that occur as a result of 401 and 403 status codes to ensure that API responses return the status codes instead of redirects to login or error pages which is the
/// default behaviour. Non-API requests will fallback to the default redirect behaviour.
/// </summary>
/// <seealso cref="CookieAuthenticationEvents" />
public class ApiIntegrationCookieAuthenticationEvents : CookieAuthenticationEvents
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	protected ApiIntegrationCookieAuthenticationEventsOptions Options { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiIntegrationCookieAuthenticationEvents"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public ApiIntegrationCookieAuthenticationEvents(
		ILogger<ApiIntegrationCookieAuthenticationEvents> logger,
		ApiIntegrationCookieAuthenticationEventsOptions options)
	{
		Logger = logger;
		Options = options;
	}

	/// <inheritdoc />
	public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
	{
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			if (Options.ApiPathPrefixes.Any(x => context.Request.Path.StartsWithSegments(x, StringComparison.OrdinalIgnoreCase)) && context.Response.StatusCode is StatusCodes.Status200OK)
			{
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			}
			else
			{
				context.Response.Redirect(context.RedirectUri);
			}

			return Task.CompletedTask;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { context.Request.Path, context.Response.StatusCode }))
		{
			throw new UmbrellaWebException("There was a problem handing the login redirect.", exc);
		}
	}

	/// <inheritdoc />
	public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
	{
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			if (Options.ApiPathPrefixes.Any(x => context.Request.Path.StartsWithSegments(x, StringComparison.OrdinalIgnoreCase)) && context.Response.StatusCode is StatusCodes.Status200OK)
			{
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
			}
			else
			{
				context.Response.Redirect(context.RedirectUri);
			}

			return Task.CompletedTask;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { context.Request.Path, context.Response.StatusCode }))
		{
			throw new UmbrellaWebException("There was a problem handing the access denied redirect.", exc);
		}
	}
}
