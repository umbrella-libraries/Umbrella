using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Generated.Mapping.Mapperly;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mapping.Abstractions;

namespace Umbrella.Utilities.Mapping.Mapperly.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60), SimpleJob(RuntimeMoniker.Net80), SimpleJob(RuntimeMoniker.Net90)]
public class UmbrellaMapperBenchmark
{
	private readonly Source _source = CreateSource(1);
	private readonly IReadOnlyCollection<Source> _sourceList = new[]
	{
		CreateSource(1), CreateSource(2), CreateSource(3), CreateSource(4), CreateSource(5)
	};

	private readonly IUmbrellaMapper _mapper;

	public UmbrellaMapperBenchmark()
	{
		_mapper = CreateMapper();
	}

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_Async() => await _mapper.MapAsync<Source, Destination>(_source);

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_ExistingDestination_Async() => await _mapper.MapAsync(_source, CreateDestination(100));

	[Benchmark]
	public async Task<IReadOnlyCollection<Destination>> MapAllAsync_GenericSourceDestination_Async() => await _mapper.MapAllAsync<Source, Destination>(_sourceList);

	private static IUmbrellaMapper CreateMapper()
	{
		ServiceCollection services = new();
		_ = services.AddSingleton<ILogger<UmbrellaMapper>>(CoreUtilitiesMocks.CreateLogger<UmbrellaMapper>());
		_ = services.AddUmbrellaUtilitiesMappingMapperly(Umbrella_Utilities_Mapping_Mapperly_BenchmarkUmbrellaMapperlyCatalog.Instance);
		var provider = services.BuildServiceProvider();

		return provider.GetRequiredService<IUmbrellaMapper>();
	}

	private static Source CreateSource(int seed, bool createChildren = true)
	{
		Random random = new(seed);

		byte[] guidBytes = new byte[16];
		random.NextBytes(guidBytes);

		Source? child = createChildren ? CreateSource(random.Next(), false) : null;
		IReadOnlyCollection<Source>? children = createChildren
			? new[]
			{
				CreateSource(random.Next(), false),
				CreateSource(random.Next(), false),
				CreateSource(random.Next(), false),
				CreateSource(random.Next(), false),
				CreateSource(random.Next(), false),
			}
			: null;

		return new Source
		{
			DecimalNumber = (decimal)random.NextDouble(),
			DoublePrecisionNumber = (double)random.NextDouble(),
			HalfPrecisionNumber = (Half)random.NextDouble(),
			SinglePrecisionNumber = random.NextSingle(),
			LongNumber = random.NextInt64(),
			ShortNumber = (short)random.NextDouble(),
			WholeNumber = random.Next(),
			SomeString = new Guid(guidBytes).ToString(),
			Id = new Guid(guidBytes),
			Child = child,
			Children = children
		};
	}

	private static Destination CreateDestination(int seed, bool createChildren = true)
	{
		Random random = new(seed);

		byte[] guidBytes = new byte[16];
		random.NextBytes(guidBytes);

		Destination? child = createChildren ? CreateDestination(random.Next(), false) : null;
		IReadOnlyCollection<Destination>? children = createChildren
			? new[]
			{
				CreateDestination(random.Next(), false),
				CreateDestination(random.Next(), false),
				CreateDestination(random.Next(), false),
				CreateDestination(random.Next(), false),
				CreateDestination(random.Next(), false),
			}
			: null;

		return new Destination
		{
			DecimalNumber = (decimal)random.NextDouble(),
			DoublePrecisionNumber = (double)random.NextDouble(),
			HalfPrecisionNumber = (Half)random.NextDouble(),
			SinglePrecisionNumber = random.NextSingle(),
			LongNumber = random.NextInt64(),
			ShortNumber = (short)random.NextDouble(),
			WholeNumber = random.Next(),
			SomeString = new Guid(guidBytes).ToString(),
			Id = new Guid(guidBytes),
			Child = child,
			Children = children
		};
	}
}