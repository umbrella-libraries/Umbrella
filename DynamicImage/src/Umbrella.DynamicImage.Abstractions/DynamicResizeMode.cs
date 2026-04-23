namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// The mode to use when resizing images.
/// </summary>
public enum DynamicResizeMode
{
	/// <summary>
	/// Resizes the image uniformly (without squashing or stretching) without exceeding the original width or height. Resizing is attempted based
	/// on the target width first. If that would result in the height exceeding the target height, resizing is done using the target height instead.
	/// This effectively combines the <see cref="UseWidth"/> and <see cref="UseHeight"/> modes.
	/// </summary>
	ScaleDown = 0,

	/// <summary>
	/// Resizes based on the target width. If the target width is greater than the original width, the original width is used.
	/// The height is then calculated using the target width to maintain aspect ratio.
	/// </summary>
	UseWidth = 1,

	/// <summary>
	/// Resizes based on the target height. If the target height is greater than the original height, the original height is used.
	/// The width is then calculated using the target height to maintain aspect ratio.
	/// </summary>
	UseHeight = 2,

	/// <summary>
	/// Similar to <see cref="ScaleDown"/>, but the image is resized to the target width or height regardless of the original size to be as large as possible.
	/// </summary>
	// TODO: Contain = 3, - Bother?

	/// <summary>
	/// Resizes the image uniformly and tries to meet both the target height and width exactly by centrally cropping the image to maintain the aspect ratio.
	/// Resizing is attempted using the target width first.
	/// If that would result in the height exceeding the target height, resizing is done using the target height instead. The width is then cropped either side of the horizonal image center.
	/// If not, the resizing is done using the target width with the height then being cropped either side of the vertical image center.
	/// </summary>
	Crop = 4,

	/// <summary>
	/// Similar to <see cref="Crop"/>, but the image is resized to the target width or height regardless of the original size to be as large as possible.
	/// </summary>
	// TODO: Cover = 5 - Bother?

	/// <summary>
	/// Resizes the image uniformly and tries to meet both the target height and width exactly by cropping the image to maintain the aspect ratio.
	/// Resizing is attempted using the target width first.
	/// If that would result in the height exceeding the target height, resizing is done using the target height instead. The width is then cropped either side of an image focal point.
	/// If not, the resizing is done using the target width with the height then being cropped either side of an image focal point.
	/// </summary>
	CropFocalPoint = 6,

	/// <summary>
	/// Resizes the image uniformly and tries to meet both the target height and width exactly by cropping the image to maintain the aspect ratio.
	/// Resizing is attempted using the target width first.
	/// If that would result in the height exceeding the target height, resizing is done using the target height instead. The width is then cropped either side of an AI determined focal point.
	/// If not, the resizing is done using the target width with the height then being cropped either side of an AI determined focal point.
	/// </summary>
	// TODO: CropAI = 7,
}