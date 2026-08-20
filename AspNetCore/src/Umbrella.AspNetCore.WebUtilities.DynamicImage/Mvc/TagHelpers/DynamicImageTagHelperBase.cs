using CommunityToolkit.Diagnostics;
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
	/// </summary>
	public double? FocalPointX { get; set; }

	/// <summary>
	/// Gets or sets the normalised Y coordinate of the focal point for the image, between 0 and 1 starting from the top of the image.
	/// </summary>
	public double? FocalPointY { get; set; }

	/// <summary>
	/// Gets or sets the <see cref="DynamicImageFormat"/>.
	/// </summary>
	public DynamicImageFormat ImageFormat { get; set; } = DynamicImageFormat.Jpeg;

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
	/// Asynchronously executes the <see cref="TagHelper"/> with the given <paramref name="context"/> and <paramref name="output"/>.
	/// </summary>
	/// <param name="context">Contains information associated with the current HTML tag.</param>
	/// <param name="output">A stateful HTML element used to generate an HTML tag.</param>
	public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
	{
		_ = await BuildCoreTagAsync(output).ConfigureAwait(false);

		await base.ProcessAsync(context, output).ConfigureAwait(false);
	}

	/// <summary>
	/// Builds the core tag and returns the <c>src</c> attribute.
	/// </summary>
	/// <param name="output">A stateful HTML element used to generate an HTML tag.</param>
	/// <returns>The <c>src</c> attribute of the tag.</returns>
	protected Task<string> BuildCoreTagAsync(TagHelperOutput output)
	{
		Guard.IsNotNull(output);

		if (ResizeMode is not DynamicResizeMode.UseWidth && HeightRequest <= 0)
			throw new InvalidOperationException($"A value for {nameof(HeightRequest)} must be provided when the resize mode is anything other than {nameof(DynamicResizeMode.UseWidth)}");

		if (ResizeMode is not DynamicResizeMode.UseHeight && WidthRequest <= 0)
			throw new InvalidOperationException($"A value for {nameof(WidthRequest)} must be provided when the resize mode is anything other than {nameof(DynamicResizeMode.UseHeight)}");

		TagHelperAttribute attrSrc = output.Attributes["src"];
		string? src = attrSrc?.Value?.ToString()?.Trim();

		if (string.IsNullOrEmpty(src))
			throw new UmbrellaWebException("src cannot be null or empty.");

		string strippedUrl = StripUrlPrefix(src);
		DynamicImageOptions options = CreateDynamicImageOptions(strippedUrl, WidthRequest, HeightRequest);

		string x1Url = GenerateVirtualPath(options);

		_ = output.Attributes.Remove(attrSrc);
		output.Attributes.Add("src", ResolveImageUrl(x1Url));

		output.TagName = OutputTagName;

		return Task.FromResult(strippedUrl);
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
}
