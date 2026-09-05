using CommunityToolkit.Diagnostics;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Imaging.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage;

/// <summary>
/// The state cascaded by an <see cref="UmbrellaDynamicImage"/> component to the <see cref="UmbrellaDynamicImageSource"/> components nested
/// inside it.
/// </summary>
/// <remarks>
/// As well as carrying the settings a nested source inherits when it does not declare its own, this exposes the URL generation used by both
/// components so that the two stay consistent.
/// </remarks>
public sealed class UmbrellaDynamicImageContext
{
	private readonly Func<UmbrellaDynamicImageSource, Task<string>> _sourcePathResolver;
	private readonly UmbrellaDynamicImageOptions _options;
	private readonly IDynamicImageUtility _dynamicImageUtility;
	private readonly IResponsiveImageHelper _responsiveImageHelper;

	/// <summary>
	/// Gets the settings of the parent image, which a nested source inherits from for anything it does not declare itself.
	/// </summary>
	public DynamicImageSourceSettings Settings { get; }

	internal UmbrellaDynamicImageContext(
		UmbrellaDynamicImageOptions options,
		IDynamicImageUtility dynamicImageUtility,
		IResponsiveImageHelper responsiveImageHelper,
		DynamicImageSourceSettings settings,
		Func<UmbrellaDynamicImageSource, Task<string>> sourcePathResolver)
	{
		_options = options;
		_dynamicImageUtility = dynamicImageUtility;
		_responsiveImageHelper = responsiveImageHelper;
		_sourcePathResolver = sourcePathResolver;
		Settings = settings;
	}

	/// <summary>
	/// Determines whether the specified URL points at an external host and therefore cannot be transformed by the Dynamic Image infrastructure.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <returns><see langword="true"/> if the URL is external; otherwise <see langword="false"/>.</returns>
	public static bool IsExternalUrl(string? url)
		=> !string.IsNullOrEmpty(url)
			&& (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Resolves the source path of a nested source that supplies a source of its own.
	/// </summary>
	/// <param name="source">The nested source component.</param>
	/// <returns>The source path with the configured prefix removed, or the URL unaltered when it is external.</returns>
	/// <remarks>
	/// This defers to the <see cref="UmbrellaDynamicImage.ResolveSourcePathAsync"/> of the parent component, so a single override serves the
	/// whole picture. A source that supplies nothing of its own does not call this at all and uses <see cref="Settings"/> instead, which the
	/// parent has already resolved, so each distinct image is resolved exactly once.
	/// </remarks>
	public Task<string> ResolveSourcePathAsync(UmbrellaDynamicImageSource source)
	{
		Guard.IsNotNull(source);

		return _sourcePathResolver(source);
	}

	/// <summary>
	/// Creates the picture sources for the specified settings.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <param name="media">The optional media condition applied to every generated source.</param>
	/// <param name="includeFallbackFormat">
	/// Whether a source using <see cref="DynamicImageSourceSettings.ImageFormat"/> is appended after the configured picture source formats.
	/// This is required for art directed sources because a browser that has matched a media condition will not fall back to the img element
	/// when it supports none of the formats offered for that condition.
	/// </param>
	/// <returns>The picture sources, ordered most to least preferred.</returns>
	public IReadOnlyCollection<DynamicImagePictureSource> CreateSources(DynamicImageSourceSettings settings, string? media, bool includeFallbackFormat)
	{
		Guard.IsNotNull(settings);

		IEnumerable<DynamicImageFormat> formats = _options.PictureSourceFormats.Where(x => x != settings.ImageFormat);

		if (includeFallbackFormat)
			formats = formats.Append(settings.ImageFormat);

		return [.. formats.Select(x => new DynamicImagePictureSource(x.ToMimeTypeString(), GetSrcSetValue(settings, x), media))];
	}

	/// <summary>
	/// Generates the URL for the specified settings and format at the requested width and height.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <param name="format">The image format.</param>
	/// <returns>The URL.</returns>
	public string GenerateUrl(DynamicImageSourceSettings settings, DynamicImageFormat format)
	{
		Guard.IsNotNull(settings);

		return GenerateUrl(settings, format, settings.WidthRequest, settings.HeightRequest);
	}

	/// <summary>
	/// Gets the value of the srcset attribute for the specified settings and format.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <param name="format">The image format.</param>
	/// <returns>The srcset value.</returns>
	public string GetSrcSetValue(DynamicImageSourceSettings settings, DynamicImageFormat format)
	{
		Guard.IsNotNull(settings);

		string src = GenerateUrl(settings, format, settings.WidthRequest, settings.HeightRequest);
		IReadOnlyCollection<int> sizeWidths = _responsiveImageHelper.GetParsedIntegerItems(settings.SizeWidths ?? "");

		string? srcSet = sizeWidths.Count is 0
			? _responsiveImageHelper.GetPixelDensitySrcSetValue(src, settings.MaxPixelDensity)
			: _responsiveImageHelper.GetSizeSrcSetValue(
				settings.Url,
				settings.SizeWidths ?? "",
				settings.MaxPixelDensity,
				settings.WidthRequest,
				settings.HeightRequest,
				x => GenerateUrl(settings, format, x.imageWidth, x.imageHeight));

		return string.IsNullOrWhiteSpace(srcSet) ? src : srcSet;
	}

	/// <summary>
	/// Removes the configured prefix from the specified URL if it is present.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <returns>The URL with the configured prefix removed.</returns>
	public string StripUrlPrefix(string url)
	{
		Guard.IsNotNullOrEmpty(url);

		return !string.IsNullOrEmpty(_options.StripPrefix) && url.StartsWith(_options.StripPrefix, StringComparison.OrdinalIgnoreCase)
			? url[_options.StripPrefix.Length..]
			: url;
	}

	private string GenerateUrl(DynamicImageSourceSettings settings, DynamicImageFormat format, int width, int height)
	{
		var options = new DynamicImageOptions(
			settings.Url,
			width,
			height,
			settings.ResizeMode,
			format,
			focalPointX: settings.FocalPointX,
			focalPointY: settings.FocalPointY,
			versionToken: settings.VersionToken,
			focalPointApproval: settings.FocalPointApproval);

		return _dynamicImageUtility.GenerateVirtualPath(_options.DynamicImagePathPrefix, options).TrimStart('~').Replace("//", "/", StringComparison.Ordinal);
	}
}
