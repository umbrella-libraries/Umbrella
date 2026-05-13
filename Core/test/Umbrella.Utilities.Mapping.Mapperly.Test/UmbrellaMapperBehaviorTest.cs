using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;
using Xunit;

namespace Umbrella.Utilities.Mapping.Mapperly.Test;

public class UmbrellaMapperBehaviorTest
{
	[Fact]
	public async Task MapAsync_ObjectSource_MissingMapper_ThrowsExpectedErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		UnmappedSource source = new("missing");
		string sourceTypeName = typeof(UnmappedSource).FullName!;
		string destinationTypeName = typeof(BehaviorDestination).FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAsync<BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		Assert.Equal("There has been a problem mapping the object.", exception.Message);

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(destinationTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains("trying to map to a new instance", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAsync_GenericSource_MissingMapper_ThrowsExpectedErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		UnmappedSource source = new("missing");
		string sourceTypeName = typeof(UnmappedSource).FullName!;
		string destinationTypeName = typeof(BehaviorDestination).FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAsync<UnmappedSource, BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		Assert.Equal("There has been a problem mapping the object.", exception.Message);

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(destinationTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains("trying to map to a new instance", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAsync_ObjectSource_Collection_ThrowsGuidanceErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		IEnumerable<object> source = new List<DerivedBehaviorSource> { new("first") };
		string sourceCollectionTypeName = source.GetType().FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAsync<BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceCollectionTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(IUmbrellaMapper.MapAllAsync), innerException.Message, StringComparison.Ordinal);
		Assert.Contains("which is a collection", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAsync_GenericSource_Collection_ThrowsGuidanceErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		List<DerivedBehaviorSource> source = [new("first")];
		string sourceCollectionTypeName = source.GetType().FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAsync<List<DerivedBehaviorSource>, BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceCollectionTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(nameof(IUmbrellaMapper.MapAllAsync), innerException.Message, StringComparison.Ordinal);
		Assert.Contains("which is a collection", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAllAsync_ObjectSource_MissingMapper_ThrowsExpectedErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		IEnumerable<object> source = new List<UnmappedSource> { new("missing") };
		string sourceCollectionTypeName = source.GetType().FullName!;
		string destinationTypeName = typeof(BehaviorDestination).FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAllAsync<BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		Assert.Equal("There has been a problem mapping the object.", exception.Message);

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceCollectionTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(destinationTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains("trying to map to a new collection", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAllAsync_GenericSource_MissingMapper_ThrowsExpectedErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		IEnumerable<UnmappedSource> source = [new("missing")];
		string sourceTypeName = typeof(UnmappedSource).FullName!;
		string destinationTypeName = typeof(BehaviorDestination).FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAllAsync<UnmappedSource, BehaviorDestination>(source, TestContext.Current.CancellationToken).AsTask());

		Assert.Equal("There has been a problem mapping the object.", exception.Message);

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(destinationTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains("trying to map to a new collection", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAsync_ExistingDestination_MissingMapper_ThrowsExpectedErrorAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		UnmappedSource source = new("missing");
		BehaviorDestination destination = new();
		string sourceTypeName = typeof(UnmappedSource).FullName!;
		string destinationTypeName = typeof(BehaviorDestination).FullName!;

		UmbrellaMappingException exception = await Assert.ThrowsAsync<UmbrellaMappingException>(
			() => mapper.MapAsync(source, destination, TestContext.Current.CancellationToken).AsTask());

		Assert.Equal("There has been a problem mapping the object.", exception.Message);

		InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
		Assert.Contains(sourceTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains(destinationTypeName, innerException.Message, StringComparison.Ordinal);
		Assert.Contains("trying to map to an existing instance", innerException.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task MapAsync_ObjectSource_PrefersAsyncMapperWhenBothInterfacesExistAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		DerivedBehaviorSource source = new("async-new");

		BehaviorDestination destination = await mapper.MapAsync<BehaviorDestination>(source, TestContext.Current.CancellationToken);

		Assert.Equal("async-new", destination.Value);
		Assert.Equal("async-new-instance", destination.Mode);
	}

	[Fact]
	public async Task MapAsync_GenericBaseSource_FallsBackToObjectDispatchAndPrefersAsyncMapperAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		BaseBehaviorSource source = new DerivedBehaviorSource("async-new-dispatch");

		BehaviorDestination destination = await mapper.MapAsync<BaseBehaviorSource, BehaviorDestination>(source, TestContext.Current.CancellationToken);

		Assert.Equal("async-new-dispatch", destination.Value);
		Assert.Equal("async-new-instance", destination.Mode);
	}

	[Fact]
	public async Task MapAllAsync_GenericBaseSource_FallsBackToObjectDispatchAndPrefersAsyncMapperAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		IEnumerable<BaseBehaviorSource> source = new List<DerivedBehaviorSource>
		{
			new("first"),
			new("second")
		};

		IReadOnlyCollection<BehaviorDestination> destination = await mapper.MapAllAsync<BaseBehaviorSource, BehaviorDestination>(source, TestContext.Current.CancellationToken);

		Assert.Collection(
			destination,
			item =>
			{
				Assert.Equal("first", item.Value);
				Assert.Equal("async-new-collection", item.Mode);
			},
			item =>
			{
				Assert.Equal("second", item.Value);
				Assert.Equal("async-new-collection", item.Mode);
			});
	}

	[Fact]
	public async Task MapAsync_GenericBaseSource_ExistingDestination_FallsBackToObjectDispatchAndPrefersAsyncMapperAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		BaseBehaviorSource source = new DerivedBehaviorSource("async-existing-dispatch");
		BehaviorDestination existingDestination = new();

		BehaviorDestination destination = await mapper.MapAsync(source, existingDestination, TestContext.Current.CancellationToken);

		Assert.Same(existingDestination, destination);
		Assert.Equal("async-existing-dispatch", destination.Value);
		Assert.Equal("async-existing-instance", destination.Mode);
	}

	[Fact]
	public async Task MapAsync_ExactGenericSource_PrefersAsyncMapperWhenBothInterfacesExistAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		DerivedBehaviorSource source = new("exact-async-new");

		BehaviorDestination destination = await mapper.MapAsync<DerivedBehaviorSource, BehaviorDestination>(source, TestContext.Current.CancellationToken);

		Assert.Equal("exact-async-new", destination.Value);
		Assert.Equal("async-new-instance", destination.Mode);
	}

	[Fact]
	public async Task MapAllAsync_ExactGenericSource_PrefersAsyncMapperWhenBothInterfacesExistAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		DerivedBehaviorSource[] source = [new("first"), new("second")];

		IReadOnlyCollection<BehaviorDestination> destinations = await mapper.MapAllAsync<DerivedBehaviorSource, BehaviorDestination>(source, TestContext.Current.CancellationToken);

		Assert.Collection(
			destinations,
			item =>
			{
				Assert.Equal("first", item.Value);
				Assert.Equal("async-new-collection", item.Mode);
			},
			item =>
			{
				Assert.Equal("second", item.Value);
				Assert.Equal("async-new-collection", item.Mode);
			});
	}

	[Fact]
	public async Task MapAsync_ExactGenericSource_ExistingDestination_PrefersAsyncMapperWhenBothInterfacesExistAsync()
	{
		IUmbrellaMapper mapper = CreateBehaviorMapper();
		DerivedBehaviorSource source = new("exact-async-existing");
		BehaviorDestination existingDestination = new();

		BehaviorDestination destination = await mapper.MapAsync<DerivedBehaviorSource, BehaviorDestination>(source, existingDestination, TestContext.Current.CancellationToken);

		Assert.Same(existingDestination, destination);
		Assert.Equal("exact-async-existing", destination.Value);
		Assert.Equal("async-existing-instance", destination.Mode);
	}

	private static IUmbrellaMapper CreateBehaviorMapper()
	{
		ServiceCollection services = new();
		_ = services.AddSingleton<ILogger<UmbrellaMapper>>(CoreUtilitiesMocks.CreateLogger<UmbrellaMapper>());
		_ = services.AddUmbrellaUtilitiesMappingMapperly(BehaviorCatalog.Instance);

		ServiceProvider provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IUmbrellaMapper>();
	}

	internal abstract record BaseBehaviorSource(string Value);

	internal sealed record DerivedBehaviorSource(string Value) : BaseBehaviorSource(Value);

	internal sealed record UnmappedSource(string Value);

	internal sealed class BehaviorDestination
	{
		public string? Value { get; set; }

		public string? Mode { get; set; }
	}

	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by dependency injection during mapper behavior tests.")]
	internal sealed class BehaviorMapper :
		IUmbrellaMapperlyNewInstanceMapper<DerivedBehaviorSource, BehaviorDestination>,
		IUmbrellaMapperlyNewInstanceAsyncMapper<DerivedBehaviorSource, BehaviorDestination>,
		IUmbrellaMapperlyNewCollectionMapper<DerivedBehaviorSource, BehaviorDestination>,
		IUmbrellaMapperlyNewCollectionAsyncMapper<DerivedBehaviorSource, BehaviorDestination>,
		IUmbrellaMapperlyExistingInstanceMapper<DerivedBehaviorSource, BehaviorDestination>,
		IUmbrellaMapperlyExistingInstanceAsyncMapper<DerivedBehaviorSource, BehaviorDestination>
	{
		public BehaviorDestination Map(DerivedBehaviorSource source)
			=> CreateDestination(source, "sync-new-instance");

		public ValueTask<BehaviorDestination> MapAsync(DerivedBehaviorSource source, CancellationToken cancellationToken)
			=> new(CreateDestination(source, "async-new-instance"));

		public IReadOnlyCollection<BehaviorDestination> MapAll(IEnumerable<DerivedBehaviorSource> source)
			=> source.Select(x => CreateDestination(x, "sync-new-collection")).ToArray();

		public ValueTask<IReadOnlyCollection<BehaviorDestination>> MapAllAsync(IEnumerable<DerivedBehaviorSource> source, CancellationToken cancellationToken)
			=> new((IReadOnlyCollection<BehaviorDestination>)source.Select(x => CreateDestination(x, "async-new-collection")).ToArray());

		public void Map(DerivedBehaviorSource source, BehaviorDestination destination)
			=> PopulateDestination(source, destination, "sync-existing-instance");

		public ValueTask MapAsync(DerivedBehaviorSource source, BehaviorDestination destination, CancellationToken cancellationToken)
		{
			PopulateDestination(source, destination, "async-existing-instance");
			return ValueTask.CompletedTask;
		}

		private static BehaviorDestination CreateDestination(DerivedBehaviorSource source, string mode)
			=> new()
			{
				Value = source.Value,
				Mode = mode
			};

		private static void PopulateDestination(DerivedBehaviorSource source, BehaviorDestination destination, string mode)
		{
			destination.Value = source.Value;
			destination.Mode = mode;
		}
	}

	internal sealed class BehaviorCatalog : IUmbrellaMapperlyCatalog
	{
		public static IUmbrellaMapperlyCatalog Instance { get; } = new BehaviorCatalog();

		public void AddServices(IServiceCollection services)
			=> services.AddSingleton<BehaviorMapper>();

		public void AddMappings(UmbrellaMapperRegistryBuilder builder)
		{
			_ = builder.AddAsyncNewInstance<BehaviorMapper, DerivedBehaviorSource, BehaviorDestination>();
			_ = builder.AddAsyncNewCollection<BehaviorMapper, DerivedBehaviorSource, BehaviorDestination>();
			_ = builder.AddAsyncExistingInstance<BehaviorMapper, DerivedBehaviorSource, BehaviorDestination>();
		}
	}
}
