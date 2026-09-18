using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.Threading.RateLimiting;
using Umbrella.Utilities.Threading.RateLimiting.Abstractions;
using Umbrella.Utilities.Threading.RateLimiting.Exceptions;

namespace Umbrella.Utilities.Test.Threading.RateLimiting;

public class RateLimitManagerTest
{
	private static readonly RateLimitPolicy _policy = new(
		new RateLimitWindow(5, TimeSpan.FromMinutes(1)),
		new RateLimitWindow(20, TimeSpan.FromDays(1), RateLimitWindowAlignment.UtcAligned));

	[Fact]
	public async Task TryAcquireAsync_ForwardsKeysPolicyAndUtcNow()
	{
		DateTime utcNow = new(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
		var store = new StubStore
		{
			Handler = (partitionKey, resourceKey, policy, suppliedUtcNow, _) =>
			{
				Assert.Equal(42, partitionKey);
				Assert.Equal("CareerSearch", resourceKey);
				Assert.Same(_policy, policy);
				Assert.Equal(utcNow, suppliedUtcNow);
				return Task.FromResult(RateLimitAcquisitionResult.Acquired);
			}
		};
		var manager = new RateLimitManager<int, string>(store, new StubDateTimeProvider(utcNow));

		RateLimitAcquisitionResult result = await manager.TryAcquireAsync(42, "CareerSearch", _policy, TestContext.Current.CancellationToken);

		Assert.True(result.IsAcquired);
	}

	[Fact]
	public async Task TryAcquireAsync_StoreFailure_ThrowsUnavailableException()
	{
		var store = new StubStore
		{
			Handler = (_, _, _, _, _) => throw new InvalidOperationException("Database unavailable.")
		};
		var manager = new RateLimitManager<int, string>(store, new StubDateTimeProvider(DateTime.UtcNow));

		RateLimitUnavailableException exception = await Assert.ThrowsAsync<RateLimitUnavailableException>(
			() => manager.TryAcquireAsync(42, "CareerSearch", _policy, TestContext.Current.CancellationToken));

		_ = Assert.IsType<InvalidOperationException>(exception.InnerException);
	}

	[Fact]
	public async Task TryAcquireAsync_Cancellation_IsNotWrapped()
	{
		var store = new StubStore
		{
			Handler = (_, _, _, _, cancellationToken) => throw new OperationCanceledException(cancellationToken)
		};
		var manager = new RateLimitManager<int, string>(store, new StubDateTimeProvider(DateTime.UtcNow));

		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => manager.TryAcquireAsync(42, "CareerSearch", _policy, TestContext.Current.CancellationToken));
	}

	[Fact]
	public void RateLimitPolicy_QuotaWindowNotLonger_Throws()
	{
		var burst = new RateLimitWindow(5, TimeSpan.FromMinutes(1));
		var quota = new RateLimitWindow(20, TimeSpan.FromMinutes(1));

		_ = Assert.Throws<ArgumentException>(() => new RateLimitPolicy(burst, quota));
	}

	private sealed class StubStore : IRateLimitStore<int, string>
	{
		public required Func<int, string, RateLimitPolicy, DateTime, CancellationToken, Task<RateLimitAcquisitionResult>> Handler { get; init; }

		public Task<RateLimitAcquisitionResult> TryAcquireAsync(int partitionKey, string resourceKey, RateLimitPolicy policy, DateTime utcNow, CancellationToken cancellationToken = default)
			=> Handler(partitionKey, resourceKey, policy, utcNow, cancellationToken);
	}

	private sealed class StubDateTimeProvider(DateTime utcNow) : IDateTimeProvider
	{
		public DateTime Now => utcNow.ToLocalTime();
		public DateTime UtcNow => utcNow;
		public DateTime Today => Now.Date;
	}
}
