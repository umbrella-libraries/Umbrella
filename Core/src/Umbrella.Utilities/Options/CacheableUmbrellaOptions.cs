#if NET9_0_OR_GREATER
using Microsoft.Extensions.Caching.Hybrid;
#endif

namespace Umbrella.Utilities.Options;

/// <summary>
/// An abstract class that serves as the base class for all Umbrella options classes that have cacheable properties.
/// </summary>
public abstract class CacheableUmbrellaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether caching is enabled. Defaults to <see langword="true" />.
	/// </summary>
	public virtual bool CacheEnabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the cache timeout. Defaults to 1 hour.
	/// </summary>
	public virtual TimeSpan CacheTimeout { get; set; } = TimeSpan.FromHours(1);

#if NET9_0_OR_GREATER
	/// <summary>
	/// Gets or sets the flags applied to Microsoft Hybrid Cache entries. Defaults to <see cref="HybridCacheEntryFlags.None" />.
	/// </summary>
	public virtual HybridCacheEntryFlags CacheEntryFlags { get; set; } = HybridCacheEntryFlags.None;
#endif
}
