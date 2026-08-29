using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Options;
using Umbrella.Utilities.Options;

#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
using PlatformCache = Microsoft.Extensions.Caching.Hybrid.HybridCache;
#else
using Microsoft.Extensions.Caching.Distributed;
using Umbrella.Utilities.Caching;
using PlatformCache = Microsoft.Extensions.Caching.Distributed.IDistributedCache;
#endif

namespace Umbrella.Utilities.Test.Caching;

public sealed class PlatformCacheTest
{
	[Fact]
	public void CacheableOptionsExposeOnlySupportedPlatformSettings()
	{
		var options = new HttpResourceInfoUtilityOptions();

		Assert.IsAssignableFrom<CacheableUmbrellaOptions>(options);
		Assert.True(options.CacheEnabled);
		Assert.Equal(TimeSpan.FromHours(1), options.CacheTimeout);

#if NET9_0_OR_GREATER
		Assert.Equal(HybridCacheEntryFlags.None, options.CacheEntryFlags);
#else
		Assert.Null(typeof(CacheableUmbrellaOptions).GetProperty("CacheEntryFlags"));
#endif
	}

	[Fact]
	public void AddUmbrellaUtilitiesRegistersFrameworkCache()
	{
		var services = new ServiceCollection();
#if !NET9_0_OR_GREATER
		_ = services.AddDistributedMemoryCache();
#endif
		_ = services.AddUmbrellaUtilities();

		using ServiceProvider provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetRequiredService<PlatformCache>());
	}

	[Fact]
	public async Task HttpResourceCacheHandlesMissAndHitAsync()
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		using var handler = new CountingHttpMessageHandler();
		using var utility = CreateHttpResourceInfoUtility(handler, new HttpResourceInfoUtilityOptions());

		HttpResourceInfo? first = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);
		HttpResourceInfo? second = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);

		Assert.NotNull(first);
		Assert.NotNull(second);
		Assert.Equal(first.Url, second.Url);
		Assert.Equal(first.ContentType, second.ContentType);
		Assert.Equal(first.ContentLength, second.ContentLength);
		Assert.Equal(first.LastModified, second.LastModified);
		Assert.Equal(1, handler.RequestCount);
	}

	[Fact]
	public async Task HttpResourceCacheCanBeDisabledAsync()
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		using var handler = new CountingHttpMessageHandler();
		using var utility = CreateHttpResourceInfoUtility(handler, new HttpResourceInfoUtilityOptions { CacheEnabled = false });

		_ = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);
		_ = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);

		Assert.Equal(2, handler.RequestCount);
	}

	[Fact]
	public async Task HttpResourceCacheExpiresAsync()
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		using var handler = new CountingHttpMessageHandler();
		using var utility = CreateHttpResourceInfoUtility(handler, new HttpResourceInfoUtilityOptions { CacheTimeout = TimeSpan.FromMilliseconds(20) });

		_ = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);
		await Task.Delay(100, cancellationToken);
		_ = await utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationToken);

		Assert.Equal(2, handler.RequestCount);
	}

	[Fact]
	public async Task HttpResourceCacheHonorsCancellationAsync()
	{
		using var handler = new CountingHttpMessageHandler();
		using var utility = CreateHttpResourceInfoUtility(handler, new HttpResourceInfoUtilityOptions());
		using var cancellationTokenSource = new CancellationTokenSource();
		await cancellationTokenSource.CancelAsync();

		_ = await Assert.ThrowsAsync<OperationCanceledException>(() => utility.GetAsync("https://example.test/image.png", cancellationToken: cancellationTokenSource.Token));
	}

	[Fact]
	public async Task PlatformCacheSetsSerializesAndRemovesPayloadAsync()
	{
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		PlatformCache cache = CoreUtilitiesMocks.CreateCache();
		var payload = new SerializableCachePayload(42, "cached");

#if NET9_0_OR_GREATER
		await cache.SetAsync("platform-cache-payload", payload, cancellationToken: cancellationToken);
		SerializableCachePayload cached = await cache.GetOrCreateAsync(
			"platform-cache-payload",
			static _ => ValueTask.FromResult(new SerializableCachePayload(0, "factory")),
			cancellationToken: cancellationToken);
		await cache.RemoveAsync("platform-cache-payload", cancellationToken);
		SerializableCachePayload recreated = await cache.GetOrCreateAsync(
			"platform-cache-payload",
			static _ => ValueTask.FromResult(new SerializableCachePayload(7, "recreated")),
			cancellationToken: cancellationToken);
#else
		await cache.SetAsync("platform-cache-payload", payload, new DistributedCacheEntryOptions(), cancellationToken);
		SerializableCachePayload? cached = await cache.GetAsync<SerializableCachePayload>("platform-cache-payload", cancellationToken);
		await cache.RemoveAsync("platform-cache-payload", cancellationToken);
		SerializableCachePayload? recreated = await cache.GetAsync<SerializableCachePayload>("platform-cache-payload", cancellationToken);
#endif

		Assert.Equal(payload, cached);
#if NET9_0_OR_GREATER
		Assert.Equal(new SerializableCachePayload(7, "recreated"), recreated);
#else
		Assert.Null(recreated);
#endif
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient ownership is transferred to HttpResourceInfoUtility, which disposes it.")]
	private static HttpResourceInfoUtility CreateHttpResourceInfoUtility(CountingHttpMessageHandler handler, HttpResourceInfoUtilityOptions options)
		=> new(
			CoreUtilitiesMocks.CreateLogger<HttpResourceInfoUtility>(),
			CoreUtilitiesMocks.CreateCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			new HttpClient(handler),
			options);

	private sealed record SerializableCachePayload(int Id, string Name);

	private sealed class CountingHttpMessageHandler : HttpMessageHandler
	{
		public int RequestCount { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			RequestCount++;

			var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
			{
				Content = new ByteArrayContent([1, 2, 3])
			};
			response.Content.Headers.ContentLength = 3;
			response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
			_ = response.Headers.TryAddWithoutValidation("Last-Modified", "Wed, 21 Oct 2015 07:28:00 GMT");

			return Task.FromResult(response);
		}
	}
}
