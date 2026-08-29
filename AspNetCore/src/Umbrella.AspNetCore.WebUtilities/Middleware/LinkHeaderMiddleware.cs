using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Http;
#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.WebUtilities.Middleware.Options.LinkHeader;

namespace Umbrella.AspNetCore.WebUtilities.Middleware;

/// <summary>
/// Middleware that adds Link headers to outgoing HTML responses for a list of URLs specified
/// using the <see cref="LinkHeaderMiddlewareOptions"/>.
/// </summary>
/// <remarks>
/// URLs are appended as headers as a list with rel=preconnect and rel=dns-prefetch values output for each URL.
/// </remarks>
public class LinkHeaderMiddleware
{
	private readonly ILogger _log;
	private readonly PlatformCache _cache;
	private readonly ICacheKeyUtility _cacheKeyUtility;
	private readonly RequestDelegate _next;
	private readonly LinkHeaderMiddlewareOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="LinkHeaderMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The platform cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="next">The next.</param>
	/// <param name="options">The options.</param>
	public LinkHeaderMiddleware(
		ILogger<LinkHeaderMiddleware> logger,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		RequestDelegate next,
		LinkHeaderMiddlewareOptions options)
	{
		_log = logger;
		_cache = cache;
		_cacheKeyUtility = cacheKeyUtility;
		_next = next;
		_options = options;
	}

	/// <summary>
	/// Invokes the middleware in the context of the current request. This method is called by the ASP.NET Core infrastructure.
	/// </summary>
	/// <param name="context">The current <see cref="HttpContext"/>.</param>
	public async Task InvokeAsync(HttpContext context)
	{
		Guard.IsNotNull(context);
		context.RequestAborted.ThrowIfCancellationRequested();

		try
		{
			context.Response.OnStarting(async () =>
			{
				if (_options.DnsPrefetchPreconnectUrls.Count is 0 && _options.PreloadUrls.Count is 0)
					return;

				bool isHtmlResponse = context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true;

				// Don't bother setting the Link header for non-HTML responses.
				if (isHtmlResponse)
				{
					string cacheKey = _cacheKeyUtility.Create<LinkHeaderMiddleware>("LinkHeaders");

					string[] Create() => _options.DnsPrefetchPreconnectUrls.SelectMany(x => x.ToLinkHeaderStrings()).Concat(_options.PreloadUrls.Select(x => x.ToLinkHeaderString())).ToArray();

					string[] cachedValue;

					if (!_options.CacheEnabled)
					{
						cachedValue = Create();
					}
					else
					{
#if NET9_0_OR_GREATER
						var options = new HybridCacheEntryOptions
						{
							Expiration = _options.CacheTimeout,
							LocalCacheExpiration = _options.CacheTimeout,
							Flags = _options.CacheEntryFlags
						};

						cachedValue = await _cache.GetOrCreateAsync<string[]>(cacheKey, _ => ValueTask.FromResult(Create()), options, cancellationToken: context.RequestAborted).ConfigureAwait(false);
#else
						(cachedValue, _) = await _cache.GetOrCreateAsync(
							cacheKey,
							() => Task.FromResult(Create()),
							() => new DistributedCacheEntryOptions().SetAbsoluteExpiration(_options.CacheTimeout),
							cancellationToken: context.RequestAborted).ConfigureAwait(false);
#endif
					}

					context.Response.Headers.AppendList("Link", cachedValue);
				}
			});
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw;
		}

		await _next.Invoke(context);
	}
}
