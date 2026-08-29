
using System.Net.Http;
using CommunityToolkit.Diagnostics;
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
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Http.Options;

namespace Umbrella.Utilities.Http;

/// <summary>
/// A utility class used to get basic details of a resource on a URL.
/// </summary>
public class HttpResourceInfoUtility : IHttpResourceInfoUtility, IDisposable
{
	private const string DefaultMimeType = "application/octet-stream";
	private readonly ILogger _log;
	private readonly HttpClient _httpClient;
	private readonly PlatformCache _cache;
	private readonly ICacheKeyUtility _cacheKeyUtility;
	private readonly HttpResourceInfoUtilityOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpResourceInfoUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="httpClient">The HTTP Client.</param>
	/// <param name="options">The options.</param>
	public HttpResourceInfoUtility(
		ILogger<HttpResourceInfoUtility> logger,
		PlatformCache cache,
		ICacheKeyUtility cacheKeyUtility,
		HttpClient httpClient,
		HttpResourceInfoUtilityOptions options)
	{
		_log = logger;
		_cache = cache;
		_cacheKeyUtility = cacheKeyUtility;
		_httpClient = httpClient;
		_options = options;
	}

	#region IHttpFileInfoUtility Members		
	/// <summary>
	/// Gets the <see cref="HttpResourceInfo"/> for the specified <paramref name="url"/>. Returns null where the resource cannot be found.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <param name="useCache">Determines whether to cache the resource info.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The <see cref="HttpResourceInfo"/>.</returns>
	/// <exception cref="UmbrellaException">There was a problem retrieving data for the specified url: {url}</exception>
	public async Task<HttpResourceInfo?> GetAsync(string url, bool useCache = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(url, nameof(url));

		try
		{
			async Task<HttpResourceInfo?> CreateAsync(CancellationToken token)
			{
				using var request = new HttpRequestMessage(HttpMethod.Head, url);
				using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);

				long contentLength = response.Content.Headers.ContentLength ?? 0;

				if (response.IsSuccessStatusCode && contentLength > 0)
				{
					DateTime? lastModified = null;

					if (response.Headers.TryGetValues("Last-Modified", out IEnumerable<string>? values) && DateTime.TryParse(values.FirstOrDefault(), out DateTime result))
						lastModified = result;

					return new HttpResourceInfo(response.Content.Headers.ContentType?.MediaType ?? DefaultMimeType, contentLength, lastModified, url);
				}

				return null;
			}

			if (!_options.CacheEnabled || !useCache)
				return await CreateAsync(cancellationToken).ConfigureAwait(false);

			string cacheKey = _cacheKeyUtility.Create<HttpResourceInfoUtility>(url);

#if NET9_0_OR_GREATER
			var options = new HybridCacheEntryOptions
			{
				Expiration = _options.CacheTimeout,
				LocalCacheExpiration = _options.CacheTimeout,
				Flags = _options.CacheEntryFlags
			};

			return await _cache.GetOrCreateAsync(cacheKey, async token => await CreateAsync(token).ConfigureAwait(false), options, cancellationToken: cancellationToken).ConfigureAwait(false);
#else
			(HttpResourceInfo? item, _) = await _cache.GetOrCreateAsync(
				cacheKey,
				async () => await CreateAsync(cancellationToken).ConfigureAwait(false),
				() => new DistributedCacheEntryOptions().SetAbsoluteExpiration(_options.CacheTimeout),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			return item;
#endif
		}
		catch (Exception exc) when (_log.WriteError(exc, new { url }))
		{
			throw new UmbrellaException($"There was a problem retrieving data for the specified url: {url}", exc);
		}
	}
	#endregion

	#region IDisposable Support
	private bool _disposedValue; // To detect redundant calls

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
				_httpClient.Dispose();

			_disposedValue = true;
		}
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true); // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		GC.SuppressFinalize(this);
	}
	#endregion
}
