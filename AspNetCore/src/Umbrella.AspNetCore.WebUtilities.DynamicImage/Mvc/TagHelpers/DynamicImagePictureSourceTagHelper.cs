using CommunityToolkit.Diagnostics;
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
/// A tag helper used to generate source tags for picture elements that have the correct URLs for use with the DynamicImage infrastructure.
/// </summary>
/// <seealso cref="DynamicImageTagHelperBase" />
[OutputElementHint("source")]
[HtmlTargetElement("dynamic-source", Attributes = RequiredAttributeNames, ParentTag = "picture", TagStructure = TagStructure.WithoutEndTag)]
public class DynamicImagePictureSourceTagHelper : DynamicImageTagHelperBase
{
	/// <summary>
	/// Gets the name of the output tag.
	/// </summary>
	protected override string OutputTagName => "source";

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicImagePictureSourceTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="responsiveImageHelper">The responsive image helper.</param>
	/// <param name="dynamicImageUtility">The dynamic image utility.</param>
	/// <param name="umbrellaHostingEnvironment">The umbrella hosting environment.</param>
	/// <param name="dynamicImageTagHelperOptions">The dynamic image tag helper options.</param>
	public DynamicImagePictureSourceTagHelper(
		ILogger<DynamicImagePictureSourceTagHelper> logger,
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
	/// <returns>A <see cref="Task"/> that on completion updates the output.</returns>
	public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
	{
		Guard.IsNotNull(output);

		await base.ProcessAsync(context, output).ConfigureAwait(false);

		_ = output.Attributes.RemoveAll("alt");
		_ = output.Attributes.RemoveAll("src");
	}
}
