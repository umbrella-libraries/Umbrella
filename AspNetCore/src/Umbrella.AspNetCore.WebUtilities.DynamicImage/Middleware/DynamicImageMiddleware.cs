using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Extensions;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.DynamicImage.Middleware.Options;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Middleware;

/// <summary>
/// Middleware that is used to return a dynamically resized version of a source image. The source image and resizing
/// options are determined by parsing the incoming request URL.
/// </summary>
public class DynamicImageMiddleware : IDisposable
{
	private readonly RequestDelegate _next;
	private readonly ILogger _log;
	private readonly IDynamicImageUtility _dynamicImageUtility;
	private readonly IDynamicImageResizer _dynamicImageResizer;
	private readonly IHttpHeaderValueUtility _headerValueUtility;
	private readonly IMimeTypeUtility _mimeTypeUtility;
	private readonly DynamicImageMiddlewareOptions _options;
	private readonly SemaphoreSlim? _requestConcurrencySemaphore;
	private bool _disposedValue;

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
		RequestDelegate next,
		ILogger<DynamicImageMiddleware> logger,
		IDynamicImageUtility dynamicImageUtility,
		IDynamicImageResizer dynamicImageResizer,
		IHttpHeaderValueUtility headerValueUtility,
		IMimeTypeUtility mimeTypeUtility,
		DynamicImageMiddlewareOptions options)
	{
		Guard.IsNotNull(options);

		_next = next;
		_log = logger;
		_dynamicImageUtility = dynamicImageUtility;
		_dynamicImageResizer = dynamicImageResizer;
		_headerValueUtility = headerValueUtility;
		_mimeTypeUtility = mimeTypeUtility;
		_options = options;

		if (_options.MaxConcurrentResizingRequests > 0)
			_requestConcurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentResizingRequests, _options.MaxConcurrentResizingRequests);
	}

	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	public async Task InvokeAsync(HttpContext context)
	{
		Guard.IsNotNull(context);
		context.RequestAborted.ThrowIfCancellationRequested();

		try
		{
			string? path = context.Request.Path.Value?.Trim();

			if (string.IsNullOrEmpty(path) || !path.StartsWith($"/{_options.DynamicImagePathPrefix}/", StringComparison.OrdinalIgnoreCase))
			{
				await _next.Invoke(context);
				return;
			}

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

			if (status is DynamicImageParseUrlResult.Skip)
			{
				await _next.Invoke(context);
				return;
			}

			if (status is DynamicImageParseUrlResult.Invalid)
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			DynamicImageMiddlewareMapping mapping = _options.GetMapping(imageOptions.SourcePath);

			if (mapping is null || (mapping.EnableValidation && !_dynamicImageUtility.ImageOptionsValid(imageOptions, mapping.ValidMappings)))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			IUmbrellaFileInfo? sourceFile = await mapping.FileProviderMapping.FileProvider.GetAsync(imageOptions.SourcePath, context.RequestAborted);

			if (sourceFile is null)
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			// Check the cache headers
			if (sourceFile.LastModified.HasValue && context.Request.IfModifiedSinceHeaderMatched(sourceFile.LastModified.Value))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotModified);
				return;
			}

			string? eTagValue = sourceFile.LastModified.HasValue
				? _headerValueUtility.CreateETagHeaderValue(sourceFile.LastModified.Value, sourceFile.Length)
				: null;

			if (eTagValue is not null && context.Request.IfNoneMatchHeaderMatched(eTagValue))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotModified);
				return;
			}

			async Task ApplyCacheHeadersAndFlushAsync(DynamicImageItem image)
			{
				context.Response.ContentType = _mimeTypeUtility.GetMimeType(image.ImageOptions.Format.ToFileExtensionString());
				context.Response.ContentLength = image.Length;

				if (mapping.Cacheability is MiddlewareHttpCacheability.NoCache && image.LastModified.HasValue)
				{
					context.Response.Headers.LastModified = _headerValueUtility.CreateLastModifiedHeaderValue(image.LastModified.Value);
					context.Response.Headers.ETag = eTagValue;
					context.Response.Headers.CacheControl = "no-cache";
				}
				else
				{
					context.Response.Headers.CacheControl = "no-store";
				}

				await image.WriteContentToStreamAsync(context.Response.Body, context.RequestAborted);

				// Ensure the response stream is flushed async immediately here. If not, there could be content
				// still buffered which will not be sent out until the stream is disposed at which point
				// the IO will happen synchronously!
				await context.Response.Body.FlushAsync(context.RequestAborted);
			}

			// Check if the image is already cached
			DynamicImageItem? image = await _dynamicImageResizer.GetCachedItemAsync(sourceFile, imageOptions, context.RequestAborted);

			if (image is { Length: > 0 })
			{
				await ApplyCacheHeadersAndFlushAsync(image);
				return;
			}

			if (_requestConcurrencySemaphore is not null)
				await _requestConcurrencySemaphore.WaitAsync(context.RequestAborted);

			// No image in cache, need to create
			try
			{
				image = await _dynamicImageResizer.GenerateImageAsync(mapping.FileProviderMapping.FileProvider, imageOptions, context.RequestAborted);
			}
			finally
			{
				_ = _requestConcurrencySemaphore?.Release();
			}

			if (image is { Length: > 0 })
			{
				await ApplyCacheHeadersAndFlushAsync(image);
				return;
			}

			context.Response.SendStatusCode(HttpStatusCode.NotFound);
			return;
		}
		catch (OperationCanceledException)
		{
			// Handle the cancellation
			context.Response.SendStatusCode(HttpStatusCode.RequestTimeout);
		}
		catch (UmbrellaFileSystemException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (UmbrellaFileAccessDeniedException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound so that any potential attacker isn't even aware the file exists.
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (UmbrellaDynamicImageException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound.
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
		}
		catch (Exception exc) when (_log.WriteError(exc, new { Path = context.Request.Path.Value }))
		{
			throw new UmbrellaWebException("An error has occurred whilst executing the request.", exc);
		}
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="DynamicImageMiddleware" /> and optionally releases the
	/// managed resources.
	/// </summary>
	/// <param name="disposing">A value indicating whether the managed resources should be released.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_requestConcurrencySemaphore?.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}