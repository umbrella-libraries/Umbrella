using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Benchmark.Extensions;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462), SimpleJob(RuntimeMoniker.Net60)]
public class StringExtensionsBenchmark
{
	private const string ShortString = "Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF)";
	private const string LongString = "Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF) Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF) Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF) Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF) Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF) Rio Tinto Minera Perú Limitada SAC (RTMPL) Borax Français (BF)";

	[Benchmark]
	public string RemapInternationalCharactersToAsciiBenchmark_StringBuilder_ShortString() => ShortString.RemapInternationalCharactersToAscii_StringBuilder();

	[Benchmark]
	public string RemapInternationalCharactersToAsciiBenchmark_Span_ShortString() => ShortString.RemapInternationalCharactersToAscii();

	[Benchmark]
	public string RemapInternationalCharactersToAsciiBenchmark_StringBuilder_LongString() => LongString.RemapInternationalCharactersToAscii_StringBuilder();

	[Benchmark]
	public string RemapInternationalCharactersToAsciiBenchmark_Span_LongString() => LongString.RemapInternationalCharactersToAscii();
}