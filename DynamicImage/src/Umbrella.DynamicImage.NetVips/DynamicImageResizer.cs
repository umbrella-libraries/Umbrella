
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using NetVips;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;

namespace Umbrella.DynamicImage.NetVips;

/// <summary>
/// An implementation of <see cref="DynamicImageResizerBase"/> which uses NetVips (libvips).
/// </summary>
/// <seealso cref="DynamicImageResizerBase" />
public class DynamicImageResizer : DynamicImageResizerBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageResizer"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dynamicImageCache">The dynamic image cache.</param>
	public DynamicImageResizer(
		ILogger<DynamicImageResizer> logger,
		IDynamicImageCache dynamicImageCache)
		: base(logger, dynamicImageCache)
	{
	}

	/// <inheritdoc />
	public override bool IsImage(byte[] bytes)
	{
		try
		{
			Guard.IsNotNull(bytes);
			Guard.HasSizeGreaterThan(bytes, 0);

			using var image = Image.NewFromBuffer(bytes);

			return image is not null;
		}
		catch
		{
			return false;
		}
	}

	/// <inheritdoc />
	public override bool SupportsFormat(DynamicImageFormat format) => format switch
	{
		DynamicImageFormat.Bmp => false, // libvips cannot write BMP to a buffer, only to a file
		DynamicImageFormat.Gif => true,
		DynamicImageFormat.Jpeg => true,
		DynamicImageFormat.Png => true,
		DynamicImageFormat.WebP => true,
		DynamicImageFormat.Avif => true,
		_ => false,
	};

	/// <inheritdoc />
	public override (int width, int height) GetImageDimensions(byte[] bytes)
	{
		Guard.IsNotNull(bytes);

		try
		{
			using var image = Image.NewFromBuffer(bytes);

			return (image.Width, image.Height);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDynamicImageException("There has been a problem determining the image dimensions.", exc);
		}
	}

	/// <inheritdoc />
	public override (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium, int qualityRequest = 75, double? focalPointX = null, double? focalPointY = null)
	{
		Guard.IsNotNull(originalImage);
		Guard.HasSizeGreaterThan(originalImage, 0);
		Guard.IsGreaterThan(width, 0);
		Guard.IsGreaterThan(height, 0);
		Guard.IsBetweenOrEqualTo(qualityRequest, 1, 100);

		try
		{
			using var image = Image.NewFromBuffer(originalImage);

			var result = GetDestinationDimensions(image.Width, image.Height, width, height, resizeMode, focalPointX, focalPointY);

			Image? croppedImage = null;
			Image imageToResize = image;

			try
			{
				if (result.cropWidth < image.Width || result.cropHeight < image.Height)
				{
					croppedImage = image.Crop(result.offsetX, result.offsetY, result.cropWidth, result.cropHeight);
					imageToResize = croppedImage;
				}

				double xscale = (double)result.width / imageToResize.Width;
				double yscale = (double)result.height / imageToResize.Height;

				using var resized = imageToResize.Resize(xscale, vscale: yscale, kernel: GetKernel(filterQuality));

				return (EncodeImage(resized, format, qualityRequest), resized.Width, resized.Height);
			}
			finally
			{
				croppedImage?.Dispose();
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { width, height, resizeMode, format }))
		{
			throw new UmbrellaDynamicImageException("An error has occurred during image resizing.", exc, width, height, resizeMode, format);
		}
	}

	private static byte[] EncodeImage(Image image, DynamicImageFormat format, int quality) => format switch
	{
		DynamicImageFormat.Jpeg => image.JpegsaveBuffer(q: quality, optimizeCoding: true),
		DynamicImageFormat.Png => image.PngsaveBuffer(compression: 6),
		DynamicImageFormat.WebP => image.WebpsaveBuffer(q: quality),
		DynamicImageFormat.Avif => image.HeifsaveBuffer(q: quality, compression: Enums.ForeignHeifCompression.Av1),
		DynamicImageFormat.Bmp => image.WriteToBuffer(".bmp"),
		DynamicImageFormat.Gif => image.GifsaveBuffer(),
		_ => throw new NotSupportedException($"The format {format} is not supported.")
	};

	private static Enums.Kernel GetKernel(DynamicImageFilterQuality quality) => quality switch
	{
		DynamicImageFilterQuality.None => Enums.Kernel.Nearest,
		DynamicImageFilterQuality.Low => Enums.Kernel.Linear,
		DynamicImageFilterQuality.Medium => Enums.Kernel.Lanczos3,
		DynamicImageFilterQuality.High => Enums.Kernel.Mitchell,
		_ => Enums.Kernel.Lanczos3
	};
}
