
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.FileSystem.Abstractions;

// TODO: Need to do some work to decouple the caching from this package and move to a new package.

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// Serves as the base class for all Dynamic Image resizers.
/// </summary>
/// <seealso cref="IDynamicImageResizer" />
public abstract class DynamicImageResizerBase : IDynamicImageResizer
{
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected IDynamicImageCache Cache { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageResizerBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dynamicImageCache">The dynamic image cache.</param>
	protected DynamicImageResizerBase(
		ILogger logger,
		IDynamicImageCache dynamicImageCache)
	{
		Logger = logger;
		Cache = dynamicImageCache;
	}

	/// <inheritdoc />
	public async Task<DynamicImageItem?> GetCachedItemAsync(IUmbrellaFileInfo sourceFile, DynamicImageOptions options, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFile);

		try
		{
			if (!sourceFile.LastModified.HasValue)
				throw new UmbrellaDynamicImageException("The fileInfo must have a last modified value.");

			return await Cache.GetAsync(options, sourceFile.LastModified.Value, options.Format.ToFileExtensionString(), cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options }) && exc is not UmbrellaDynamicImageException)
		{
			throw new UmbrellaDynamicImageException("An error has occurred trying to find the cached item.", exc, options);
		}
	}

	/// <inheritdoc />
	public async Task<DynamicImageItem?> GenerateImageAsync(IUmbrellaFileStorageProvider sourceFileProvider, DynamicImageOptions options, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFileProvider);

		try
		{
			var fileInfo = await sourceFileProvider.GetAsync(options.SourcePath, cancellationToken).ConfigureAwait(false);

			if (fileInfo is null || !await fileInfo.ExistsAsync(cancellationToken).ConfigureAwait(false))
				return null;

			return !fileInfo.LastModified.HasValue
				? throw new UmbrellaDynamicImageException("The fileInfo must have a last modified value.")
				: await GenerateImageAsync(async () => await fileInfo.ReadAsByteArrayAsync(cancellationToken: cancellationToken).ConfigureAwait(false),
				fileInfo.LastModified.Value,
				options,
				cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { options }) && exc is not UmbrellaDynamicImageException)
		{
			throw new UmbrellaDynamicImageException("An error has occurred during image resizing.", exc, options);
		}
	}

	/// <inheritdoc />
	public async Task<DynamicImageItem> GenerateImageAsync(Func<Task<byte[]>> sourceBytesProvider, DateTimeOffset sourceLastModified, DynamicImageOptions options, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceBytesProvider);

		try
		{
			// TODO: Consider building in stampede protection in this method for use cases where this method is called multiple times in quick succession.
			// We have implemented stampede protection in the ASP.NET Core middleware already.

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { sourceLastModified, options }, "Started generating the image based on the state.");

			//Check if the image exists in the cache
			DynamicImageItem? dynamicImage = await Cache.GetAsync(options, sourceLastModified, options.Format.ToFileExtensionString(), cancellationToken).ConfigureAwait(false);

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { options, sourceLastModified, options.Format }, "Searched the image cache using the supplied state.");

			if (dynamicImage is not null)
			{
				if (Logger.IsEnabled(LogLevel.Debug))
					Logger.WriteDebug(new { dynamicImage.ImageOptions, dynamicImage.LastModified }, "Image found in cache.");

				return dynamicImage;
			}

			//Item cannot be found in the cache - build a new image
			byte[] originalBytes = await sourceBytesProvider().ConfigureAwait(false);

			// Need to get the newly resized image and assign it to the instance
			var (resizedBytes, _, _) = ResizeImage(originalBytes, options);

			dynamicImage = new DynamicImageItem
			{
				ImageOptions = options,
				LastModified = DateTimeOffset.UtcNow,
				Content = resizedBytes
			};

			//Now add to the cache
			await Cache.AddAsync(dynamicImage, cancellationToken).ConfigureAwait(false);

			return dynamicImage;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceLastModified, options }) && (exc is UmbrellaDynamicImageException) == false)
		{
			throw new UmbrellaDynamicImageException("An error has occurred during image resizing.", exc, options);
		}
	}

	/// <inheritdoc />
	public abstract bool IsImage(byte[] bytes);

	/// <inheritdoc />
	public (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, DynamicImageOptions options)
		=> ResizeImage(originalImage, options.Width, options.Height, options.ResizeMode, options.Format, options.FilterQuality, options.QualityRequest);

	/// <inheritdoc />
	public abstract (byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium, int qualityRequest = 75);

	/// <inheritdoc />
	public abstract (int width, int height) GetImageDimensions(byte[] bytes);

	/// <inheritdoc />
	public abstract bool SupportsFormat(DynamicImageFormat format);

	/// <summary>
	/// Gets the destination dimensions.
	/// </summary>
	/// <param name="originalWidth">Width of the original.</param>
	/// <param name="originalHeight">Height of the original.</param>
	/// <param name="targetWidth">Width of the target.</param>
	/// <param name="targetHeight">Height of the target.</param>
	/// <param name="mode">The mode.</param>
	/// <returns>A tuple containing the destination dimensions.</returns>
	protected static (int width, int height, int offsetX, int offsetY, int cropWidth, int cropHeight) GetDestinationDimensions(int originalWidth, int originalHeight, int targetWidth, int targetHeight, DynamicResizeMode mode)
	{
		int? requestedWidth = null;
		int? requestedHeight = null;

		int offsetX = 0;
		int offsetY = 0;
		int cropWidth = originalWidth;
		int cropHeight = originalHeight;

		switch (mode)
		{
			case DynamicResizeMode.UseWidth:
				requestedWidth = targetWidth < originalWidth ? targetWidth : originalWidth;
				break;
			case DynamicResizeMode.UseHeight:
				requestedHeight = targetHeight < originalHeight ? targetHeight : originalHeight;
				break;
			case DynamicResizeMode.ScaleDown:
				// If both requested dimensions are greater than source image, we don't need to do any resizing.
				if (targetWidth < originalWidth || targetHeight < originalHeight)
				{
					// Calculate requested width and height so as not to squash image.

					// First, resize based on max width and check whether resized height will be more than target height.
					var (_, tempHeight) = CalculateOutputDimensions(originalWidth, originalHeight, targetWidth, null);

					if (tempHeight > targetHeight)
					{
						// If so, we need to resize based on max height instead.
						requestedHeight = targetHeight;
					}
					else
					{
						// If not, we have our max dimension.
						requestedWidth = targetWidth;
					}
				}
				else
				{
					requestedWidth = originalWidth;
					requestedHeight = originalHeight;
				}

				break;
			case DynamicResizeMode.Crop:
				// Resize based on width first. If this means that height is less than target height, we resize based on height.
				if (targetWidth < originalWidth || targetHeight < originalHeight)
				{
					// Calculate requested width and height so as not to squash image.

					// First, resize based on width and check whether resized height will be more than max height.
					var (_, tempHeight) = CalculateOutputDimensions(originalWidth, originalHeight, targetWidth, null);

					if (tempHeight < targetHeight)
					{
						// If so, we need to resize based on max height instead.
						requestedHeight = targetHeight;
						int tempWidth;
						(tempWidth, _) = CalculateOutputDimensions(originalWidth, originalHeight, null, targetHeight);

						// Then crop width and calculate offset.
						requestedWidth = targetWidth;
						cropWidth = (int)(targetWidth / (float)tempWidth * originalWidth);
						offsetX = (originalWidth - cropWidth) / 2;
					}
					else
					{
						// If not, we have our max dimension.
						requestedWidth = targetWidth;

						// Then crop height and calculate offset.
						requestedHeight = targetHeight;
						cropHeight = (int)(targetHeight / (float)tempHeight * originalHeight);
						offsetY = (originalHeight - cropHeight) / 2;
					}
				}
				else
				{
					requestedWidth = originalWidth;
					requestedHeight = originalHeight;
				}

				break;
		}

		var (width, height) = CalculateOutputDimensions(originalWidth, originalHeight, requestedWidth, requestedHeight);

		return (width, height, offsetX, offsetY, cropWidth, cropHeight);
	}

	private static (int width, int height) CalculateOutputDimensions(int nInputWidth, int nInputHeight, int? nRequestedWidth, int? nRequestedHeight)
	{
		// both width and height are specified - squash image
		if (nRequestedWidth is not null && nRequestedHeight is not null)
		{
			return (nRequestedWidth.Value, nRequestedHeight.Value);
		}
		else if (nRequestedWidth is not null) // calculate height to keep aspect ratio
		{
			double aspectRatio = (double)nInputWidth / nInputHeight;

			return (nRequestedWidth.Value, (int)(nRequestedWidth.Value / aspectRatio));
		}
		else if (nRequestedHeight is not null) // calculate width to keep aspect ratio
		{
			double aspectRatio = (double)nInputHeight / nInputWidth;

			return ((int)(nRequestedHeight.Value / aspectRatio), nRequestedHeight.Value);
		}
		else
		{
			throw new UmbrellaDynamicImageException("Width or height, or both, must be specified");
		}
	}
}