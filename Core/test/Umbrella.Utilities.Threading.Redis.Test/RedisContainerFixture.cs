using Testcontainers.Redis;

namespace Umbrella.Utilities.Threading.Redis.Test;

public sealed class RedisContainerFixture : IAsyncLifetime
{
	private const string RedisImage = "redis:7.0";
	private readonly RedisContainer _container = new RedisBuilder(RedisImage).Build();

	public string ConnectionString => _container.GetConnectionString();

	public async ValueTask InitializeAsync() => await _container.StartAsync();

	public async ValueTask DisposeAsync()
	{
		await _container.DisposeAsync();
		GC.SuppressFinalize(this);
	}
}
