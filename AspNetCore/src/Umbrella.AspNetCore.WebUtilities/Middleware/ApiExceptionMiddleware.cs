
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// Middleware that intercepts exceptions raised by API controllers and returns a friendly message
/// when not running in development enivironments.
/// </summary>
public class ApiExceptionMiddleware
{
	private readonly ILogger _log;
	private readonly IHostEnvironment _hostEnvironment;
	private readonly RequestDelegate _next;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiExceptionMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostEnvironment">The host environment.</param>
	/// <param name="next">The next middleware in the pipeline.</param>
	public ApiExceptionMiddleware(
		ILogger<ApiExceptionMiddleware> logger,
		IHostEnvironment hostEnvironment,
		RequestDelegate next)
	{
		_log = logger;
		_hostEnvironment = hostEnvironment;
		_next = next;
	}

	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	public async Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		try
		{
			await _next(context);
		}
		catch (Exception exc) when (_log.WriteError(exc, returnValue: !_hostEnvironment.IsDevelopment()))
		{
			// When in production, we need to return a friendly plain error message and ensure we don't see a HTML response.
			await context.Response.WriteAsync("An error occurred while processing your request.", context.RequestAborted);
			await context.Response.Body.FlushAsync(context.RequestAborted);
			await context.Response.CompleteAsync();
		}
	}
}
