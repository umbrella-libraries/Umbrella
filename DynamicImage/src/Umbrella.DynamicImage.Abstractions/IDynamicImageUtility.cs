namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Contains utility methods for common operations performed by the Dynamic Image infrastructure.
/// </summary>
public interface IDynamicImageUtility
{
	/// <summary>
	/// Parses the image format.
	/// </summary>
	/// <param name="format">The format. This is usually the file extension, optionally starting with a '.'</param>
	/// <returns>The <see cref="DynamicImageFormat"/> for the specified format.</returns>
	DynamicImageFormat ParseImageFormat(string format);

	/// <summary>
	/// Tries to parse the relative URL into <see cref="DynamicImageOptions"/>.
	/// Supports both unversioned and versioned Dynamic Image URL shapes.
	/// </summary>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix, e.g. /dynamic-image</param>
	/// <param name="relativeUrl">The relative URL.</param>
	/// <param name="overrideFormat">
	/// The dynamic image format that the image will be set to regardless of the original format specified in the URL.
	/// This is useful for web situations where a browser supports webp and the url is requesting a jpg. This parameter allows the caller
	/// to override the image format and set it to webp.
	/// </param>
	/// <returns>A tuple containing the status of the parsing operation together with the parsed options.</returns>
	(DynamicImageParseUrlResult status, DynamicImageOptions imageOptions) TryParseUrl(string dynamicImagePathPrefix, string relativeUrl, DynamicImageFormat? overrideFormat = null);

	/// <summary>
	/// Checks if the specified <paramref name="imageOptions"/> are valid based on the specified <paramref name="validMappings"/>.
	/// </summary>
	/// <param name="imageOptions">The image options.</param>
	/// <param name="validMappings">The valid mappings.</param>
	/// <returns><see langword="true"/> if the <paramref name="imageOptions"/> are valid; otherwise <see langword="false"/>.</returns>
	bool ImageOptionsValid(DynamicImageOptions imageOptions, IEnumerable<DynamicImageMapping> validMappings);

	/// <summary>
	/// Generates an application relative virtual path for the specified <paramref name="dynamicImagePathPrefix"/> and <paramref name="options"/>.
	/// The emitted shape is controlled by <see cref="DynamicImageOptions.UrlPathShape"/>.
	/// </summary>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <param name="options">The options.</param>
	/// <returns></returns>
	string GenerateVirtualPath(string dynamicImagePathPrefix, DynamicImageOptions options);
}
