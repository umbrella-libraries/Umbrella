using BenchmarkDotNet.Attributes;

namespace Umbrella.DynamicImage.Benchmark;

[MemoryDiagnoser]
[BenchmarkCategory("Resize")]
public class DynamicImageResizerBenchmark
{
	private ResizerWorkload _workload = null!;
	public static IEnumerable<string> Scenarios => ResizeScenario.CommonIds;
	[ParamsSource(nameof(Scenarios))]
	public string Scenario { get; set; } = null!;

	[GlobalSetup(Target = nameof(FreeImage))]
	public void SetupFreeImage() => Setup("FreeImage");
	[GlobalSetup(Target = nameof(NetVips))]
	public void SetupNetVips() => Setup("NetVips");
	[GlobalSetup(Target = nameof(SkiaSharp))]
	public void SetupSkiaSharp() => Setup("SkiaSharp");

	private void Setup(string implementation)
	{
		_workload = new(implementation, ResizeScenario.Find(Scenario));
		Console.WriteLine($"Validated {implementation}/{Scenario}; output bytes: {_workload.Validate()}");
	}

	[Benchmark]
	public (byte[], int, int) FreeImage() => _workload.Resize();
	[Benchmark]
	public (byte[], int, int) NetVips() => _workload.Resize();
	[Benchmark(Baseline = true)]
	public (byte[], int, int) SkiaSharp() => _workload.Resize();
}

[MemoryDiagnoser]
[BenchmarkCategory("Resize", "Avif")]
public class NetVipsAvifBenchmark
{
	private ResizerWorkload _workload = null!;
	public static IEnumerable<string> Scenarios => ResizeScenario.AvifIds;
	[ParamsSource(nameof(Scenarios))]
	public string Scenario { get; set; } = null!;

	[GlobalSetup]
	public void Setup()
	{
		_workload = new("NetVips", ResizeScenario.Find(Scenario));
		Console.WriteLine($"Validated NetVips/{Scenario}; output bytes: {_workload.Validate()}");
	}

	[Benchmark]
	public (byte[], int, int) NetVips() => _workload.Resize();
}