using System.Diagnostics.CodeAnalysis;

namespace Umbrella.Utilities.Threading.RateLimiting.Exceptions;

/// <summary>
/// The exception thrown when an operation cannot acquire a rate-limit permit.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "The retry date is required for this specialized exception.")]
public sealed class RateLimitExceededException : Exception
{
	/// <summary>
	/// Gets the earliest UTC date at which the operation can be retried.
	/// </summary>
	public DateTime RetryAfterDateUtc { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitExceededException"/> class.
	/// </summary>
	public RateLimitExceededException(string message, DateTime retryAfterDateUtc)
		: base(message)
	{
		if (retryAfterDateUtc.Kind is not DateTimeKind.Utc)
			throw new ArgumentException("The retry date must use UTC.", nameof(retryAfterDateUtc));

		RetryAfterDateUtc = retryAfterDateUtc;
	}
}
