using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

namespace Umbrella.Utilities.Mapping.Mapperly.Test;

public class UmbrellaMapperTest
{
	public static object[][] MapAsync_Source_Data { get; } =
	[
		[CreateMapper(), CreateSource(1), CreateDestination(1)],
	];

	public static object[][] MapAsync_SourceDestination_Data { get; } =
	[
		[CreateMapper(), CreateSource(1), CreateDestination(100), CreateDestination(1)]
	];

	public static object[][] MapAsync_SourceCollection_Data { get; } =
	[
		[CreateMapper(), Enumerable.Range(0, 100).Select(x => CreateSource(x)), Enumerable.Range(0, 100).Select(x => CreateDestination(x))]
	];

	[Theory]
	[MemberData(nameof(MapAsync_Source_Data))]
	public async Task MapAsync_GenericSource_ValidAsync(IUmbrellaMapper mapper, Source source, Destination expectedDestination)
	{
		Guard.IsNotNull(mapper);
		Guard.IsNotNull(source);
		Guard.IsNotNull(expectedDestination);

		Destination destination = await mapper.MapAsync<Source, Destination>(source, TestContext.Current.CancellationToken);

		AssertExpectedDestinationEquality(expectedDestination, destination);
	}

	[Theory]
	[MemberData(nameof(MapAsync_SourceDestination_Data))]
	public async Task MapAsync_GenericSource_ExistingDestination_ValidAsync(IUmbrellaMapper mapper, Source source, Destination existingDestination, Destination expectedDestination)
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
	public async Task MapAllAsync_GenericSourceDestination_ValidAsync(IUmbrellaMapper mapper, IEnumerable<Source> lstSource, IEnumerable<Destination> lstExpectedDestination)
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

	private static IUmbrellaMapper CreateMapper()
	{
		ServiceCollection services = new();
		_ = services.AddSingleton<ILogger<UmbrellaMapper>>(CoreUtilitiesMocks.CreateLogger<UmbrellaMapper>());
		_ = services.AddUmbrellaUtilitiesMappingMapperly(MapperCatalog.Instance);

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

	private sealed class MapperCatalog : IUmbrellaMapperlyCatalog
	{
		public static IUmbrellaMapperlyCatalog Instance { get; } = new MapperCatalog();

		public void AddServices(IServiceCollection services)
			=> services.AddSingleton<Mapper>();

		public void AddMappings(UmbrellaMapperRegistryBuilder builder)
		{
			_ = builder.AddNewInstance<Mapper, Source, Destination>();
			_ = builder.AddNewCollection<Mapper, Source, Destination>();
			_ = builder.AddExistingInstance<Mapper, Source, Destination>();
		}
	}
}