using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Encryption;
using Umbrella.Utilities.Encryption.Options;

namespace Umbrella.Utilities.Benchmark.Encryption;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net462), SimpleJob(RuntimeMoniker.NetCoreApp31)]
public class SecureStringGeneratorBenchmark
{
	private readonly SecureRandomStringGenerator _secureStringGenerator;

	public SecureStringGeneratorBenchmark()
	{
		var logger = new Mock<ILogger<SecureRandomStringGenerator>>();
		var options = new SecureRandomStringGeneratorOptions();

		_secureStringGenerator = new SecureRandomStringGenerator(logger.Object, options);
	}

	[Benchmark]
	public string Generate() => _secureStringGenerator.Generate(20, 5, 5);

	[Benchmark(Baseline = true)]
	public string GenerateOld() =>
#pragma warning disable CS0612 // Type or member is obsolete
		_secureStringGenerator.GenerateOld(20, 5, 5);
#pragma warning restore CS0612 // Type or member is obsolete

}
