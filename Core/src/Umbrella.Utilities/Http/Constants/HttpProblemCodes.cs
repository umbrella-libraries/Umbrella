namespace Umbrella.Utilities.Http.Constants;

/// <summary>
/// Contains core Http Problem Codes.
/// </summary>
public static class HttpProblemCodes
{
	/// <summary>
	/// Indicates that there was a concurrency stamp mismatch.
	/// </summary>
	public const string ConcurrencyStampMismatch = nameof(ConcurrencyStampMismatch);

	/// <summary>
	/// Indicates that a rate limit has been exceeded.
	/// </summary>
	public const string RateLimitExceeded = nameof(RateLimitExceeded);

	/// <summary>
	/// Indicates that authoritative rate-limit state is unavailable.
	/// </summary>
	public const string RateLimitUnavailable = nameof(RateLimitUnavailable);
}