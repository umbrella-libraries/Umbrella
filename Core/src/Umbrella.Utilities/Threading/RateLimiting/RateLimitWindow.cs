namespace Umbrella.Utilities.Threading.RateLimiting;

/// <summary>
/// Describes a single fixed rate-limit window.
/// </summary>
public sealed class RateLimitWindow
{
	/// <summary>
	/// Gets the maximum number of successful acquisitions allowed in the window.
	/// </summary>
	public int PermitLimit { get; }

	/// <summary>
	/// Gets the duration of the window.
	/// </summary>
	public TimeSpan Duration { get; }

	/// <summary>
	/// Gets the window alignment.
	/// </summary>
	public RateLimitWindowAlignment Alignment { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitWindow"/> class.
	/// </summary>
	public RateLimitWindow(int permitLimit, TimeSpan duration, RateLimitWindowAlignment alignment = RateLimitWindowAlignment.Relative)
	{
		if (permitLimit <= 0)
			throw new ArgumentOutOfRangeException(nameof(permitLimit), permitLimit, "The permit limit must be greater than zero.");

		if (duration <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(duration), duration, "The duration must be greater than zero.");

#if NET8_0_OR_GREATER
		if (!Enum.IsDefined(alignment))
#else
		if (!Enum.IsDefined(typeof(RateLimitWindowAlignment), alignment))
#endif
			throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "The window alignment is invalid.");

		PermitLimit = permitLimit;
		Duration = duration;
		Alignment = alignment;
	}
}
