using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Represents the options for resizing a single image.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DynamicImageOptions
{
	/// <summary>
	/// Gets the width.
	/// </summary>
	public int Width { get; }

	/// <summary>
	/// Gets the height.
	/// </summary>
	public int Height { get; }

	/// <summary>
	/// Gets the resize mode.
	/// </summary>
	public DynamicResizeMode ResizeMode { get; }

	/// <summary>
	/// Gets the format.
	/// </summary>
	public DynamicImageFormat Format { get; }

	/// <summary>
	/// This represents the source path and could be a physical path or URL.
	/// </summary>
	public string SourcePath { get; }

	/// <summary>
	/// Gets the filter quality for resizing the image.
	/// </summary>
	public DynamicImageFilterQuality FilterQuality { get; } = DynamicImageFilterQuality.Medium;

	/// <summary>
	/// Gets the optional version token extracted from or used in the Dynamic Image URL.
	/// </summary>
	public string? VersionToken { get; }

	/// <summary>
	/// Get the quality request.
	/// </summary>
	/// <remarks>
	/// This is a value between 0-100. The quality is a suggestion, and not all formats (for example, PNG) or image libraries (e.g. FreeImage) respect or support it. Defaults to <c>100</c>.
	/// </remarks>
	public int QualityRequest { get; } = 100;

	/// <summary>
	/// Normalised X coordinate of the focal point for the image, between 0 and 1 starting from the left of the image.
	/// </summary>
	public double? FocalPointX { get; }

	/// <summary>
	/// Normalised Y coordinate of the focal point for the image, between 0 and 1 starting from the top of the image.
	/// </summary>
	public double? FocalPointY { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageOptions"/> struct.
	/// </summary>
	/// <param name="path">The path.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="format">The format.</param>
	/// <param name="filterQuality">The options for filter quality when resizing.</param>
	/// <param name="qualityRequest"></param>
	/// <param name="focalPointX"></param>
	/// <param name="focalPointY"></param>
	/// <param name="versionToken">The optional version token used by versioned Dynamic Image URLs.</param>
	public DynamicImageOptions(
		string path,
		int width,
		int height,
		DynamicResizeMode resizeMode,
		DynamicImageFormat format,
		DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium,
		int qualityRequest = 100,
		double? focalPointX = null,
		double? focalPointY = null,
		string? versionToken = null)
	{
		Guard.IsBetweenOrEqualTo(qualityRequest, 1, 100);

		if (focalPointX.HasValue != focalPointY.HasValue)
			throw new ArgumentException($"Both {nameof(FocalPointX)} and {nameof(FocalPointY)} must be defined if either is specified.");

		if (focalPointY.HasValue)
			Guard.IsBetweenOrEqualTo(focalPointY.Value, 0, 1);

		if (focalPointX.HasValue)
			Guard.IsBetweenOrEqualTo(focalPointX.Value, 0, 1);

		SourcePath = path;
		Width = width;
		Height = height;
		ResizeMode = resizeMode;
		Format = format;
		FilterQuality = filterQuality;
		VersionToken = string.IsNullOrWhiteSpace(versionToken) ? null : versionToken!.Trim().ToLowerInvariant();
		QualityRequest = qualityRequest;
		FocalPointX = focalPointX;
		FocalPointY = focalPointY;
	}

	/// <summary>
	/// Performs an explicit conversion from <see cref="DynamicImageOptions"/> to <see cref="DynamicImageMapping"/>.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>The <see cref="DynamicImageMapping"/> instance.</returns>
	public static explicit operator DynamicImageMapping(in DynamicImageOptions options) => new(options.Width, options.Height, options.ResizeMode, options.Format);

	/// <summary>
	/// Determines whether the specified options is empty.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>
	///   <c>true</c> if the specified options is empty; otherwise, <c>false</c>.
	/// </returns>
	public static bool IsEmpty(in DynamicImageOptions options) => options == default;

	/// <summary>
	/// Converts the specified options to a <see cref="DynamicImageMapping"/> instance.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>The <see cref="DynamicImageMapping"/> instance.</returns>
	public static DynamicImageMapping ToDynamicImageMapping(in DynamicImageOptions options) => (DynamicImageMapping)options;
}
