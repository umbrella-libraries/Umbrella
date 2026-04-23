namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// The formats supported by the Dynamic Image infrastructure.
/// </summary>
public enum DynamicImageFormat
{
	/// <summary>
	/// A bitmap file.
	/// </summary>
	Bmp = 0,

	/// <summary>
	/// A gif file.
	/// </summary>
	Gif = 1,

	/// <summary>
	/// A jpeg / jpg file.
	/// </summary>
	Jpeg = 2,

	/// <summary>
	/// A png file.
	/// </summary>
	Png = 3,

	/// <summary>
	/// A webp file.
	/// </summary>
	WebP = 4,

	/// <summary>
	/// An avif file.
	/// </summary>
	/// <remarks>
	/// AVIF encoding is not available in current SkiaSharp native builds — <c>SKBitmap.Encode</c> returns null.
	/// FreeImage does not support AVIF at all. Both resizers report <c>SupportsFormat(Avif) = false</c>, so the
	/// middleware fallback will not offer AVIF until native encoder support ships.
	/// </remarks>
	Avif = 5
}

/// <summary>
/// Contains extension methods that operate on values of the <see cref="DynamicImageFormat"/> enumeration.
/// </summary>
public static class DynamicImageFormatExtensions
{
	/// <summary>
	/// Converts a <see cref="DynamicImageFormat"/> value to its corresponding file extension (without a leading '.').
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The file extension (without a leading '.').</returns>
	public static string ToFileExtensionString(this DynamicImageFormat value) => value switch
	{
		DynamicImageFormat.Jpeg => "jpg",
		DynamicImageFormat.Bmp => "bmp",
		DynamicImageFormat.Gif => "gif",
		DynamicImageFormat.Png => "png",
		DynamicImageFormat.WebP => "webp",
		DynamicImageFormat.Avif => "avif",
		_ => throw new NotSupportedException($"The specified format: {value} is not supported.")
	};
}