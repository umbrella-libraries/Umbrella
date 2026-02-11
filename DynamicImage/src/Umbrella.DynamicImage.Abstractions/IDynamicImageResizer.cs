using Umbrella.FileSystem.Abstractions;

namespace Umbrella.DynamicImage.Abstractions;

/// <summary>
/// A utility for resizing images.
/// </summary>
public interface IDynamicImageResizer
{
	/// <summary>
	/// Determines whether the specified bytes is an image.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>
	///   <c>true</c> if the specified bytes is an image; otherwise, <c>false</c>.
	/// </returns>
	bool IsImage(byte[] bytes);

	/// <summary>
	/// Generates the image using the specified options or finds its existing version in the cache.
	/// </summary>
	/// <param name="sourceFileProvider">The source file provider.</param>
	/// <param name="options">The options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The resized image.</returns>
	Task<DynamicImageItem?> GenerateImageAsync(IUmbrellaFileStorageProvider sourceFileProvider, DynamicImageOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates the image using the specified options or finds its existing version in the cache.
	/// </summary>
	/// <param name="sourceBytesProvider">The source bytes provider.</param>
	/// <param name="sourceLastModified">The source last modified.</param>
	/// <param name="options">The options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Resized image.</returns>
	Task<DynamicImageItem> GenerateImageAsync(Func<Task<byte[]>> sourceBytesProvider, DateTimeOffset sourceLastModified, DynamicImageOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Resizes the image using the specified options.
	/// </summary>
	/// <param name="originalImage">The original image.</param>
	/// <param name="options">The options.</param>
	/// <returns>The resized image together with its new width and height.</returns>
	(byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, DynamicImageOptions options);

	/// <summary>
	/// Resizes the image.
	/// </summary>
	/// <param name="originalImage">The original image.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="format">The format.</param>
	/// <param name="filterQuality">The options for filter quality when resizing.</param>
	/// <param name="qualityRequest">A value between 0-100. The quality is a suggestion, and not all formats (for example, PNG) or image libraries (e.g. FreeImage) respect or support it. Defaults to <c>75</c>.</param>
	/// <returns>The resized image together with its new width and height.</returns>
	(byte[] resizedBytes, int resizedWidth, int resizedHeight) ResizeImage(byte[] originalImage, int width, int height, DynamicResizeMode resizeMode, DynamicImageFormat format, DynamicImageFilterQuality filterQuality = DynamicImageFilterQuality.Medium, int qualityRequest = 75);

	/// <summary>
	/// Gets the dimensions of the image.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The width and height of the image in pixels.</returns>
	(int width, int height) GetImageDimensions(byte[] bytes);

	/// <summary>
	/// Gets the cached item if it exists.
	/// </summary>
	/// <param name="sourceFile">The source file.</param>
	/// <param name="options">The options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached image item.</returns>
	Task<DynamicImageItem?> GetCachedItemAsync(IUmbrellaFileInfo sourceFile, DynamicImageOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Determines whether the resizer supports the specified format.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <returns>Whether the resizer supports the specified format.</returns>
	bool SupportsFormat(DynamicImageFormat format);
}