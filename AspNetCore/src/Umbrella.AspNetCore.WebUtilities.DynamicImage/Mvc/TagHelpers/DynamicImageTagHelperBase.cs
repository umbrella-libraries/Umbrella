using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;
using Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.WebUtilities.Exceptions;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

/// <summary>
/// The base class used for all Dynamic Image tag helpers.
/// </summary>
/// <seealso cref="ResponsiveImageTagHelper" />
public abstract class DynamicImageTagHelperBase : ResponsiveImageTagHelper
{
	/// <summary>
	/// The required attribute names
	/// </summary>
	protected const string RequiredAttributeNames = "src," + WidthRequestAttributeName + "," + HeightRequestAttributeName;

	/// <summary>
	/// The width request attribute name
	/// </summary>
	protected const string WidthRequestAttributeName = "width-request";

	/// <summary>
	/// The height request attribute name
	/// </summary>
	protected const string HeightRequestAttributeName = "height-request";

	/// <summary>
	/// The version token attribute name.
	/// </summary>
	protected const string VersionTokenAttributeName = "version-token";

	/// <summary>
	/// The size widths attribute name.
	/// </summary>
	protected const string SizeWidthsAttributeName = "size-widths";

	/// <summary>
	/// Gets the <see cref="IDynamicImageUtility"/>.
	/// </summary>
	protected IDynamicImageUtility DynamicImageUtility { get; }

	/// <summary>
	/// Gets the dynamic image tag helper options.
	/// </summary>
	protected DynamicImageTagHelperOptions DynamicImageTagHelperOptions { get; }

	/// <summary>
	/// Gets the name of the output tag. This is abstract and always overridden.
	/// </summary>
	protected abstract string OutputTagName { get; }

	/// <summary>
	/// Gets or sets the width request in pixels.
	/// </summary>
	[HtmlAttributeName(WidthRequestAttributeName)]
	public int WidthRequest { get; set; }

	/// <summary>
	/// Gets or sets the height request in pixels.
	/// </summary>
	[HtmlAttributeName(HeightRequestAttributeName)]
	public int HeightRequest { get; set; }

	/// <summary>
	/// Gets or sets the optional version token that should be embedded in generated Dynamic Image URLs.
	/// </summary>
	[HtmlAttributeName(VersionTokenAttributeName)]
	public string? VersionToken { get; set; }

	/// <summary>
	/// Gets or sets the resize mode. Defaults to <see cref="DynamicResizeMode.Crop"/>.
	/// </summary>
	/// <remarks>
	/// For more information on how these resize modes work, please refer to the <see cref="DynamicResizeMode"/> code documentation.
	/// </remarks>
	public DynamicResizeMode ResizeMode { get; set; } = DynamicResizeMode.Crop;

	/// <summary>
	/// Gets or sets the filter quality for the dynamic image. Defaults to <see cref="DynamicImageFilterQuality.Medium"/>.
	/// </summary>
	/// <remarks>
	/// The filter quality determines the level of filtering applied during image resizing.
	/// Higher quality settings may result in better visual output but can increase processing time.
	/// </remarks>
	public DynamicImageFilterQuality FilterQuality { get; set; } = DynamicImageFilterQuality.Medium;

	/// <summary>
	/// Gets or sets the quality request. This is a value between 0-100. The quality is a suggestion, and not all formats (for example, PNG) or image libraries (e.g. FreeImage) respect or support it. Defaults to <see langword="100" />.
	/// </summary>
	public int QualityRequest { get; set; } = 100;

	/// <summary>
	/// Gets or sets the normalised X coordinate of the focal point for the image, between 0 and 1 starting from the left of the image.
	/// Only used with <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	public double? FocalPointX { get; set; }

	/// <summary>
	/// Gets or sets the normalised Y coordinate of the focal point for the image, between 0 and 1 starting from the top of the image.
	/// Only used with <see cref="DynamicResizeMode.CropFocalPoint"/>.
	/// </summary>
	public double? FocalPointY { get; set; }

	/// <summary>
	/// Gets or sets the <see cref="DynamicImageFormat"/>.
	/// </summary>
	public DynamicImageFormat ImageFormat { get; set; } = DynamicImageFormat.Jpeg;

	/// <summary>
	/// Gets or sets the size widths.
	/// </summary>
	/// <remarks>
	/// If specified, these are used in combination with the values of <see cref="ResponsiveImageTagHelper.ImageMaxPixelDensity"/>,
	/// <see cref="WidthRequest"/> and <see cref="HeightRequest"/> to set the value of the srcset attribute of the generated tags.
	/// </remarks>
	[HtmlAttributeName(SizeWidthsAttributeName)]
	public string? SizeWidths { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageTagHelperBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dynamicImageUtility">The dynamic image utility.</param>
	/// <param name="umbrellaHostingEnvironment">The umbrella hosting environment.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="responsiveImageHelper">The responsive image helper.</param>
	/// <param name="dynamicImageTagHelperOptions">The dynamic image tag helper options.</param>
	protected DynamicImageTagHelperBase(
		ILogger<DynamicImageTagHelperBase> logger,
		IUmbrellaWebHostingEnvironment umbrellaHostingEnvironment,
		IMemoryCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IResponsiveImageHelper responsiveImageHelper,
		IDynamicImageUtility dynamicImageUtility,
		DynamicImageTagHelperOptions dynamicImageTagHelperOptions)
		: base(logger, umbrellaHostingEnvironment, cache, cacheKeyUtility, responsiveImageHelper)
	{
		DynamicImageUtility = dynamicImageUtility;
		DynamicImageTagHelperOptions = dynamicImageTagHelperOptions;
	}

	/// <summary>
	/// Sets the <c>src</c> attribute and tag name of the output from an already resolved source path.
	/// </summary>
	/// <param name="output">A stateful HTML element used to generate an HTML tag.</param>
	/// <param name="sourcePath">The source path with the configured prefix already removed.</param>
	protected void ApplyResolvedSourcePath(TagHelperOutput output, string sourcePath)
	{
		Guard.IsNotNull(output);
		Guard.IsNotNullOrWhiteSpace(sourcePath);

		ValidateSizeRequests();

		DynamicImageOptions options = CreateDynamicImageOptions(sourcePath, WidthRequest, HeightRequest);

		output.Attributes.SetAttribute("src", ResolveImageUrl(GenerateVirtualPath(options)));
		output.TagName = OutputTagName;
	}

	/// <summary>
	/// Determines whether the specified URL points at an external host and therefore cannot be transformed by the Dynamic Image infrastructure.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <returns><see langword="true"/> if the URL is external; otherwise <see langword="false"/>.</returns>
	protected static bool IsExternalUrl(string? url)
		=> !string.IsNullOrEmpty(url)
			&& (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Validates that the width and height requests are compatible with the current <see cref="ResizeMode"/>.
	/// </summary>
	protected void ValidateSizeRequests()
	{
		if (ResizeMode is not DynamicResizeMode.UseWidth && HeightRequest <= 0)
			throw new InvalidOperationException($"A value for {nameof(HeightRequest)} must be provided when the resize mode is anything other than {nameof(DynamicResizeMode.UseWidth)}");

		if (ResizeMode is not DynamicResizeMode.UseHeight && WidthRequest <= 0)
			throw new InvalidOperationException($"A value for {nameof(WidthRequest)} must be provided when the resize mode is anything other than {nameof(DynamicResizeMode.UseHeight)}");
	}

	/// <summary>
	/// Validates the specified source URL and removes the configured prefix from it.
	/// </summary>
	/// <param name="src">The source URL.</param>
	/// <returns>The source path with the configured prefix removed.</returns>
	/// <exception cref="UmbrellaWebException">Thrown if <paramref name="src"/> is <see langword="null"/> or empty.</exception>
	protected string ResolveSourcePath(string? src)
	{
		string? trimmed = src?.Trim();

		return string.IsNullOrEmpty(trimmed)
			? throw new UmbrellaWebException("src cannot be null or empty.")
			: StripUrlPrefix(trimmed);
	}

	/// <summary>
	/// Builds a <![CDATA[<source>]]> tag for the specified format.
	/// </summary>
	/// <param name="sourcePath">The source path with the configured prefix already removed.</param>
	/// <param name="format">The image format.</param>
	/// <param name="media">The optional value of the <c>media</c> attribute.</param>
	/// <returns>The source tag.</returns>
	protected TagBuilder BuildSourceTag(string sourcePath, DynamicImageFormat format, string? media = null)
	{
		Guard.IsNotNullOrWhiteSpace(sourcePath);

		var source = new TagBuilder("source")
		{
			TagRenderMode = TagRenderMode.SelfClosing
		};

		if (!string.IsNullOrWhiteSpace(media))
			source.Attributes.Add("media", media);

		source.Attributes.Add("type", format.ToMimeTypeString());
		source.Attributes.Add("srcset", GetSrcSetValue(sourcePath, format));

		return source;
	}

	/// <summary>
	/// Gets the value of the <c>srcset</c> attribute for the specified source path and format using the current tag helper configuration.
	/// </summary>
	/// <param name="sourcePath">The source path with the configured prefix already removed.</param>
	/// <param name="format">The image format.</param>
	/// <returns>The <c>srcset</c> value.</returns>
	protected string GetSrcSetValue(string sourcePath, DynamicImageFormat format)
	{
		Guard.IsNotNullOrWhiteSpace(sourcePath);

		string cacheKey = CacheKeyUtility.Create<DynamicImageTagHelperBase>($"{sourcePath}:{VersionToken}:{WidthRequest}:{HeightRequest}:{ResizeMode}:{format}:{FilterQuality}:{QualityRequest}:{FocalPointX}:{FocalPointY}:{ImageMaxPixelDensity}:{SizeWidths}");

		return Cache.GetOrCreate(
			cacheKey,
			entry =>
			{
				_ = entry
					.SetAbsoluteExpiration(TimeSpan.FromHours(1))
					.SetPriority(CacheItemPriority.Low);

				IReadOnlyCollection<int> sizeWidths = ResponsiveImageHelper.GetParsedIntegerItems(SizeWidths ?? "");
				DynamicImageOptions options = CreateDynamicImageOptions(sourcePath, WidthRequest, HeightRequest, format);
				string src = ResolveImageUrl(GenerateVirtualPath(options));

				string? srcSet = sizeWidths.Count is 0
					? ResponsiveImageHelper.GetPixelDensitySrcSetValue(src, ImageMaxPixelDensity)
					: ResponsiveImageHelper.GetSizeSrcSetValue(sourcePath, SizeWidths ?? "", ImageMaxPixelDensity, WidthRequest, HeightRequest, x =>
					{
						DynamicImageOptions sizeOptions = CreateDynamicImageOptions(sourcePath, x.imageWidth, x.imageHeight, format);
						return ResolveImageUrl(GenerateVirtualPath(sizeOptions));
					});

				return string.IsNullOrWhiteSpace(srcSet) ? src : srcSet;
			}) ?? string.Empty;
	}

	/// <inheritdoc/>
	protected override string ResolveImageUrl(string url)
	{
		Guard.IsNotNullOrWhiteSpace(url);

		if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return url;

		return base.ResolveImageUrl(url);
	}

	/// <summary>
	/// Generates the virtual path for the image.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>The virtual path.</returns>
	protected virtual string GenerateVirtualPath(in DynamicImageOptions options)
	{
		Guard.IsNotNull(options);

		return DynamicImageUtility.GenerateVirtualPath(DynamicImageTagHelperOptions.DynamicImagePathPrefix, options);
	}

	/// <summary>
	/// Creates the dynamic image options for the current tag helper configuration.
	/// </summary>
	/// <param name="sourcePath">The source path.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="imageFormat">The optional output image format.</param>
	/// <returns>The dynamic image options.</returns>
	protected DynamicImageOptions CreateDynamicImageOptions(string sourcePath, int width, int height, DynamicImageFormat? imageFormat = null)
		=> new(
			sourcePath,
			width,
			height,
			ResizeMode,
			imageFormat ?? ImageFormat,
			FilterQuality,
			QualityRequest,
			FocalPointX,
			FocalPointY,
			VersionToken);

	/// <summary>
	/// Removes the configured prefix from the specified URL if it is present.
	/// </summary>
	/// <remarks>The prefix to remove is specified by the <see cref="DynamicImageTagHelperOptions.StripPrefix"/> property. The comparison is
	/// case-insensitive. If the prefix is not set or not present at the start of the URL, the original URL is returned
	/// unchanged.</remarks>
	/// <param name="url">The URL string from which to remove the prefix. Cannot be null or empty.</param>
	/// <returns>A string containing the URL with the prefix removed if it was present; otherwise, the original URL.</returns>
	protected string StripUrlPrefix(string url)
	{
		Guard.IsNotNullOrEmpty(url);

		return !string.IsNullOrEmpty(DynamicImageTagHelperOptions.StripPrefix) && url.StartsWith(DynamicImageTagHelperOptions.StripPrefix, StringComparison.OrdinalIgnoreCase) ? url[DynamicImageTagHelperOptions.StripPrefix.Length..] : url;
	}

	/// <summary>
	/// Validates the focal point configuration.
	/// </summary>
	private protected void ValidateFocalPoint()
	{
		if (FocalPointX.HasValue != FocalPointY.HasValue)
			throw new ArgumentException($"Both {nameof(FocalPointX)} and {nameof(FocalPointY)} must be defined if either is specified.");

		if (!FocalPointX.HasValue)
			return;

		Guard.IsBetweenOrEqualTo(FocalPointX.Value, 0, 1);
		Guard.IsBetweenOrEqualTo(FocalPointY!.Value, 0, 1);

		if (ResizeMode is not DynamicResizeMode.CropFocalPoint)
			throw new InvalidOperationException($"{nameof(FocalPointX)} and {nameof(FocalPointY)} can only be used with {nameof(DynamicResizeMode.CropFocalPoint)}.");
	}
}
