using System.Reflection;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mapping.Mapperly.Enumerations;
using Umbrella.Utilities.Mapping.Mapperly.Options;
using Xunit;

namespace Umbrella.Utilities.Mapping.Mapperly.Test;

public class UmbrellaMapperTest
{
	public static object[][] MapAsync_Source_Data { get; } =
	[
		[CreateMapper(MapperlyEnvironmentType.Client), CreateSource(1), CreateDestination(1)],
		[CreateMapper(MapperlyEnvironmentType.Server), CreateSource(1), CreateDestination(1)],
	];

	public static object[][] MapAsync_SourceDestination_Data { get; } =
	[
		[CreateMapper(MapperlyEnvironmentType.Client), CreateSource(1), CreateDestination(100), CreateDestination(1)],
		[CreateMapper(MapperlyEnvironmentType.Server), CreateSource(1), CreateDestination(100), CreateDestination(1)]
	];

	public static object[][] MapAsync_SourceCollection_Data { get; } =
	[
		[CreateMapper(MapperlyEnvironmentType.Client), Enumerable.Range(0, 100).Select(x => CreateSource(x)), Enumerable.Range(0, 100).Select(x => CreateDestination(x))],
		[CreateMapper(MapperlyEnvironmentType.Server), Enumerable.Range(0, 100).Select(x => CreateSource(x)), Enumerable.Range(0, 100).Select(x => CreateDestination(x))]
	];

	[Theory]
	[MemberData(nameof(MapAsync_Source_Data))]
	public async Task MapAsync_ObjectSource_ValidAsync(UmbrellaMapper mapper, Source source, Destination expectedDestination)
	{
		Guard.IsNotNull(mapper);
		Guard.IsNotNull(source);
		Guard.IsNotNull(expectedDestination);

		Destination destination = await mapper.MapAsync<Destination>(source, TestContext.Current.CancellationToken);

		AssertExpectedDestinationEquality(expectedDestination, destination);
	}

	[Theory]
	[MemberData(nameof(MapAsync_Source_Data))]
	public async Task MapAsync_GenericSource_ValidAsync(UmbrellaMapper mapper, Source source, Destination expectedDestination)
	{
		Guard.IsNotNull(mapper);
		Guard.IsNotNull(source);
		Guard.IsNotNull(expectedDestination);

		Destination destination = await mapper.MapAsync<Source, Destination>(source, TestContext.Current.CancellationToken);

		AssertExpectedDestinationEquality(expectedDestination, destination);
	}

	[Theory]
	[MemberData(nameof(MapAsync_SourceDestination_Data))]
	public async Task MapAsync_GenericSource_ExistingDestination_ValidAsync(UmbrellaMapper mapper, Source source, Destination existingDestination, Destination expectedDestination)
	{
		Guard.IsNotNull(mapper);
		Guard.IsNotNull(source);
		Guard.IsNotNull(existingDestination);
		Guard.IsNotNull(expectedDestination);

		Destination destination = await mapper.MapAsync(source, existingDestination, TestContext.Current.CancellationToken);

		Assert.Same(destination, existingDestination);
		AssertExpectedDestinationEquality(expectedDestination, existingDestination);
		AssertExpectedDestinationEquality(expectedDestination, destination);
	}

	[Theory]
	[MemberData(nameof(MapAsync_SourceCollection_Data))]
	public async Task MapAllAsync_ObjectSource_ValidAsync(UmbrellaMapper mapper, IEnumerable<Source> lstSource, IEnumerable<Destination> lstExpectedDestination)
	{
		Guard.IsNotNull(mapper);

		IReadOnlyCollection<Destination> lstDestination = await mapper.MapAllAsync<Destination>(lstSource, TestContext.Current.CancellationToken);

		Assert.Equal(lstExpectedDestination.Count(), lstDestination.Count);

		foreach (var (expectedDestination, destination) in lstDestination.Zip(lstExpectedDestination))
		{
			AssertExpectedDestinationEquality(expectedDestination, destination);
		}
	}

	[Theory]
	[MemberData(nameof(MapAsync_SourceCollection_Data))]
	public async Task MapAllAsync_GenericSourceDestination_ValidAsync(UmbrellaMapper mapper, IEnumerable<Source> lstSource, IEnumerable<Destination> lstExpectedDestination)
	{
		Guard.IsNotNull(mapper);

		IReadOnlyCollection<Destination> lstDestination = await mapper.MapAllAsync<Source, Destination>(lstSource, TestContext.Current.CancellationToken);

		Assert.Equal(lstExpectedDestination.Count(), lstDestination.Count);

		foreach (var (expectedDestination, destination) in lstDestination.Zip(lstExpectedDestination))
		{
			AssertExpectedDestinationEquality(expectedDestination, destination);
		}
	}

	private static void AssertExpectedDestinationEquality(Destination expectedDestination, Destination destination)
	{
		Assert.Equal(expectedDestination.ToString(), destination.ToString());

		Guard.IsNotNull(expectedDestination.Children);
		Guard.IsNotNull(destination.Children);

		for (int i = 0; i < expectedDestination.Children.Count; i++)
		{
			Assert.Equal(expectedDestination.Children.ElementAt(i).ToString(), destination.Children.ElementAt(i).ToString());
		}
	}

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