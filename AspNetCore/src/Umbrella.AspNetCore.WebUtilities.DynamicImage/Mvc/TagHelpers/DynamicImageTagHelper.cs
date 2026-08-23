using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;

/// <summary>
/// A tag helper used to generate picture elements with format-specific URLs for use with the Dynamic Image infrastructure.
/// </summary>
/// <seealso cref="DynamicImageTagHelperBase" />
[OutputElementHint("picture")]
[HtmlTargetElement("dynamic-image", Attributes = RequiredAttributeNames, TagStructure = TagStructure.WithoutEndTag)]
public class DynamicImageTagHelper : DynamicImageTagHelperBase
{
	/// <summary>
	/// Gets or sets the size widths.
	/// </summary>
	public string? SizeWidths { get; set; }

	/// <summary>
	/// Gets the name of the output tag.
	/// </summary>
	protected override string OutputTagName => "img";

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImageTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="responsiveImageHelper">The responsive image helper.</param>
	/// <param name="dynamicImageUtility">The dynamic image utility.</param>
	/// <param name="umbrellaHostingEnvironment">The umbrella hosting environment.</param>
	/// <param name="dynamicImageTagHelperOptions">The dynamic image tag helper options.</param>
	public DynamicImageTagHelper(
		ILogger<DynamicImageTagHelper> logger,
		IUmbrellaWebHostingEnvironment umbrellaHostingEnvironment,
		IMemoryCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IResponsiveImageHelper responsiveImageHelper,
		IDynamicImageUtility dynamicImageUtility,
		DynamicImageTagHelperOptions dynamicImageTagHelperOptions)
		: base(logger, umbrellaHostingEnvironment, cache, cacheKeyUtility, responsiveImageHelper, dynamicImageUtility, dynamicImageTagHelperOptions)
	{
	}

	/// <summary>
	/// Asynchronously executes the <see cref="TagHelper"/> with the given <paramref name="context"/> and <paramref name="output"/>.
	/// </summary>
	/// <param name="context">Contains information associated with the current HTML tag.</param>
	/// <param name="output">A stateful HTML element used to generate an HTML tag.</param>
	public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
	{
		Guard.IsNotNull(context);
		Guard.IsNotNull(output);
		ValidateFocalPoint();

		string? sourceUrl = output.Attributes["src"]?.Value?.ToString()?.Trim();

		if (string.IsNullOrWhiteSpace(sourceUrl))
			throw new InvalidOperationException("A source URL is required.");

		bool isExternalUrl = sourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			|| sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
		string sourcePath;

		if (isExternalUrl)
		{
			sourcePath = sourceUrl;
			output.TagName = "img";
			output.Attributes.SetAttribute("srcset", ResponsiveImageHelper.GetPixelDensitySrcSetValue(sourceUrl, ImageMaxPixelDensity));
		}
		else
		{
			sourcePath = await BuildCoreTagAsync(output).ConfigureAwait(false);
			output.Attributes.SetAttribute("srcset", GetSrcSetValue(sourcePath, ImageFormat));
		}

		if (ImageLazyLoading)
		{
			output.Attributes.SetAttribute("loading", "lazy");
			output.Attributes.SetAttribute("decoding", "async");
		}

		var content = new HtmlContentBuilder();

		if (!isExternalUrl)
		{
			foreach (DynamicImageFormat format in DynamicImageTagHelperOptions.PictureSourceFormats.Where(x => x != ImageFormat))
			{
				var source = new TagBuilder("source")
				{
					TagRenderMode = TagRenderMode.SelfClosing
				};
				source.Attributes.Add("type", format is DynamicImageFormat.Avif ? "image/avif" : "image/webp");
				source.Attributes.Add("srcset", GetSrcSetValue(sourcePath, format));
				_ = content.AppendHtml(source);
			}
		}

		var image = new TagBuilder("img")
		{
			TagRenderMode = TagRenderMode.SelfClosing
		};

		foreach (TagHelperAttribute attribute in output.Attributes)
			image.Attributes[attribute.Name] = attribute.Value?.ToString() ?? string.Empty;

		_ = content.AppendHtml(image);
		output.Attributes.Clear();
		output.TagName = "picture";
		output.TagMode = TagMode.StartTagAndEndTag;
		_ = output.Content.SetHtmlContent(content);
	}

	private string GetSrcSetValue(string sourcePath, DynamicImageFormat format)
	{
		string cacheKey = CacheKeyUtility.Create<DynamicImageTagHelper>($"{sourcePath}:{VersionToken}:{WidthRequest}:{HeightRequest}:{ResizeMode}:{format}:{FilterQuality}:{QualityRequest}:{FocalPointX}:{FocalPointY}:{ImageMaxPixelDensity}:{SizeWidths}");

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
}
