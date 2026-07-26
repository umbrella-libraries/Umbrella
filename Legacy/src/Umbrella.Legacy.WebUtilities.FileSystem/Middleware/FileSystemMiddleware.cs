using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Legacy.WebUtilities.Extensions;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.FileSystem.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.Legacy.WebUtilities.FileSystem.Middleware;

/// <summary>
/// OWIN Middleware that is used to access files stored in a physical or virtual file system. Underling file access
/// is provided using the <see cref="Umbrella.FileSystem"/> infrastructure.
/// </summary>
/// <seealso cref="OwinMiddleware" />
public class FileSystemMiddleware : OwinMiddleware
{
	private readonly ILogger _log;
	private readonly IHttpHeaderValueUtility _httpHeaderValueUtility;
	private readonly FileSystemMiddlewareOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileSystemMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next middleware.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="httpHeaderValueUtility">The HTTP header value utility.</param>
	/// <param name="options">The options.</param>
	public FileSystemMiddleware(
		OwinMiddleware next,
		ILogger<FileSystemMiddleware> logger,
		IHttpHeaderValueUtility httpHeaderValueUtility,
		FileSystemMiddlewareOptions options)
		: base(next)
	{
		_log = logger;
		_httpHeaderValueUtility = httpHeaderValueUtility;
		_options = options;
	}

	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current <see cref="IOwinContext"/>.</param>
	public override async Task Invoke(IOwinContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.Request.CallCancelled.ThrowIfCancellationRequested();

		try
		{
			string path = context.Request.Path.Value;

			if (!path.StartsWith("/" + _options.FileSystemPathPrefix, StringComparison.OrdinalIgnoreCase))
			{
				await Next.Invoke(context);
				return;
			}

			// Strip the prefix
			path = path.Substring(_options.FileSystemPathPrefix.Length + 1);

			FileSystemMiddlewareMapping? mapping = _options.GetMapping(path);

			if (mapping is not null)
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Request.CallCancelled);
				CancellationToken token = cts.Token;

				IUmbrellaFileInfo? fileInfo = await mapping.FileProviderMapping.FileProvider.GetAsync(path, token);

				if (fileInfo is null)
				{
					cts.Cancel();
					context.Response.SendStatusCode(HttpStatusCode.NotFound);

					return;
				}

				string? eTagValue = null;

				// Check the cache headers
				if (fileInfo.LastModified.HasValue)
				{
					if (context.Request.IfModifiedSinceHeaderMatched(fileInfo.LastModified.Value.UtcDateTime))
					{
						cts.Cancel();
						context.Response.SendStatusCode(HttpStatusCode.NotModified);

						return;
					}

					eTagValue = _httpHeaderValueUtility.CreateETagHeaderValue(fileInfo.LastModified.Value, fileInfo.Length);

					if (context.Request.IfNoneMatchHeaderMatched(eTagValue))
					{
						cts.Cancel();
						context.Response.SendStatusCode(HttpStatusCode.NotModified);

						return;
					}
				}

				context.Response.ContentType = fileInfo.ContentType;

				if (mapping.Cacheability == MiddlewareHttpCacheability.NoCache && fileInfo.LastModified.HasValue)
				{
					eTagValue ??= _httpHeaderValueUtility.CreateETagHeaderValue(fileInfo.LastModified.Value, fileInfo.Length);

					context.Response.Headers["Last-Modified"] = _httpHeaderValueUtility.CreateLastModifiedHeaderValue(fileInfo.LastModified.Value);
					context.Response.Headers["ETag"] = eTagValue;
					context.Response.Headers["Cache-Control"] = "no-cache";
				}
				else
				{
					context.Response.Headers["Cache-Control"] = "no-store";
				}

				await fileInfo.WriteToStreamAsync(context.Response.Body, cancellationToken: token);
				await context.Response.Body.FlushAsync();

				return;
			}

			await Next.Invoke(context);
		}
		catch (UmbrellaFileAccessDeniedException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound so that any potential attacker isn't even aware the file exists.
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { Path = context.Request.Path.Value }))
		{
			throw new UmbrellaWebException("An error has occurred whilst executing the request.", exc);
		}
	}
}
