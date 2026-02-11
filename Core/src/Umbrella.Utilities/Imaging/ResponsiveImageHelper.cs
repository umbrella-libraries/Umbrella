


using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Imaging.Abstractions;

namespace Umbrella.Utilities.Imaging;

/// <summary>
/// A helper used to support the generation of responsive images.
/// </summary>
public class ResponsiveImageHelper : IResponsiveImageHelper
{
	private static readonly char[] _separatorCharacterArray = [','];

	private readonly ILogger<ResponsiveImageHelper> _logger;
	private readonly ConcurrentDictionary<int, int[]> _pixelDensityDictionary = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ResponsiveImageHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public ResponsiveImageHelper(ILogger<ResponsiveImageHelper> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public IReadOnlyCollection<int> GetParsedIntegerItems(string itemsString)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(itemsString))
				return [];

			string[] items = itemsString.Split(_separatorCharacterArray, StringSplitOptions.RemoveEmptyEntries);

			return new HashSet<int>(items.ParseIntegers());
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { itemsString }))
		{
			throw new UmbrellaException("There has been a problem parsing the items.", exc);
		}
	}

	/// <inheritdoc />
	public IReadOnlyCollection<int> GetPixelDensities(int maxPixelDensity)
	{
		Guard.IsGreaterThanOrEqualTo(maxPixelDensity, 1);

		try
		{
			return _pixelDensityDictionary.GetOrAdd(maxPixelDensity, key => [.. Enumerable.Range(1, maxPixelDensity)]);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { maxPixelDensity }))
		{
			throw new UmbrellaException("There has been a problem getting the pixel densities.", exc);
		}
	}

	/// <inheritdoc />
	public string GetPixelDensitySrcSetValue(string imageUrl, int maxPixelDensity, Func<string, int, string>? pixelDensityUrlTransformer = null, Func<string, string>? pathResolver = null)
	{
		Guard.IsNotNullOrWhiteSpace(imageUrl);
		Guard.IsGreaterThanOrEqualTo(maxPixelDensity, 1);

		try
		{
			var lstPixelDensity = GetPixelDensities(maxPixelDensity);

			if (lstPixelDensity.Count < 2)
				return string.Empty;

			var (sanitizedImageUrl, qs) = SplitImageUrl(imageUrl);

			IEnumerable<string> srcsetImagePaths =
				from density in lstPixelDensity
				orderby density
				let highResImagePath = density > 1 ? pixelDensityUrlTransformer?.Invoke(sanitizedImageUrl, density) ?? ApplyPixelDensity(sanitizedImageUrl, density) : sanitizedImageUrl
				select (pathResolver?.Invoke(highResImagePath) ?? highResImagePath) + (!string.IsNullOrEmpty(qs) ? "?" + qs : null) + " " + $"{density}x";

			return string.Join(", ", srcsetImagePaths);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { imageUrl, maxPixelDensity }))
		{
			throw new UmbrellaException("There has been a problem creating the srcset value.", exc);
		}
	}

	/// <inheritdoc />
	public IReadOnlyCollection<string> GetPixelDensityImageUrls(string imageUrl, int maxPixelDensity, Func<string, int, string>? pixelDensityUrlTransformer = null, Func<string, string>? pathResolver = null)
	{
		Guard.IsNotNullOrWhiteSpace(imageUrl);
		Guard.IsGreaterThanOrEqualTo(maxPixelDensity, 1);

		try
		{
			var lstPixelDensity = GetPixelDensities(maxPixelDensity);

			var (sanitizedImageUrl, qs) = SplitImageUrl(imageUrl);

			IEnumerable<string> paths =
				from density in lstPixelDensity
				orderby density
				let highResImagePath = density > 1 ? pixelDensityUrlTransformer?.Invoke(sanitizedImageUrl, density) ?? ApplyPixelDensity(sanitizedImageUrl, density) : sanitizedImageUrl
				select (pathResolver?.Invoke(highResImagePath) ?? highResImagePath) + (!string.IsNullOrEmpty(qs) ? "?" + qs : null);

			return paths.ToArray();
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { imageUrl, maxPixelDensity }))
		{
			throw new UmbrellaException("There has been a problem creating the urls.", exc);
		}
	}

	/// <inheritdoc />
	public string GetPixelDensityImageUrl(string imageUrl, int pixelDensity, Func<string, int, string>? pixelDensityUrlTransformer = null, Func<string, string>? pathResolver = null)
	{
		Guard.IsNotNullOrWhiteSpace(imageUrl);
		Guard.IsGreaterThanOrEqualTo(pixelDensity, 1);

		try
		{
			var (sanitizedImageUrl, qs) = SplitImageUrl(imageUrl);

			string highResImagePath = pixelDensity > 1 ? pixelDensityUrlTransformer?.Invoke(sanitizedImageUrl, pixelDensity) ?? ApplyPixelDensity(sanitizedImageUrl, pixelDensity) : sanitizedImageUrl;

			return (pathResolver?.Invoke(highResImagePath) ?? highResImagePath) + (!string.IsNullOrEmpty(qs) ? "?" + qs : null);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { imageUrl, pixelDensity }))
		{
			throw new UmbrellaException("There has been a problem creating the url.", exc);
		}
	}

	/// <inheritdoc />
	public string ApplyPixelDensity(string sanitizedImageUrl, int pixelDensity)
	{
		Guard.IsNotNullOrEmpty(sanitizedImageUrl);
		Guard.IsGreaterThanOrEqualTo(pixelDensity, 1);

		try
		{
			int index = sanitizedImageUrl.LastIndexOf('.');

			if (index is not >= 0)
				throw new InvalidOperationException("The image URL does not contain a file extension.");

			return sanitizedImageUrl.Insert(index, $"@{pixelDensity}x");
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { sanitizedImageUrl, pixelDensity }))
		{
			throw new UmbrellaException("There has been a problem applying the pixel density", exc);
		}
	}

	private static (string sanitizedImageUrl, string? qs) SplitImageUrl(string imageUrl)
	{
		string sanitizedImageUrl = imageUrl;
		string? qs = null;

#if NET6_0_OR_GREATER
		if (imageUrl.Contains('?', StringComparison.Ordinal))
#else
		if (imageUrl.Contains('?'))
#endif
		{
			string[] parts = imageUrl.Split('?');

			if (parts.Length != 2)
				throw new UmbrellaException("The path contains more than one '?'.");

			sanitizedImageUrl = parts[0];
			qs = parts[1];
		}

		return (sanitizedImageUrl, qs);
	}

	/// <inheritdoc />
	public string GetSizeSrcSetValue(string imageUrl, string sizeWidths, int maxPixelDensity, int widthRequest, int heightRequest, Func<(string path, int imageWidth, int imageHeight), string> pathResolver)
	{
		Guard.IsNotNullOrWhiteSpace(imageUrl);
		Guard.IsNotNullOrWhiteSpace(sizeWidths);
		Guard.IsGreaterThanOrEqualTo(maxPixelDensity, 1);
		Guard.IsGreaterThanOrEqualTo(widthRequest, 1);
		Guard.IsGreaterThanOrEqualTo(heightRequest, 1);
		Guard.IsNotNull(pathResolver);

		try
		{
			var lstSize = new HashSet<string>();

			double aspectRatio = widthRequest / (double)heightRequest;

			foreach (int sizeWidth in GetParsedIntegerItems(sizeWidths))
			{
				foreach (int density in GetPixelDensities(maxPixelDensity))
				{
					int imgWidth = sizeWidth * density;
					int imgHeight = (int)Math.Ceiling(imgWidth / aspectRatio);

					string resolvedUrl = pathResolver((imageUrl, imgWidth, imgHeight));

					_ = lstSize.Add($"{resolvedUrl} {imgWidth}w");
				}
			}

			return string.Join(", ", lstSize.OrderBy(x => x));
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { imageUrl, sizeWidths, maxPixelDensity, widthRequest, heightRequest }))
		{
			throw new UmbrellaException("There has been a problem creating the srcset value.", exc);
		}
	}
}