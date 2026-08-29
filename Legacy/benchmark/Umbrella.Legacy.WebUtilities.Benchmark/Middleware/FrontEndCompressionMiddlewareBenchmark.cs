using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Moq;
using Umbrella.Internal.Mocks;
using Umbrella.Legacy.WebUtilities.Middleware;
using Umbrella.Utilities.Hosting.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.WebUtilities.Http.Abstractions;
using Umbrella.WebUtilities.Middleware.Options;

namespace Umbrella.Legacy.WebUtilities.Benchmark.Middleware;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462)]
public class FrontEndCompressionMiddlewareBenchmark
{
	private readonly FrontEndCompressionMiddleware _frontEndCompressionMiddleware;

	public FrontEndCompressionMiddlewareBenchmark()
	{
		var logger = new Mock<ILogger<FrontEndCompressionMiddleware>>();
		var cache = new Mock<IDistributedCache>();
		var hostingEnvironment = new Mock<IUmbrellaHostingEnvironment>();
		var httpHeaderValueUtility = new Mock<IHttpHeaderValueUtility>();
		var mimeTypeUtility = new Mock<IMimeTypeUtility>();

		var options = new FrontEndCompressionMiddlewareOptions
		{
			Mappings =
			[
				new()
				{
					AppRelativeFolderPaths = ["/sitefiles"]
				}
			]
		};

		_frontEndCompressionMiddleware = new FrontEndCompressionMiddleware(
			null!,
			logger.Object,
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			cache.Object,
			hostingEnvironment.Object,
			httpHeaderValueUtility.Object,
			mimeTypeUtility.Object,
			options);

		var fileProvider = new Mock<IFileProvider>();

		_frontEndCompressionMiddleware.FileProvider = fileProvider.Object;
	}

	[Benchmark]
	public async Task RunMiddlewareAsync()
	{
		var context = new Mock<IOwinContext>();

		await _frontEndCompressionMiddleware.Invoke(context.Object).ConfigureAwait(true);
	}
}
