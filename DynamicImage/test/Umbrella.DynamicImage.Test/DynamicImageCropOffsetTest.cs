using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;

namespace Umbrella.DynamicImage.Test;

/// <summary>
/// Covers the crop offset arithmetic shared by every resizer implementation.
/// </summary>
/// <remarks>
/// <see cref="DynamicResizeMode.Crop"/> anchors on a focal point when one is supplied and on the image center when one is not.
/// These tests pin the center case to the exact offset the dedicated center crop produced before the two modes were merged,
/// because a merged implementation that drifts by a pixel would silently reframe every existing focal-point-free crop.
/// </remarks>
public class DynamicImageCropOffsetTest
{
	/// <summary>
	/// The offset a center crop produced before <c>CropFocalPoint</c> was merged into <see cref="DynamicResizeMode.Crop"/>.
	/// </summary>
	private static int LegacyCenterOffset(int originalLength, int cropLength) => (originalLength - cropLength) / 2;

	[Theory]
	// The original is 300 x 193, so these targets all crop the width and leave the height alone.
	[InlineData(300, 193, 50, 150)]
	[InlineData(300, 193, 100, 200)]
	[InlineData(300, 193, 51, 101)]
	[InlineData(300, 193, 99, 197)]
	public void Crop_WithoutFocalPoint_UsesLegacyCenterOffset(int originalWidth, int originalHeight, int targetWidth, int targetHeight)
	{
		var (_, _, offsetX, offsetY, cropWidth, cropHeight) = TestResizer.GetDimensions(originalWidth, originalHeight, targetWidth, targetHeight, DynamicResizeMode.Crop);

		Assert.Equal(LegacyCenterOffset(originalWidth, cropWidth), offsetX);
		Assert.Equal(LegacyCenterOffset(originalHeight, cropHeight), offsetY);
	}

	[Fact]
	public void Crop_WithoutFocalPoint_UsesLegacyCenterOffsetAcrossManySizes()
	{
		// The two formulas diverge only for particular parities of the original and crop lengths, so sweep a wide range
		// rather than relying on a handful of hand-picked sizes.
		for (int originalWidth = 40; originalWidth <= 300; originalWidth++)
		{
			for (int targetWidth = 10; targetWidth < originalWidth; targetWidth += 3)
			{
				var (_, _, offsetX, offsetY, cropWidth, cropHeight) = TestResizer.GetDimensions(originalWidth, 193, targetWidth, 150, DynamicResizeMode.Crop);

				Assert.Equal(LegacyCenterOffset(originalWidth, cropWidth), offsetX);
				Assert.Equal(LegacyCenterOffset(193, cropHeight), offsetY);
			}
		}
	}

	[Fact]
	public void Crop_WithExplicitCenterFocalPoint_MatchesImplicitCenter()
	{
		var implicitCenter = TestResizer.GetDimensions(300, 193, 50, 150, DynamicResizeMode.Crop);
		var explicitCenter = TestResizer.GetDimensions(300, 193, 50, 150, DynamicResizeMode.Crop, 0.5, 0.5);

		Assert.Equal(implicitCenter, explicitCenter);
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 1)]
	public void Crop_WithEdgeFocalPoint_KeepsCropWindowInsideImage(double focalPointX, double focalPointY)
	{
		var (_, _, offsetX, offsetY, cropWidth, cropHeight) = TestResizer.GetDimensions(300, 193, 50, 150, DynamicResizeMode.Crop, focalPointX, focalPointY);

		Assert.InRange(offsetX, 0, 300 - cropWidth);
		Assert.InRange(offsetY, 0, 193 - cropHeight);
	}

	[Fact]
	public void Crop_WithOffCenterFocalPoint_ShiftsAwayFromCenter()
	{
		var (_, _, centerOffsetX, _, _, _) = TestResizer.GetDimensions(300, 193, 50, 150, DynamicResizeMode.Crop);
		var (_, _, leftOffsetX, _, _, _) = TestResizer.GetDimensions(300, 193, 50, 150, DynamicResizeMode.Crop, 0, 0.5);

		Assert.True(leftOffsetX < centerOffsetX);
	}

	/// <summary>
	/// Exposes the protected offset arithmetic on <see cref="DynamicImageResizerBase"/> so it can be asserted directly
	/// rather than inferred from encoded image bytes.
	/// </summary>
	private sealed class TestResizer : DynamicImageResizerBase
	{
		public TestResizer()
			: base(new Mock<ILogger>().Object, new Mock<IDynamicImageCache>().Object)
		{
		}

		public static (int width, int height, int offsetX, int offsetY, int cropWidth, int cropHeight) GetDimensions(
			int originalWidth,
			int originalHeight,
			int targetWidth,
			int targetHeight,
			DynamicResizeMode mode,
			double? focalPointX = null,
			double? focalPointY = null)
			=> GetDestinationDimensions(originalWidth, originalHeight, targetWidth, targetHeight, mode, focalPointX, focalPointY);

		public override bool IsImage(byte[] bytes) => throw new NotSupportedException();

		public override (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium, int qualityRequest = 75, double? focalPointX = null, double? focalPointY = null)
			=> throw new NotSupportedException();

		public override (int width, int height) GetImageDimensions(byte[] bytes) => throw new NotSupportedException();

		public override bool SupportsFormat(DynamicImageFormat format) => throw new NotSupportedException();
	}
}
