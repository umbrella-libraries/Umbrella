using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mapping.Mapperly.Enumerations;
using Umbrella.Utilities.Mapping.Mapperly.Options;

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

	private readonly UmbrellaMapper _clientMapper;
	private readonly UmbrellaMapper _serverMapper;

	public UmbrellaMapperBenchmark()
	{
		_clientMapper = CreateMapper(MapperlyEnvironmentType.Client);
		_serverMapper = CreateMapper(MapperlyEnvironmentType.Server);
	}

	[Benchmark]
	public async Task<Destination> MapAsync_ObjectSource_Client_Async() => await _clientMapper.MapAsync<Destination>(_source);

	[Benchmark]
	public async Task<Destination> MapAsync_ObjectSource_Server_Async() => await _serverMapper.MapAsync<Destination>(_source);

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_Client_Async() => await _clientMapper.MapAsync<Source, Destination>(_source);

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_Server_Async() => await _serverMapper.MapAsync<Source, Destination>(_source);

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_ExistingDestination_Client_Async() => await _clientMapper.MapAsync(_source, CreateDestination(100));

	[Benchmark]
	public async Task<Destination> MapAsync_GenericSource_ExistingDestination_Server_Async() => await _serverMapper.MapAsync(_source, CreateDestination(100));

	[Benchmark]
	public async Task<IReadOnlyCollection<Destination>> MapAllAsync_ObjectSource_Client_Async() => await _clientMapper.MapAllAsync<Destination>(_sourceList);

	[Benchmark]
	public async Task<IReadOnlyCollection<Destination>> MapAllAsync_ObjectSource_Server_Async() => await _serverMapper.MapAllAsync<Destination>(_sourceList);

	[Benchmark]
	public async Task<IReadOnlyCollection<Destination>> MapAllAsync_GenericSourceDestination_Client_Async() => await _clientMapper.MapAllAsync<Source, Destination>(_sourceList);

	[Benchmark]
	public async Task<IReadOnlyCollection<Destination>> MapAllAsync_GenericSourceDestination_Server_Async() => await _serverMapper.MapAllAsync<Source, Destination>(_sourceList);

	private static UmbrellaMapper CreateMapper(MapperlyEnvironmentType environmentType)
	{
		UmbrellaMapperOptions options = new()
		{
			Environment = environmentType,
			TargetAssemblies = new[] { Assembly.GetExecutingAssembly() }
		};

		var logger = CoreUtilitiesMocks.CreateLogger<UmbrellaMapper>();

		ServiceCollection services = new();
		var provider = services.BuildServiceProvider();

		return new UmbrellaMapper(logger, options, provider);
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