


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;

namespace Umbrella.DynamicImage.SkiaSharp;

/// <summary>
/// An implementation of the <see cref="DynamicImageResizerBase"/> which uses SkiaSharp.
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

			using var image = LoadBitmap(bytes);

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

	// TODO: Could add an option to specify whether or not to resize if the image is already less than the width and height, ensuring we take
	// into account the resize mode.
	// TODO: Build in auto-rotate capability - see https://github.com/mono/SkiaSharp/issues/836
	/// <inheritdoc />
	public override (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium, int qualityRequest = 75)
	{
		Guard.IsNotNull(originalImage);
		Guard.HasSizeGreaterThan(originalImage, 0);
		Guard.IsGreaterThan(width, 0);
		Guard.IsGreaterThan(height, 0);
		Guard.IsBetweenOrEqualTo(qualityRequest, 1, 100);

		try
		{
			using var image = LoadBitmap(originalImage);

			SKBitmap imageToResize = image;

			var result = GetDestinationDimensions(image.Width, image.Height, width, height, resizeMode);

			try
			{
				// TODO: Look at how we can alter the resizing code to allow the cropped area position to be varied.
				// Could have a crop hotspot, i.e. an X and Y coordinate that we use to specify the center of the cropped area and then
				// calculate the edges accordingly.
				if (result.offsetX > 0 || result.offsetY > 0)
				{
					var cropRect = SKRectI.Create(result.offsetX, result.offsetY, result.cropWidth, result.cropHeight);

#pragma warning disable CA2000 // Dispose objects before losing scope
					imageToResize = new SKBitmap(cropRect.Width, cropRect.Height);
#pragma warning restore CA2000 // Dispose objects before losing scope
					_ = image.ExtractSubset(imageToResize, cropRect);
				}

				using var resizedImage = imageToResize.Resize(new SKImageInfo(result.width, result.height), filterQuality.ToSamplingOptions());
				using var outputImage = SKImage.FromBitmap(resizedImage);

				return (outputImage.Encode(GetImageFormat(format), qualityRequest).ToArray(), result.width, result.height);
			}
			finally
			{
				if (!ReferenceEquals(image, imageToResize))
					imageToResize.Dispose();
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { width, height, resizeMode, format }))
		{
			throw new UmbrellaDynamicImageException("An error has occurred during image resizing.", exc, width, height, resizeMode, format);
		}
	}

	private static SKBitmap LoadBitmap(byte[] bytes)
	{
		using var ms = new MemoryStream(bytes);

		// NB: This breaks using 20.8.3. Using the replacement SkData.Create fixes things.
		// See: https://github.com/mono/SkiaSharp/issues/1551
		//using var s = new SKManagedStream(ms);
		//using var codec = SKCodec.Create(ms);

		using var skData = SKData.Create(ms);
		using var codec = SKCodec.Create(skData);

		var info = codec.Info;
#pragma warning disable CA2000 // Dispose objects before losing scope
		var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, info.ColorType, info.AlphaType, info.ColorSpace));
#pragma warning restore CA2000 // Dispose objects before losing scope

		var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(out IntPtr length));

		return result switch
		{
			SKCodecResult.Success => bitmap,
			SKCodecResult.IncompleteInput => bitmap,
			_ => throw new ArgumentException("Unable to load bitmap from provided data")
		};
	}

	private static SKEncodedImageFormat GetImageFormat(DynamicImageFormat format) => format switch
	{
		DynamicImageFormat.Bmp => SKEncodedImageFormat.Bmp,
		DynamicImageFormat.Gif => SKEncodedImageFormat.Gif,
		DynamicImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
		DynamicImageFormat.Png => SKEncodedImageFormat.Png,
		DynamicImageFormat.WebP => SKEncodedImageFormat.Webp,
		DynamicImageFormat.Avif => throw new NotSupportedException("Avif is not supported."),
		_ => default,
	};
}

/// <summary>
/// Provides extension method for converting <see cref="DynamicImageFilterQuality"/> to <see cref="SKSamplingOptions"/>.
/// </summary>
public static partial class DynamicImageFilterQualityExtensions
{
	/// <summary>
	/// Converts the specified <see cref="DynamicImageFilterQuality"/> to <see cref="SKSamplingOptions"/>.
	/// </summary>
	/// <param name="quality">The filter quality.</param>
	/// <returns>The corresponding <see cref="SKSamplingOptions"/>.</returns>
	public static SKSamplingOptions ToSamplingOptions(this DynamicImageFilterQuality quality) =>
		quality switch
		{
			DynamicImageFilterQuality.None => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
			DynamicImageFilterQuality.Low => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
			DynamicImageFilterQuality.Medium => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
			DynamicImageFilterQuality.High => new SKSamplingOptions(SKCubicResampler.Mitchell),
			_ => throw new ArgumentOutOfRangeException(nameof(quality), $"Unknown filter quality: '{quality}'"),
		};
}