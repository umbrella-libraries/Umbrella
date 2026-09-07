using Umbrella.Utilities.Threading.Redis.Options;

namespace Umbrella.Utilities.Threading.Redis.Test;

public sealed class DistributedRedisSynchronizationManagerOptionsTest
{
	[Fact]
	public void Sanitize_TrimsConnectionString()
	{
		var options = new DistributedRedisSynchronizationManagerOptions { ConnectionString = "  localhost:6379  " };

		options.Sanitize();

		Assert.Equal("localhost:6379", options.ConnectionString);
	}

	[Fact]
	public void Validate_WhenConnectionStringIsEmpty_ThrowsArgumentException()
	{
		var options = new DistributedRedisSynchronizationManagerOptions { ConnectionString = "" };

		_ = Assert.Throws<ArgumentException>(options.Validate);
	}
}
