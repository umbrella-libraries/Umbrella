using System.Runtime.InteropServices;

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload;

/// <summary>
/// The event arguments raised when the focal point of an <see cref="UmbrellaFileImagePreviewUpload"/> changes.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs"/> struct.
	/// </summary>
	/// <param name="focalPointX">The normalised X coordinate, or <see langword="null"/> when cleared.</param>
	/// <param name="focalPointY">The normalised Y coordinate, or <see langword="null"/> when cleared.</param>
	public UmbrellaFileImagePreviewUploadFocalPointChangedEventArgs(double? focalPointX, double? focalPointY)
	{
		FocalPointX = focalPointX;
		FocalPointY = focalPointY;
	}

	/// <summary>
	/// Gets the normalised X coordinate, or <see langword="null"/> when cleared.
	/// </summary>
	public double? FocalPointX { get; }

	/// <summary>
	/// Gets the normalised Y coordinate, or <see langword="null"/> when cleared.
	/// </summary>
	public double? FocalPointY { get; }
}
