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
/// <remarks>
/// Nested <see cref="DynamicImagePictureSourceTagHelper"/> elements can be used to contribute art directed sources that are rendered
/// before the automatically generated format sources.
/// </remarks>
/// <seealso cref="DynamicImageTagHelperBase" />
[OutputElementHint("picture")]
[HtmlTargetElement("dynamic-image", Attributes = RequiredAttributeNames, TagStructure = TagStructure.NormalOrSelfClosing)]
[HtmlTargetElement("dynamic-image", Attributes = "image,width-request,height-request", TagStructure = TagStructure.NormalOrSelfClosing)]
public class DynamicImageTagHelper : DynamicImageTagHelperBase
{
	/// <summary>
	/// The attributes that describe the image itself rather than the element wrapping it, and are therefore rendered on the generated
	/// <c>&lt;img&gt;</c> even when declared in the view.
	/// </summary>
	/// <remarks>
	/// These are the content attributes HTML defines on <c>img</c>, plus the accessibility attributes, which belong on the element carrying
	/// the image role. Everything else a view declares - <c>class</c>, <c>id</c>, <c>style</c>, <c>title</c>, <c>data-</c> attributes - is
	/// left on the <c>&lt;picture&gt;</c>, which is the element the view wrote.
	/// </remarks>
	private static readonly HashSet<string> _imageAttributeNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"alt", "crossorigin", "decoding", "fetchpriority", "height", "ismap", "loading",
		"longdesc", "referrerpolicy", "sizes", "src", "srcset", "usemap", "width"
	};

	private DynamicImagePictureContext? _pictureContext;

	/// <summary>
	/// Gets the name of the output tag.
	/// </summary>
	protected override string OutputTagName => "picture";

	/// <summary>
	/// Gets the attributes applied to the generated <c>&lt;img&gt;</c>.
	/// </summary>
	/// <remarks>
	/// Everything this tag helper generates for the image itself - the <c>src</c>, the <c>srcset</c>, the loading hints - belongs here rather
	/// than on the output, because the output is the <c>&lt;picture&gt;</c>. Anything the view declared on the element stays on the output and
	/// is therefore rendered on the <c>&lt;picture&gt;</c>, which is the element the view actually wrote.
	/// </remarks>
	protected TagHelperAttributeList ImageAttributes { get; } = [];

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
	/// Initializes the tag helper and creates the <see cref="DynamicImagePictureContext"/> shared with any nested
	/// <see cref="DynamicImagePictureSourceTagHelper"/> instances.
	/// </summary>
	/// <param name="context">Contains information associated with the current HTML tag.</param>
	/// <remarks>
	/// This runs after the Razor infrastructure has assigned the bound properties of this tag helper and before the scope of any child
	/// tag helper is created, which is what allows children to inherit the values declared here.
	/// </remarks>
	public override void Init(TagHelperContext context)
	{
		Guard.IsNotNull(context);

		base.Init(context);
		ApplyImageDescriptor(context);

		_pictureContext = new DynamicImagePictureContext
		{
			SourcePathResolver = ResolveSourcePathAsync,
			ParentTagHelperContext = context,
			VersionToken = VersionToken,
			ResizeMode = ResizeMode,
			ImageFormat = ImageFormat,
			FilterQuality = FilterQuality,
			QualityRequest = QualityRequest,
			MaxPixelDensity = ImageMaxPixelDensity,
			SizeWidths = SizeWidths,
			FocalPointX = FocalPointX,
			FocalPointY = FocalPointY,
			FocalPointApproval = FocalPointApproval
		};

		context.Items[typeof(DynamicImagePictureContext)] = _pictureContext;
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

		// Resolved through the shared context so that this element and any nested sources agree on the image, and so that an overridden
		// ResolveSourcePathAsync runs once no matter how many of them ask for it. Init always assigns the context before this runs.
		string sourcePath = await _pictureContext!.ResolveParentSourcePathAsync().ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(sourcePath))
			throw new InvalidOperationException("A source URL is required.");

		bool isExternalUrl = IsExternalUrl(sourcePath);

		if (isExternalUrl)
		{
			ImageAttributes.SetAttribute("src", sourcePath);
			ImageAttributes.SetAttribute("srcset", GetPixelDensitySrcSetValue(sourcePath));
		}
		else
		{
			ApplyResolvedSourcePath(output, ImageAttributes, sourcePath);
			ImageAttributes.SetAttribute("srcset", GetSrcSetValue(sourcePath, ImageFormat));
		}

		if (ImageLazyLoading)
		{
			ImageAttributes.SetAttribute("loading", "lazy");
			ImageAttributes.SetAttribute("decoding", "async");
		}

		// Executing the child content is what runs any nested dynamic-source tag helpers. They suppress their own output and instead
		// append their generated source tags to the shared picture context.
		TagHelperContent childContent = await output.GetChildContentAsync().ConfigureAwait(false);

		if (!childContent.IsEmptyOrWhiteSpace)
			throw new InvalidOperationException("Only <dynamic-source> elements can be nested inside a <dynamic-image> element.");

		// A nested source rejects an external parent itself, before it generates any URLs, so this is empty whenever the parent is external.
		List<IHtmlContent> artDirectionSources = _pictureContext!.Sources;
		var content = new HtmlContentBuilder();

		// Art directed sources must be rendered first. Browsers use the first source with a matching media condition and a supported
		// type, so the format sources below would otherwise win regardless of the media conditions declared on the children.
		foreach (IHtmlContent source in artDirectionSources)
			_ = content.AppendHtml(source);

		if (!isExternalUrl)
		{
			foreach (DynamicImageFormat format in DynamicImageTagHelperOptions.PictureSourceFormats.Where(x => x != ImageFormat))
				_ = content.AppendHtml(BuildSourceTag(sourcePath, format));
		}

		// Everything the view declared is on the output, which is the picture. Those that describe the image rather than the wrapper are moved
		// across, so that alt in particular lands where it means something. The rest stay where the view put them.
		foreach (TagHelperAttribute attribute in output.Attributes.Where(x => IsImageAttribute(x.Name)).ToArray())
		{
			// Anything already generated wins. The declared src is the path the resized URLs were derived from, not a value to render.
			if (!ImageAttributes.ContainsName(attribute.Name))
				ImageAttributes.SetAttribute(attribute);

			_ = output.Attributes.Remove(attribute);
		}

		var image = new TagBuilder("img")
		{
			TagRenderMode = TagRenderMode.SelfClosing
		};

		foreach (TagHelperAttribute attribute in ImageAttributes)
			image.Attributes[attribute.Name] = attribute.Value?.ToString() ?? string.Empty;

		_ = content.AppendHtml(image);

		output.TagName = OutputTagName;
		output.TagMode = TagMode.StartTagAndEndTag;
		_ = output.Content.SetHtmlContent(content);
	}

	/// <summary>
	/// Determines whether an attribute declared in the view describes the image rather than the <c>&lt;picture&gt;</c> wrapping it, and should
	/// therefore be rendered on the generated <c>&lt;img&gt;</c>.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <returns><see langword="true" /> if the attribute belongs on the image; otherwise <see langword="false" />.</returns>
	/// <remarks>
	/// Accessibility attributes are included because the image is the element carrying the role, so a label or description declared in the
	/// view has to travel with it rather than being left on a wrapper that conveys nothing.
	/// </remarks>
	protected virtual bool IsImageAttribute(string name)
		=> !string.IsNullOrEmpty(name)
			&& (_imageAttributeNames.Contains(name)
				|| name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
				|| name.Equals("role", StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Resolves the source path of the image described by the specified tag helper context.
	/// </summary>
	/// <param name="context">The context to resolve the source path from. This is the context of this element, or that of a nested
	/// <c>dynamic-source</c> element that declares a source of its own.</param>
	/// <returns>The source path with the configured prefix removed, or the URL unaltered when it is external.</returns>
	/// <remarks>
	/// <para>
	/// The default implementation reads the <c>src</c> attribute. Override this to resolve a source that is only known at runtime, for
	/// example an identifier that has to be looked up in a digital asset management system, which is why the result is a task.
	/// </para>
	/// <para>
	/// This runs before any nested source is executed and its result is shared with them, so an override does not need to be concerned with
	/// ordering. The result is retained per context, so an override performing I/O is invoked once per distinct element rather than once per
	/// generated tag.
	/// </para>
	/// </remarks>
	protected virtual Task<string> ResolveSourcePathAsync(TagHelperContext context)
	{
		Guard.IsNotNull(context);

		string? sourceUrl = context.AllAttributes["image"]?.Value is DynamicImageDescriptor descriptor
			? descriptor.Url
			: context.AllAttributes["src"]?.Value?.ToString()?.Trim();

		if (string.IsNullOrEmpty(sourceUrl))
			return Task.FromResult(string.Empty);

		return Task.FromResult(IsExternalUrl(sourceUrl) ? sourceUrl : StripUrlPrefix(sourceUrl));
	}
}
