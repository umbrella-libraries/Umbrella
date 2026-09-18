using System.Diagnostics.CodeAnalysis;

namespace Umbrella.Utilities.Threading.RateLimiting.Exceptions;

/// <summary>
/// The exception thrown when authoritative rate-limit state cannot be accessed.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "An inner store exception is required for this specialized exception.")]
public sealed class RateLimitUnavailableException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitUnavailableException"/> class.
	/// </summary>
	public RateLimitUnavailableException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
