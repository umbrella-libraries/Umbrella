using System.Threading.RateLimiting;
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
	private readonly ConcurrencyLimiter? _requestConcurrencyLimiter;
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
		{
			_requestConcurrencyLimiter = new(new ConcurrencyLimiterOptions
			{
				PermitLimit = _options.MaxConcurrentResizingRequests,
				QueueLimit = int.MaxValue,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst
			});
		}
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

			string relativeUrl = path + context.Request.QueryString;
			var (status, requestedImageOptions) = _dynamicImageUtility.TryParseUrl(_options.DynamicImagePathPrefix, relativeUrl);

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

			if (!_options.ImageOptionsAllowed(requestedImageOptions))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			DynamicImageMiddlewareMapping mapping = _options.GetMapping(requestedImageOptions.SourcePath);

			if (mapping is null || (_options.EnableValidation && !_dynamicImageUtility.ImageOptionsValid(requestedImageOptions, _options.AllowedVariants)))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			if (!_dynamicImageResizer.SupportsFormat(requestedImageOptions.Format))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			DynamicImageOptions imageOptions = requestedImageOptions;

			IUmbrellaFileInfo? sourceFile = await mapping.FileProviderMapping.FileProvider.GetAsync(imageOptions.SourcePath, context.RequestAborted);

			if (sourceFile is null)
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			string? currentVersionToken = await UmbrellaFileVersionTokenUtility.CreateAsync(sourceFile, context.RequestAborted).ConfigureAwait(false);
			bool hasLastModified = sourceFile.LastModified.HasValue;
			bool hasValidators = !string.IsNullOrWhiteSpace(currentVersionToken);
			bool supportsConditionalRequests = hasValidators
				&& mapping.Cacheability is MiddlewareHttpCacheability.NoCache
					or MiddlewareHttpCacheability.Private
					or MiddlewareHttpCacheability.Public;
			string? lastModifiedHeaderValue = hasLastModified
				? _headerValueUtility.CreateLastModifiedHeaderValue(sourceFile.LastModified!.Value)
				: null;
			string? eTagValue = hasValidators ? $"\"{currentVersionToken}\"" : null;

			if (TryRedirectToCanonicalUrl(context, requestedImageOptions, currentVersionToken))
			{
				return;
			}

			bool preventLongLivedUnfingerprintedCaching = _options.EnableUrlFingerprinting && string.IsNullOrWhiteSpace(currentVersionToken);

			void ApplyResponseHeaders()
			{
				context.Response.Headers.XContentTypeOptions = "nosniff";

				if (preventLongLivedUnfingerprintedCaching)
				{
					context.Response.Headers.CacheControl = "no-store";
				}
				else if (mapping.Cacheability is MiddlewareHttpCacheability.NoCache && hasValidators)
				{
					if (hasLastModified)
						context.Response.Headers.LastModified = lastModifiedHeaderValue;

					context.Response.Headers.ETag = eTagValue;
					context.Response.Headers.CacheControl = "no-cache";
				}
				else if (mapping.Cacheability is MiddlewareHttpCacheability.Private or MiddlewareHttpCacheability.Public)
				{
					if (hasValidators)
					{
						if (hasLastModified)
							context.Response.Headers.LastModified = lastModifiedHeaderValue;

						context.Response.Headers.ETag = eTagValue;
					}

					if (mapping.MaxAgeSeconds.HasValue)
						context.Response.Headers.Expires = DateTimeOffset.UtcNow.AddSeconds(mapping.MaxAgeSeconds.Value).ToString("R");

					string cacheControl = mapping.Cacheability.ToCacheControlString();

					if (mapping.MaxAgeSeconds.HasValue)
						cacheControl += ", max-age=" + mapping.MaxAgeSeconds.Value;

					cacheControl += ", must-revalidate";
					context.Response.Headers.CacheControl = cacheControl;
				}
				else
				{
					context.Response.Headers.CacheControl = "no-store";
				}
			}

			// Check the cache headers
			if (supportsConditionalRequests)
			{
				if (context.Request.IfNoneMatchHeaderMatched(eTagValue!))
				{
					ApplyResponseHeaders();
					context.Response.SendStatusCode(HttpStatusCode.NotModified);
					return;
				}

				if (hasLastModified && context.Request.IfModifiedSinceHeaderMatched(sourceFile.LastModified!.Value))
				{
					ApplyResponseHeaders();
					context.Response.SendStatusCode(HttpStatusCode.NotModified);
					return;
				}
			}

			async Task ApplyCacheHeadersAndFlushAsync(DynamicImageItem image)
			{
				ApplyResponseHeaders();
				context.Response.ContentType = _mimeTypeUtility.GetMimeType(image.ImageOptions.Format.ToFileExtensionString());
				context.Response.ContentLength = image.Length;

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

			// No image in cache, need to create
			using (RateLimitLease? lease = _requestConcurrencyLimiter is null
				? null
				: await _requestConcurrencyLimiter.AcquireAsync(1, context.RequestAborted))
			{
				if (lease is not null && !lease.IsAcquired)
				{
					context.Response.SendStatusCode(HttpStatusCode.ServiceUnavailable);
					return;
				}

				image = await _dynamicImageResizer.GenerateImageAsync(mapping.FileProviderMapping.FileProvider, imageOptions, context.RequestAborted);
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

	private static DynamicImageOptions CreateCanonicalImageOptions(in DynamicImageOptions options, bool enableUrlFingerprinting, string? versionToken)
	{
		bool useVersionedPath = enableUrlFingerprinting && !string.IsNullOrWhiteSpace(versionToken);

		// Focal points remain in the original query string so their raw values are preserved when constructing the redirect location.
		// Including them here would cause GenerateVirtualPath to regenerate and duplicate them when the query string is appended.
		return new(
			options.SourcePath,
			options.Width,
			options.Height,
			options.ResizeMode,
			options.Format,
			options.FilterQuality,
			options.QualityRequest,
			versionToken: useVersionedPath ? versionToken : null);
	}

	private bool TryRedirectToCanonicalUrl(HttpContext context, in DynamicImageOptions requestedImageOptions, string? versionToken)
	{
		Guard.IsNotNull(context);

		bool canGenerateVersionedCanonicalUrl = _options.EnableUrlFingerprinting && !string.IsNullOrWhiteSpace(versionToken);

		if (canGenerateVersionedCanonicalUrl)
		{
			if (string.Equals(requestedImageOptions.VersionToken, versionToken, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		else if (string.IsNullOrWhiteSpace(requestedImageOptions.VersionToken))
		{
			return false;
		}

		DynamicImageOptions canonicalImageOptions = CreateCanonicalImageOptions(requestedImageOptions, _options.EnableUrlFingerprinting, versionToken);
		string location = _dynamicImageUtility.GenerateVirtualPath(_options.DynamicImagePathPrefix, canonicalImageOptions).TrimStart('~');
		location += context.Request.QueryString;

		if (context.Request.PathBase.HasValue)
			location = context.Request.PathBase + location;

		context.Response.Headers.Location = location;
		context.Response.Headers.CacheControl = "no-store";
		context.Response.StatusCode = (int)_options.CanonicalRedirectStatusCode;

		return true;
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
				_requestConcurrencyLimiter?.Dispose();
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
