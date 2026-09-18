namespace Umbrella.Utilities.Threading.RateLimiting.Abstractions;

/// <summary>
/// Coordinates rate-limit acquisitions using the current UTC time and a configured persistence provider.
/// </summary>
public interface IRateLimitManager<in TPartitionKey, in TResourceKey>
	where TPartitionKey : notnull
	where TResourceKey : notnull
{
	/// <summary>
	/// Attempts to acquire one permit for the specified partition and resource.
	/// </summary>
	Task<RateLimitAcquisitionResult> TryAcquireAsync(
		TPartitionKey partitionKey,
		TResourceKey resourceKey,
		RateLimitPolicy policy,
		CancellationToken cancellationToken = default);
}
