using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Legacy.WebUtilities.Extensions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.Legacy.WebUtilities.DynamicImage.Middleware;

/// <summary>
/// OWIN Middleware that is used to return a dynamically resized version of a source image. The source image and resizing options
/// are determined by parsing the incoming request URL.
/// </summary>
/// <seealso cref="OwinMiddleware" />
// TODO: This has become out of sync with the ASP.NET Core version. Need to update this to match the new version.
public class DynamicImageMiddleware : OwinMiddleware
{
	#region Private Members
	private readonly ILogger _log;
	private readonly IDynamicImageUtility _dynamicImageUtility;
	private readonly IDynamicImageResizer _dynamicImageResizer;
	private readonly IHttpHeaderValueUtility _headerValueUtility;
	private readonly IMimeTypeUtility _mimeTypeUtility;
	private readonly DynamicImageMiddlewareOptions _options;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next middleware.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="dynamicImageUtility">The dynamic image utility.</param>
	/// <param name="dynamicImageResizer">The dynamic image resizer.</param>
	/// <param name="headerValueUtility">The header value utility.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="options">The options.</param>
	public DynamicImageMiddleware(
		OwinMiddleware next,
		ILogger<DynamicImageMiddleware> logger,
		IDynamicImageUtility dynamicImageUtility,
		IDynamicImageResizer dynamicImageResizer,
		IHttpHeaderValueUtility headerValueUtility,
		IMimeTypeUtility mimeTypeUtility,
		DynamicImageMiddlewareOptions options)
		: base(next)
	{
		_log = logger;
		_dynamicImageUtility = dynamicImageUtility;
		_dynamicImageResizer = dynamicImageResizer;
		_headerValueUtility = headerValueUtility;
		_mimeTypeUtility = mimeTypeUtility;
		_options = options;
	}
	#endregion

	#region Overridden Methods		
	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current <see cref="IOwinContext"/>.</param>
	/// <exception cref="UmbrellaWebException">An error has occurred whilst executing the request.</exception>
	public override async Task Invoke(IOwinContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.Request.CallCancelled.ThrowIfCancellationRequested();

		var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Request.CallCancelled);
		CancellationToken token = cts.Token;

		try
		{
			string path = context.Request.Path.Value;

			DynamicImageFormat? overrideFormat = null;

			if (_options.EnableJpgPngWebPOrAvifOverride
				&& (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
			{
				overrideFormat = context switch
				{
					var _ when context.Request.AcceptsAvif() && _dynamicImageResizer.SupportsFormat(DynamicImageFormat.Avif) => DynamicImageFormat.Avif,
					var _ when context.Request.AcceptsWebP() && _dynamicImageResizer.SupportsFormat(DynamicImageFormat.WebP) => DynamicImageFormat.WebP,
					_ => null
				};
			}

			var (status, imageOptions) = _dynamicImageUtility.TryParseUrl(_options.DynamicImagePathPrefix, path, overrideFormat);

			if (status == DynamicImageParseUrlResult.Skip)
			{
				await Next.Invoke(context);
				return;
			}

			if (status == DynamicImageParseUrlResult.Invalid)
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			if (!_options.ImageOptionsAllowed(imageOptions))
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			DynamicImageMiddlewareMapping mapping = _options.GetMapping(imageOptions.SourcePath);

			if (mapping is null || (_options.EnableValidation && !_dynamicImageUtility.ImageOptionsValid(imageOptions, _options.AllowedVariants)))
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			DynamicImageItem? image = await _dynamicImageResizer.GenerateImageAsync(mapping.FileProviderMapping.FileProvider, imageOptions, token);

			if (image is null)
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			//Check the cache headers
			if (image.LastModified.HasValue && context.Request.IfModifiedSinceHeaderMatched(image.LastModified.Value))
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotModified);
				return;
			}

			string? eTagValue = image.LastModified.HasValue
				? _headerValueUtility.CreateETagHeaderValue(image.LastModified.Value, image.Length)
				: null;

			if (eTagValue is not null && context.Request.IfNoneMatchHeaderMatched(eTagValue))
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotModified);
				return;
			}

			if (image.Length > 0)
			{
				context.Response.ContentType = _mimeTypeUtility.GetMimeType(image.ImageOptions.Format.ToFileExtensionString());
				context.Response.ContentLength = image.Length;

				if (mapping.Cacheability == MiddlewareHttpCacheability.NoCache && image.LastModified.HasValue)
				{
					context.Response.Headers["Last-Modified"] = _headerValueUtility.CreateLastModifiedHeaderValue(image.LastModified.Value);
					context.Response.ETag = _headerValueUtility.CreateETagHeaderValue(image.LastModified.Value, image.Length);
					context.Response.Headers["Cache-Control"] = "no-cache";
				}
				else
				{
					context.Response.Headers["Cache-Control"] = "no-store";
				}

				await image.WriteContentToStreamAsync(context.Response.Body, token);

				// Ensure the response stream is flushed async immediately here. If not, there could be content
				// still buffered which will not be sent out until the stream is disposed at which point
				// the IO will happen synchronously!
				await context.Response.Body.FlushAsync(token);

				return;
			}
			else
			{
				cts.Cancel();
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}
		}
		catch (UmbrellaFileSystemException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound so that any potential attacker isn't even aware the file exists.
			cts.Cancel();
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (UmbrellaFileAccessDeniedException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound so that any potential attacker isn't even aware the file exists.
			cts.Cancel();
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (UmbrellaDynamicImageException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound.
			cts.Cancel();
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { Path = context.Request.Path.Value }))
		{
			throw new UmbrellaWebException("An error has occurred whilst executing the request.", exc);
		}
		finally
		{
			cts.Dispose();
		}
	}
	#endregion
}
