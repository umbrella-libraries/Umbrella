


using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.DynamicImage.Benchmark;

[MemoryDiagnoser]
[BenchmarkCategory(nameof(DynamicImageUtility))]
[SimpleJob(RuntimeMoniker.Net462), SimpleJob(RuntimeMoniker.Net60), SimpleJob(RuntimeMoniker.Net70), SimpleJob(RuntimeMoniker.Net90)]
public class DynamicImageUtilityBenchmark
{
	private readonly DynamicImageUtility _dynamicImageUtility;

	public DynamicImageUtilityBenchmark()
	{
		var logger = new Mock<ILogger<DynamicImageUtility>>();
		_dynamicImageUtility = new DynamicImageUtility(logger.Object);
	}

	[Benchmark]
	public DynamicImageOptions TryParseUrl()
	{
		var (_, imageOptions) = _dynamicImageUtility.TryParseUrl(DynamicImageConstants.DefaultPathPrefix, "/dynamicimage/680/649/Uniform/png/images/mobile-devices@2x.jpg");

		return imageOptions;
	}
}