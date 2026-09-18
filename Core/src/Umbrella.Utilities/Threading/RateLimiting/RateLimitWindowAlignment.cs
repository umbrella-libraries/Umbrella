namespace Umbrella.Utilities.Threading.RateLimiting;

/// <summary>
/// Specifies how a fixed rate-limit window is aligned.
/// </summary>
public enum RateLimitWindowAlignment
{
	/// <summary>
	/// The window begins with the first successful acquisition after the preceding window expires.
	/// </summary>
	Relative = 0,

	/// <summary>
	/// The window is aligned to UTC boundaries based on its duration.
	/// </summary>
	UtcAligned = 1
}
