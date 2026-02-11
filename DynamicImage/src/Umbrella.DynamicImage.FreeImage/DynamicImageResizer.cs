
using System.Drawing;
using CommunityToolkit.Diagnostics;
using FreeImageAPI;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;

namespace Umbrella.DynamicImage.FreeImage;

// TODO: Handle issues with transparency information being lost on resize

/// <summary>
/// An implementation of the <see cref="DynamicImageResizerBase"/> which uses FreeImage.
/// </summary>
/// <seealso cref="DynamicImageResizerBase" />
public class DynamicImageResizer : DynamicImageResizerBase
{
	#region Constructors		
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
	#endregion

	#region Overridden Methods
	/// <inheritdoc />
	public override bool IsImage(byte[] bytes)
	{
		try
		{
			Guard.IsNotNull(bytes);
			Guard.HasSizeGreaterThan(bytes, 0);

			using var inputStream = new MemoryStream(bytes);
			using var image = FreeImageBitmap.FromStream(inputStream);

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
		DynamicImageFormat.Bmp => true,
		DynamicImageFormat.Gif => true,
		DynamicImageFormat.Jpeg => true,
		DynamicImageFormat.Png => true,
		DynamicImageFormat.WebP => true,
		DynamicImageFormat.Avif => false,
		_ => false,
	};

	/// <inheritdoc/>
	public override (int width, int height) GetImageDimensions(byte[] bytes)
	{
		Guard.IsNotNull(bytes);

		try
		{
			using var image = LoadBitmap(bytes);

			return (image.Width, image.Height);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDynamicImageException("There has been a problem determining the image dimensions.", exc);
		}
	}

	/// <inheritdoc />
	public override (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.None, int qualityRequest = 75)
	{
		Guard.IsNotNull(originalImage);
		Guard.HasSizeGreaterThan(originalImage, 0);
		Guard.IsGreaterThan(width, 0);
		Guard.IsGreaterThan(height, 0);
		Guard.IsBetweenOrEqualTo(qualityRequest, 1, 100);

		try
		{
			using var image = LoadBitmap(originalImage);

			FreeImageBitmap imageToSave = image;

			var result = GetDestinationDimensions(image.Width, image.Height, width, height, resizeMode);

			try
			{
				if (result.offsetX > 0 || result.offsetY > 0)
					imageToSave = image.Copy(new Rectangle(result.offsetX, result.offsetY, result.cropWidth, result.cropHeight));

				_ = imageToSave.Rescale(result.width, result.height, FREE_IMAGE_FILTER.FILTER_LANCZOS3);

				using var outputStream = new MemoryStream();

				imageToSave.Save(outputStream, GetImageFormat(format), GetSaveFlags(format));

				return (outputStream.ToArray(), result.width, result.height);
			}
			finally
			{
				if (!ReferenceEquals(image, imageToSave))
					imageToSave.Dispose();
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { width, height, resizeMode, format }))
		{
			throw new UmbrellaDynamicImageException("An error has occurred during image resizing.", exc, width, height, resizeMode, format);
		}
	}
	#endregion

	#region Private Methods
	private static FreeImageBitmap LoadBitmap(byte[] bytes)
	{
		using var inputStream = new MemoryStream(bytes);

		return FreeImageBitmap.FromStream(inputStream);
	}

	private static FREE_IMAGE_FORMAT GetImageFormat(DynamicImageFormat format) => format switch
	{
		DynamicImageFormat.Bmp => FREE_IMAGE_FORMAT.FIF_BMP,
		DynamicImageFormat.Gif => FREE_IMAGE_FORMAT.FIF_GIF,
		DynamicImageFormat.Jpeg => FREE_IMAGE_FORMAT.FIF_JPEG,
		DynamicImageFormat.Png => FREE_IMAGE_FORMAT.FIF_PNG,
		DynamicImageFormat.WebP => FREE_IMAGE_FORMAT.FIF_WEBP,
		DynamicImageFormat.Avif => throw new NotSupportedException("Avif is not supported."),
		_ => default,
	};

	private static FREE_IMAGE_SAVE_FLAGS GetSaveFlags(DynamicImageFormat format) => format switch
	{
		DynamicImageFormat.Jpeg => FREE_IMAGE_SAVE_FLAGS.JPEG_QUALITYGOOD | FREE_IMAGE_SAVE_FLAGS.JPEG_BASELINE,
		DynamicImageFormat.Png => FREE_IMAGE_SAVE_FLAGS.PNG_Z_BEST_COMPRESSION,
		DynamicImageFormat.WebP => FREE_IMAGE_SAVE_FLAGS.WEBP_DEFAULT,
		DynamicImageFormat.Avif => throw new NotSupportedException("Avif is not supported."),
		_ => default
	};
	#endregion
}