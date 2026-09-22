
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.DynamicImage.Benchmark;

[MemoryDiagnoser]
[BenchmarkCategory(nameof(DynamicImageUtility))]
public class DynamicImageUtilityBenchmark
{
	private readonly DynamicImageUtility _dynamicImageUtility;

	public DynamicImageUtilityBenchmark()
	{
		_dynamicImageUtility = new DynamicImageUtility(NullLogger<DynamicImageUtility>.Instance);
	}

	[Benchmark]
	public DynamicImageOptions TryParseUrl()
	{
		var (_, imageOptions) = _dynamicImageUtility.TryParseUrl(DynamicImageConstants.DefaultPathPrefix, "/dynamicimage/680/649/Uniform/png/images/mobile-devices@2x.jpg");

		return imageOptions;
	}
}