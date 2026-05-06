using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.Extensions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.FileSystem.Middleware.Options;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.AspNetCore.WebUtilities.FileSystem.Middleware;

/// <summary>
/// Middleware that is used to access files stored in a physical or virtual file system. Underling file access
/// is provided using the <see cref="Umbrella.FileSystem"/> infrastructure.
/// </summary>
public class FileSystemMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger _log;
	private readonly IHttpHeaderValueUtility _httpHeaderValueUtility;
	private readonly FileSystemMiddlewareOptions _options;
	private readonly string _fileSystemPathPrefix;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileSystemMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next middleware.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="httpHeaderValueUtility">The HTTP header value utility.</param>
	/// <param name="options">The options.</param>
	public FileSystemMiddleware(
		RequestDelegate next,
		ILogger<FileSystemMiddleware> logger,
		IHttpHeaderValueUtility httpHeaderValueUtility,
		FileSystemMiddlewareOptions options)
	{
		Guard.IsNotNull(options);

		_next = next;
		_log = logger;
		_httpHeaderValueUtility = httpHeaderValueUtility;
		_options = options;
		_fileSystemPathPrefix = "/" + options.FileSystemPathPrefix.Trim('/');
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
			if (!context.Request.Path.StartsWithSegments(_fileSystemPathPrefix, StringComparison.OrdinalIgnoreCase, out PathString remainingPath)
				|| !remainingPath.HasValue
				|| remainingPath.Value!.Length <= 1)
			{
				await _next.Invoke(context);
				return;
			}

			if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
			{
				await _next.Invoke(context);
				return;
			}

			string path = remainingPath.Value!;

			if (IsInvalidFilePath(path))
			{
				context.Response.SendStatusCode(HttpStatusCode.NotFound);
				return;
			}

			FileSystemMiddlewareMapping? mapping = _options.GetMapping(path);

			if (mapping is not null)
			{
				CancellationToken token = context.RequestAborted;
				bool isHeadRequest = HttpMethods.IsHead(context.Request.Method);

				IUmbrellaFileInfo? fileInfo = await mapping.FileProviderMapping.FileProvider.GetAsync(path, token);

				if (fileInfo is null)
				{
					context.Response.SendStatusCode(HttpStatusCode.NotFound);

					return;
				}

				bool hasValidators = fileInfo.LastModified.HasValue;
				bool supportsConditionalRequests = hasValidators
					&& mapping.Cacheability is MiddlewareHttpCacheability.NoCache
						or MiddlewareHttpCacheability.Private
						or MiddlewareHttpCacheability.Public;
				string? lastModifiedHeaderValue = hasValidators
					? _httpHeaderValueUtility.CreateLastModifiedHeaderValue(fileInfo.LastModified!.Value)
					: null;
				string? eTagValue = hasValidators
					? _httpHeaderValueUtility.CreateETagHeaderValue(fileInfo.LastModified!.Value, fileInfo.Length)
					: null;

				void ApplyResponseHeaders()
				{
					context.Response.Headers.XContentTypeOptions = "nosniff";

					if (mapping.Cacheability == MiddlewareHttpCacheability.NoCache && hasValidators)
					{
						context.Response.Headers.LastModified = lastModifiedHeaderValue;
						context.Response.Headers.ETag = eTagValue;
						context.Response.Headers.CacheControl = "no-cache";
					}
					else if (mapping.Cacheability is MiddlewareHttpCacheability.Private or MiddlewareHttpCacheability.Public)
					{
						if (hasValidators)
						{
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

					if (context.Request.IfModifiedSinceHeaderMatched(fileInfo.LastModified!.Value))
					{
						ApplyResponseHeaders();
						context.Response.SendStatusCode(HttpStatusCode.NotModified);

						return;
					}
				}

				ApplyResponseHeaders();
				context.Response.ContentType = fileInfo.ContentType ?? "application/octet-stream";
				context.Response.ContentLength = fileInfo.Length;

				// TODO: Build in support for Range request header and Content-Range response header using a 206 response code.
				// Need to alter the following:

				// fileInfo.WriteToStreamAsync
				// fileInfo.ReadAsStreamAsync
				// fileInfo.ReadAsByteArrayAsync

				// Before altering the above, use read as stream or byte array to test the Range stuff works.
				// Probably best to copy this middleware into the target project first to do the initial work
				// before altering the file system.

				if (isHeadRequest)
					return;

				await fileInfo.WriteToStreamAsync(context.Response.Body, cancellationToken: token);
				await context.Response.Body.FlushAsync(token);

				return;
			}

			await _next.Invoke(context);
		}
		catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
		{
			if (!context.Response.HasStarted)
				_log.WriteDebug(new { Path = context.Request.Path.Value }, "The request was aborted by the client.");
		}
		catch (OperationCanceledException)
		{
			if (!context.Response.HasStarted)
				context.Response.SendStatusCode(HttpStatusCode.RequestTimeout);
		}
		catch (UmbrellaFileSystemException exc) when (_log.WriteWarning(exc, new { Path = context.Request.Path.Value }))
		{
			// Just return a 404 NotFound so that any potential attacker isn't even aware the file exists.
			context.Response.SendStatusCode(HttpStatusCode.NotFound);
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

	private static bool IsInvalidFilePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path)
			|| path.Length <= 1
			|| path[^1] == '/'
			|| path.Contains('\\', StringComparison.Ordinal)
			|| path.Contains('\0', StringComparison.Ordinal))
		{
			return true;
		}

		string[] segments = path.Split('/');

		for (int i = 1; i < segments.Length; i++)
		{
			string segment = segments[i];

			if (string.IsNullOrWhiteSpace(segment) || segment is "." or "..")
				return true;
		}

		return false;
	}
}
