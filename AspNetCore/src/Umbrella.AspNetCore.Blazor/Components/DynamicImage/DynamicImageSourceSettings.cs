using CommunityToolkit.Diagnostics;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage;

/// <summary>
/// The resolved configuration used to generate the URLs of a single image or picture source.
/// </summary>
public sealed record DynamicImageSourceSettings
{
	/// <summary>
	/// Gets the URL of the source image with the configured prefix already removed.
	/// </summary>
	public required string Url { get; init; }

	/// <summary>
	/// Gets the width request in pixels.
	/// </summary>
	public required int WidthRequest { get; init; }

	/// <summary>
	/// Gets the height request in pixels.
	/// </summary>
	public required int HeightRequest { get; init; }

	/// <summary>
	/// Gets the resize mode.
	/// </summary>
	public required DynamicResizeMode ResizeMode { get; init; }

	/// <summary>
	/// Gets the image format used for the fallback image or source.
	/// </summary>
	public required DynamicImageFormat ImageFormat { get; init; }

	/// <summary>
	/// Gets the maximum pixel density.
	/// </summary>
	public required int MaxPixelDensity { get; init; }

	/// <summary>
	/// Gets the size widths.
	/// </summary>
	public required string? SizeWidths { get; init; }

	/// <summary>
	/// Gets the normalised X coordinate of the focal point.
	/// </summary>
	public required double? FocalPointX { get; init; }

	/// <summary>
	/// Gets the normalised Y coordinate of the focal point.
	/// </summary>
	public required double? FocalPointY { get; init; }

	/// <summary>
	/// Gets the optional version token embedded in generated Dynamic Image URLs.
	/// </summary>
	public required string? VersionToken { get; init; }

	/// <summary>Gets the approval bound to the effective image and focal point.</summary>
	public string? FocalPointApproval { get; init; }

	/// <summary>
	/// Validates the focal point configuration.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when only one of the focal point coordinates has been specified.</exception>
	/// <exception cref="InvalidOperationException">Thrown when a focal point is specified for a resize mode that does not use one.</exception>
	public void ValidateFocalPoint()
	{
		if (FocalPointX.HasValue != FocalPointY.HasValue)
			throw new ArgumentException($"Both {nameof(FocalPointX)} and {nameof(FocalPointY)} must be defined if either is specified.");

		if (!FocalPointX.HasValue)
			return;

		Guard.IsBetweenOrEqualTo(FocalPointX.Value, 0, 1);
		Guard.IsBetweenOrEqualTo(FocalPointY!.Value, 0, 1);

		if (ResizeMode is not DynamicResizeMode.CropFocalPoint)
			throw new InvalidOperationException($"{nameof(FocalPointX)} and {nameof(FocalPointY)} can only be used with {nameof(DynamicResizeMode.CropFocalPoint)}.");
	}
}
