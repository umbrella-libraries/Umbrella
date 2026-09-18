namespace Umbrella.Utilities.Threading.RateLimiting;

/// <summary>
/// Describes an atomic two-window rate-limit policy comprising a short burst window and a longer quota window.
/// </summary>
public sealed class RateLimitPolicy
{
	/// <summary>
	/// Gets the short burst window.
	/// </summary>
	public RateLimitWindow BurstWindow { get; }

	/// <summary>
	/// Gets the longer quota window.
	/// </summary>
	public RateLimitWindow QuotaWindow { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitPolicy"/> class.
	/// </summary>
	public RateLimitPolicy(RateLimitWindow burstWindow, RateLimitWindow quotaWindow)
	{
		if (burstWindow is null)
			throw new ArgumentNullException(nameof(burstWindow));

		if (quotaWindow is null)
			throw new ArgumentNullException(nameof(quotaWindow));

		if (quotaWindow.Duration <= burstWindow.Duration)
			throw new ArgumentException("The quota window duration must be longer than the burst window duration.", nameof(quotaWindow));

		BurstWindow = burstWindow;
		QuotaWindow = quotaWindow;
	}
}
