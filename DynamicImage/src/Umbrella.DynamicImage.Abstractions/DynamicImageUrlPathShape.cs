namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Represents the supported Dynamic Image URL path shapes.
/// </summary>
public enum DynamicImageUrlPathShape
{
	/// <summary>
	/// Uses the canonical legacy URL shape with no explicit version token path segment.
	/// </summary>
	Unversioned,

	/// <summary>
	/// Uses a URL shape that includes a dedicated version-token path segment.
	/// </summary>
	Versioned
}
