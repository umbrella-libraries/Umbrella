


using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.AspNetCore.WebUtilities.Middleware.Options;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// Middleware that intercepts requests that contain a QueryString parameter with
/// a name of <see cref="AppQueryStringKeys.FileAccessToken"/> and uses this to
/// create a <see cref="ClaimsPrincipal"/> if the value of the token is valid.
/// </summary>
public class FileAccessTokenQueryStringMiddleware
{
	private readonly ILogger _log;
	private readonly RequestDelegate _next;
	private readonly FileAccessTokenQueryStringMiddlewareOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileAccessTokenQueryStringMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="next">The next.</param>
	/// <param name="options">The options.</param>
	public FileAccessTokenQueryStringMiddleware(
		ILogger<FileAccessTokenQueryStringMiddleware> logger,
		RequestDelegate next,
		FileAccessTokenQueryStringMiddlewareOptions options)
	{
		_log = logger;
		_next = next;
		_options = options;
	}

	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	public async Task InvokeAsync(HttpContext context)
	{
		Guard.IsNotNull(context);

		try
		{
			string? jwt = context.Request.Query[AppQueryStringKeys.FileAccessToken];

			if(string.IsNullOrEmpty(jwt))
			{
				await _next(context);

				return;
			}

			JsonWebTokenHandler jwtHandler = new();

			TokenValidationResult validationResult = await jwtHandler.ValidateTokenAsync(jwt, _options.ValidationParameters);

			if (validationResult.IsValid)
				context.User = new ClaimsPrincipal(validationResult.ClaimsIdentity);
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw;
		}

		await _next(context);
	}
}