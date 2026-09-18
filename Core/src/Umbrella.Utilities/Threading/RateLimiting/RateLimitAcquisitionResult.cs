namespace Umbrella.Utilities.Threading.RateLimiting;

/// <summary>
/// Represents the result of an atomic rate-limit acquisition.
/// </summary>
public sealed class RateLimitAcquisitionResult
{
	/// <summary>
	/// Gets a successful acquisition result.
	/// </summary>
	public static RateLimitAcquisitionResult Acquired { get; } = new(true, null);

	/// <summary>
	/// Gets a value indicating whether a permit was acquired.
	/// </summary>
	public bool IsAcquired { get; }

	/// <summary>
	/// Gets the earliest UTC date at which the rejected operation can be retried.
	/// </summary>
	public DateTime? RetryAfterDateUtc { get; }

	private RateLimitAcquisitionResult(bool isAcquired, DateTime? retryAfterDateUtc)
	{
		IsAcquired = isAcquired;
		RetryAfterDateUtc = retryAfterDateUtc;
	}

	/// <summary>
	/// Creates a rejected acquisition result.
	/// </summary>
	public static RateLimitAcquisitionResult Rejected(DateTime retryAfterDateUtc)
	{
		if (retryAfterDateUtc.Kind is not DateTimeKind.Utc)
			throw new ArgumentException("The retry date must use UTC.", nameof(retryAfterDateUtc));

		return new RateLimitAcquisitionResult(false, retryAfterDateUtc);
	}
}
