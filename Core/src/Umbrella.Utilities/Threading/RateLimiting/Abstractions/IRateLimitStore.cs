namespace Umbrella.Utilities.Threading.RateLimiting.Abstractions;

/// <summary>
/// A persistence provider that atomically enforces both windows in a <see cref="RateLimitPolicy"/>.
/// </summary>
public interface IRateLimitStore<in TPartitionKey, in TResourceKey>
	where TPartitionKey : notnull
	where TResourceKey : notnull
{
	/// <summary>
	/// Attempts to acquire one permit from both policy windows as a single atomic operation.
	/// </summary>
	Task<RateLimitAcquisitionResult> TryAcquireAsync(
		TPartitionKey partitionKey,
		TResourceKey resourceKey,
		RateLimitPolicy policy,
		DateTime utcNow,
		CancellationToken cancellationToken = default);
}
