using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;
using Xunit;

namespace Umbrella.Utilities.Mapping.Mapperly.Test;

public class UmbrellaMapperRegistryBuilderTest
{
	[Fact]
	public void AddNewInstance_DuplicateRegistration_ThrowsInvalidOperationException()
	{
		UmbrellaMapperRegistryBuilder builder = new();
		_ = builder.AddNewInstance<SyncAlphaMapper, AlphaSource, SharedDestination>();

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
			builder.AddAsyncNewInstance<AsyncAlphaMapper, AlphaSource, SharedDestination>());

		Assert.Contains(typeof(AsyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
		Assert.Contains(typeof(SyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void AddNewCollection_DuplicateRegistration_ThrowsInvalidOperationException()
	{
		UmbrellaMapperRegistryBuilder builder = new();
		_ = builder.AddNewCollection<SyncAlphaMapper, AlphaSource, SharedDestination>();

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
			builder.AddAsyncNewCollection<AsyncAlphaMapper, AlphaSource, SharedDestination>());

		Assert.Contains(typeof(AsyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
		Assert.Contains(typeof(SyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void AddExistingInstance_DuplicateRegistration_ThrowsInvalidOperationException()
	{
		UmbrellaMapperRegistryBuilder builder = new();
		_ = builder.AddExistingInstance<SyncAlphaMapper, AlphaSource, SharedDestination>();

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
			builder.AddAsyncExistingInstance<AsyncAlphaMapper, AlphaSource, SharedDestination>());

		Assert.Contains(typeof(AsyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
		Assert.Contains(typeof(SyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Build_MultipleMappingsForTheSameDestination_ComposesAllDispatcherKindsAsync()
	{
		UmbrellaMapperRegistryBuilder builder = new();
		_ = builder
			.AddNewInstance<SyncAlphaMapper, AlphaSource, SharedDestination>()
			.AddNewInstance<SyncBetaMapper, BetaSource, SharedDestination>()
			.AddNewCollection<SyncAlphaMapper, AlphaSource, SharedDestination>()
			.AddNewCollection<SyncBetaMapper, BetaSource, SharedDestination>()
			.AddExistingInstance<SyncAlphaMapper, AlphaSource, SharedDestination>()
			.AddExistingInstance<SyncBetaMapper, BetaSource, SharedDestination>();

		UmbrellaMapperRegistry registry = builder.Build();
		using ServiceProvider serviceProvider = CreateServiceCollection()
			.AddSingleton<SyncAlphaMapper>()
			.AddSingleton<SyncBetaMapper>()
			.BuildServiceProvider();

		Assert.True(registry.TryMapNewInstanceObject(serviceProvider, new AlphaSource { Value = "one" }, TestContext.Current.CancellationToken, out ValueTask<SharedDestination> alphaNewTask));
		Assert.True(registry.TryMapNewInstanceObject(serviceProvider, new BetaSource { Value = "two" }, TestContext.Current.CancellationToken, out ValueTask<SharedDestination> betaNewTask));

		Assert.Equal("alpha:one", (await alphaNewTask).Value);
		Assert.Equal("beta:two", (await betaNewTask).Value);

		Assert.True(registry.TryMapNewCollectionObject(serviceProvider, new AlphaSource[] { new() { Value = "one" }, new() { Value = "three" } }, TestContext.Current.CancellationToken, out ValueTask<IReadOnlyCollection<SharedDestination>> alphaCollectionTask));
		Assert.True(registry.TryMapNewCollectionObject(serviceProvider, new BetaSource[] { new() { Value = "two" } }, TestContext.Current.CancellationToken, out ValueTask<IReadOnlyCollection<SharedDestination>> betaCollectionTask));

		Assert.Collection(
			await alphaCollectionTask,
			item => Assert.Equal("alpha:one", item.Value),
			item => Assert.Equal("alpha:three", item.Value));
		Assert.Collection(
			await betaCollectionTask,
			item => Assert.Equal("beta:two", item.Value));

		SharedDestination alphaExistingDestination = new() { Value = "initial-alpha" };
		SharedDestination betaExistingDestination = new() { Value = "initial-beta" };

		Assert.True(registry.TryMapExistingInstanceObject(serviceProvider, new AlphaSource { Value = "updated-alpha" }, alphaExistingDestination, TestContext.Current.CancellationToken, out ValueTask<SharedDestination> alphaExistingTask));
		Assert.True(registry.TryMapExistingInstanceObject(serviceProvider, new BetaSource { Value = "updated-beta" }, betaExistingDestination, TestContext.Current.CancellationToken, out ValueTask<SharedDestination> betaExistingTask));

		Assert.Same(alphaExistingDestination, await alphaExistingTask);
		Assert.Same(betaExistingDestination, await betaExistingTask);
		Assert.Equal("alpha:updated-alpha", alphaExistingDestination.Value);
		Assert.Equal("beta:updated-beta", betaExistingDestination.Value);
	}

	[Fact]
	public async Task AddUmbrellaUtilitiesMappingMapperly_MultipleCatalogs_ComposesMappingsAndReplacesExistingMapperAsync()
	{
		StubUmbrellaMapper existingMapper = new();
		ServiceCollection services = CreateServiceCollection();
		_ = services.AddSingleton<IUmbrellaMapper>(existingMapper);

		IServiceCollection returnedServices = services.AddUmbrellaUtilitiesMappingMapperly(new AlphaCatalog(), new BetaCatalog());

		Assert.Same(services, returnedServices);
		Assert.Single(services, x => x.ServiceType == typeof(IUmbrellaMapper));

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IUmbrellaMapper mapper = serviceProvider.GetRequiredService<IUmbrellaMapper>();

		Assert.IsType<UmbrellaMapper>(mapper);
		Assert.NotSame(existingMapper, mapper);

		SharedDestination alphaDestination = await mapper.MapAsync<SharedDestination>(new AlphaSource { Value = "one" }, TestContext.Current.CancellationToken);
		SharedDestination betaDestination = await mapper.MapAsync<SharedDestination>(new BetaSource { Value = "two" }, TestContext.Current.CancellationToken);
		IReadOnlyCollection<SharedDestination> betaCollection = await mapper.MapAllAsync<BetaSource, SharedDestination>([new BetaSource { Value = "three" }], TestContext.Current.CancellationToken);

		SharedDestination existingDestination = new() { Value = "initial" };
		SharedDestination mappedExistingDestination = await mapper.MapAsync(new AlphaSource { Value = "four" }, existingDestination, TestContext.Current.CancellationToken);

		Assert.Equal("alpha:one", alphaDestination.Value);
		Assert.Equal("beta:two", betaDestination.Value);
		Assert.Collection(betaCollection, item => Assert.Equal("beta:three", item.Value));
		Assert.Same(existingDestination, mappedExistingDestination);
		Assert.Equal("alpha:four", existingDestination.Value);
	}

	[Fact]
	public void AddUmbrellaUtilitiesMappingMapperly_DuplicateMappingsAcrossCatalogs_ThrowsInvalidOperationException()
	{
		ServiceCollection services = CreateServiceCollection();

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
			services.AddUmbrellaUtilitiesMappingMapperly(new AlphaCatalog(), new DuplicateAlphaCatalog()));

		Assert.Contains(typeof(DuplicateAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
		Assert.Contains(typeof(SyncAlphaMapper).FullName!, exception.Message, StringComparison.Ordinal);
	}

	private static ServiceCollection CreateServiceCollection()
	{
		ServiceCollection services = new();
		_ = services.AddSingleton<ILogger<UmbrellaMapper>>(CoreUtilitiesMocks.CreateLogger<UmbrellaMapper>());
		return services;
	}

	public sealed record AlphaSource
	{
		public required string Value { get; init; }
	}

	public sealed record BetaSource
	{
		public required string Value { get; init; }
	}

	public sealed record SharedDestination
	{
		public required string Value { get; set; }
	}

	public sealed class SyncAlphaMapper : IUmbrellaMapperlyMapper<AlphaSource, SharedDestination>
	{
		public SharedDestination Map(AlphaSource source) => new() { Value = $"alpha:{source.Value}" };

		public IReadOnlyCollection<SharedDestination> MapAll(IEnumerable<AlphaSource> source)
			=> source.Select(Map).ToArray();

		public void Map(AlphaSource source, SharedDestination destination) => destination.Value = $"alpha:{source.Value}";
	}

	public sealed class AsyncAlphaMapper :
		IUmbrellaMapperlyNewInstanceAsyncMapper<AlphaSource, SharedDestination>,
		IUmbrellaMapperlyNewCollectionAsyncMapper<AlphaSource, SharedDestination>,
		IUmbrellaMapperlyExistingInstanceAsyncMapper<AlphaSource, SharedDestination>
	{
		public ValueTask<SharedDestination> MapAsync(AlphaSource source, CancellationToken cancellationToken)
			=> ValueTask.FromResult(new SharedDestination { Value = $"alpha:{source.Value}" });

		public ValueTask<IReadOnlyCollection<SharedDestination>> MapAllAsync(IEnumerable<AlphaSource> source, CancellationToken cancellationToken)
			=> ValueTask.FromResult<IReadOnlyCollection<SharedDestination>>(source.Select(x => new SharedDestination { Value = $"alpha:{x.Value}" }).ToArray());

		public ValueTask MapAsync(AlphaSource source, SharedDestination destination, CancellationToken cancellationToken)
		{
			destination.Value = $"alpha:{source.Value}";
			return ValueTask.CompletedTask;
		}
	}

	public sealed class SyncBetaMapper : IUmbrellaMapperlyMapper<BetaSource, SharedDestination>
	{
		public SharedDestination Map(BetaSource source) => new() { Value = $"beta:{source.Value}" };

		public IReadOnlyCollection<SharedDestination> MapAll(IEnumerable<BetaSource> source)
			=> source.Select(Map).ToArray();

		public void Map(BetaSource source, SharedDestination destination) => destination.Value = $"beta:{source.Value}";
	}

	public sealed class DuplicateAlphaMapper : IUmbrellaMapperlyNewInstanceMapper<AlphaSource, SharedDestination>
	{
		public SharedDestination Map(AlphaSource source) => new() { Value = $"duplicate:{source.Value}" };
	}

	public sealed class AlphaCatalog : IUmbrellaMapperlyCatalog
	{
		public void AddServices(IServiceCollection services)
		{
			_ = services.AddSingleton<SyncAlphaMapper>();
		}

		public void AddMappings(UmbrellaMapperRegistryBuilder builder)
		{
			_ = builder
				.AddNewInstance<SyncAlphaMapper, AlphaSource, SharedDestination>()
				.AddNewCollection<SyncAlphaMapper, AlphaSource, SharedDestination>()
				.AddExistingInstance<SyncAlphaMapper, AlphaSource, SharedDestination>();
		}
	}

	public sealed class BetaCatalog : IUmbrellaMapperlyCatalog
	{
		public void AddServices(IServiceCollection services)
		{
			_ = services.AddSingleton<SyncBetaMapper>();
		}

		public void AddMappings(UmbrellaMapperRegistryBuilder builder)
		{
			_ = builder
				.AddNewInstance<SyncBetaMapper, BetaSource, SharedDestination>()
				.AddNewCollection<SyncBetaMapper, BetaSource, SharedDestination>()
				.AddExistingInstance<SyncBetaMapper, BetaSource, SharedDestination>();
		}
	}

	public sealed class DuplicateAlphaCatalog : IUmbrellaMapperlyCatalog
	{
		public void AddServices(IServiceCollection services)
		{
			_ = services.AddSingleton<DuplicateAlphaMapper>();
		}

		public void AddMappings(UmbrellaMapperRegistryBuilder builder)
		{
			_ = builder.AddNewInstance<DuplicateAlphaMapper, AlphaSource, SharedDestination>();
		}
	}

	private sealed class StubUmbrellaMapper : IUmbrellaMapper
	{
		public ValueTask<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TDestination>(IEnumerable<object> source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}
}
