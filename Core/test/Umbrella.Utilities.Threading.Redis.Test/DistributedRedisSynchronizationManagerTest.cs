using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;
using Umbrella.Utilities.Threading.Redis.Options;

namespace Umbrella.Utilities.Threading.Redis.Test;

public sealed class DistributedRedisSynchronizationManagerTest : IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _fixture;

	public DistributedRedisSynchronizationManagerTest(RedisContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(fixture);
		_fixture = fixture;
	}

	[Fact]
	public async Task GetSynchronizationRootAndWaitAsync_AcquiresAndReleasesLock()
	{
		await using DistributedRedisSynchronizationManager manager = CreateManager();

		await using ISynchronizationRoot root = await manager.GetSynchronizationRootAndWaitAsync<string>(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

		Assert.NotNull(root);
	}

	[Fact]
	public async Task GetSynchronizationRootAndWaitAsync_SameKeyAcrossManagers_WaitsForRelease()
	{
		string key = Guid.NewGuid().ToString();
		await using DistributedRedisSynchronizationManager firstManager = CreateManager();
		await using DistributedRedisSynchronizationManager secondManager = CreateManager();
		ISynchronizationRoot firstRoot = await firstManager.GetSynchronizationRootAndWaitAsync<string>(key, TestContext.Current.CancellationToken);

		Task<ISynchronizationRoot> pendingRoot = secondManager.GetSynchronizationRootAndWaitAsync<string>(key, TestContext.Current.CancellationToken).AsTask();
		await Task.Delay(250, TestContext.Current.CancellationToken);

		Assert.False(pendingRoot.IsCompleted);

		await firstRoot.DisposeAsync();
		await using ISynchronizationRoot secondRoot = await pendingRoot.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		Assert.NotNull(secondRoot);
	}

	[Fact]
	public async Task GetSynchronizationRootAndWaitAsync_WhenWaitingIsCancelled_ThrowsOperationCancelledException()
	{
		string key = Guid.NewGuid().ToString();
		await using DistributedRedisSynchronizationManager firstManager = CreateManager();
		await using DistributedRedisSynchronizationManager secondManager = CreateManager();
		await using ISynchronizationRoot firstRoot = await firstManager.GetSynchronizationRootAndWaitAsync<string>(key, TestContext.Current.CancellationToken);
		using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => secondManager.GetSynchronizationRootAndWaitAsync<string>(key, cancellationTokenSource.Token).AsTask());
	}

	[Fact]
	public async Task GetSynchronizationRootAndWaitAsync_SameKeyForDifferentTypes_AcquiresIndependently()
	{
		string key = Guid.NewGuid().ToString();
		await using DistributedRedisSynchronizationManager manager = CreateManager();
		await using ISynchronizationRoot stringRoot = await manager.GetSynchronizationRootAndWaitAsync<string>(key, TestContext.Current.CancellationToken);
		await using ISynchronizationRoot integerRoot = await manager.GetSynchronizationRootAndWaitAsync<int>(key, TestContext.Current.CancellationToken);

		Assert.NotNull(integerRoot);
	}

	[Fact]
	public void AddUmbrellaDistributedRedisLock_RegistersManager()
	{
		var services = new ServiceCollection();
		_ = services.AddUmbrellaUtilities();
		_ = services.AddUmbrellaDistributedRedisLock((_, options) => options.ConnectionString = _fixture.ConnectionString);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		_ = Assert.IsType<DistributedRedisSynchronizationManager>(serviceProvider.GetRequiredService<ISynchronizationManager>());
	}

	private DistributedRedisSynchronizationManager CreateManager() => new(
		NullLogger<DistributedRedisSynchronizationManager>.Instance,
		new DistributedRedisSynchronizationManagerOptions { ConnectionString = _fixture.ConnectionString });
}
