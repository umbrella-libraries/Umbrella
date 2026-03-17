using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Imaging.Abstractions;
using Umbrella.WebUtilities.Hosting;

namespace Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers;

/// <summary>
/// A tag helper used to create responsive "img" tags.
/// </summary>
/// <seealso cref="TagHelper" />
[OutputElementHint("img")]
[HtmlTargetElement("img", Attributes = RequiredAttributeNames)]
public class ResponsiveImageTagHelper : TagHelper
{
	private const string RequiredAttributeNames = "src," + MaxPixelDensityAttributeName;

	/// <summary>
	/// The pixel densities attribute name.
	/// </summary>
	protected const string MaxPixelDensityAttributeName = "image-density";

	/// <summary>
	/// Gets or sets the maximum pixel density. All values below the maximum inclusive will have corresponding image URLs
	/// generated for them.
	/// </summary>
	/// <remarks>Defaults to 3.</remarks>
	[HtmlAttributeName(MaxPixelDensityAttributeName)]
	public int ImageMaxPixelDensity { get; set; } = 3;

	/// <summary>
	/// Gets or sets a value indicating whether lazy loading is enabled for the image. Defaults to <see langword="true" />
	/// </summary>
	[HtmlAttributeName("image-lazy-loading")]
	public bool ImageLazyLoading { get; set; } = true;

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the cache.
	/// </summary>
	protected IMemoryCache Cache { get; }

	/// <summary>
	/// Gets the cache key utility.
	/// </summary>
	public ICacheKeyUtility CacheKeyUtility { get; }

	/// <summary>
	/// Gets the umbrella hosting environment.
	/// </summary>
	protected IUmbrellaWebHostingEnvironment UmbrellaHostingEnvironment { get; }

	/// <summary>
	/// Gets the responsive image helper.
	/// </summary>
	public IResponsiveImageHelper ResponsiveImageHelper { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ResponsiveImageTagHelper"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="umbrellaHostingEnvironment">The umbrella hosting environment.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="responsiveImageHelper">The responsive image helper.</param>
	public ResponsiveImageTagHelper(
		ILogger<ResponsiveImageTagHelper> logger,
		IUmbrellaWebHostingEnvironment umbrellaHostingEnvironment,
		IMemoryCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IResponsiveImageHelper responsiveImageHelper)
	{
		Logger = logger;
		UmbrellaHostingEnvironment = umbrellaHostingEnvironment;
		Cache = cache;
		CacheKeyUtility = cacheKeyUtility;
		ResponsiveImageHelper = responsiveImageHelper;
	}

	/// <inheritdoc />
	public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
	{
		Guard.IsNotNull(context);
		Guard.IsNotNull(output);

		await base.ProcessAsync(context, output);

		string? path = output.Attributes["src"]?.Value as string ?? context.AllAttributes["src"]?.Value?.ToString();

		if (!string.IsNullOrWhiteSpace(path))
		{
			// Cache this using the image src attribute value and PixelDensities
			string key = CacheKeyUtility.Create<ResponsiveImageTagHelper>($"{path}:{ImageMaxPixelDensity}");

			string? srcsetValue = Cache.GetOrCreate(
				key,
				entry =>
				{
					_ = entry
						.SetAbsoluteExpiration(TimeSpan.FromHours(1))
						.SetPriority(CacheItemPriority.Low);

					return ResponsiveImageHelper.GetPixelDensitySrcSetValue(path, ImageMaxPixelDensity, ApplyPixelDensity, ResolveImageUrl);
				});

			if (!string.IsNullOrWhiteSpace(srcsetValue))
				output.Attributes.Add("srcset", srcsetValue);

			if (ImageLazyLoading)
			{
				output.Attributes.Add("loading", "lazy");
				output.Attributes.Add("decoding", "async");
			}
		}
	}

	/// <summary>
	/// Resolves the image URL.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <returns>The resolved image url.</returns>
	protected virtual string ResolveImageUrl(string url)
	{
		Guard.IsNotNullOrWhiteSpace(url);

		if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			return url;

		return UmbrellaHostingEnvironment.MapWebPath(url);
	}

	/// <summary>
	/// Applies the pixel density to the image URL. This is used to transform the image URL into a pixel density specific
	/// URL.
	/// </summary>
	/// <param name="sanitizedImageUrl">The sanitized image URL.</param>
	/// <param name="pixelDensity">The pixel density.</param>
	/// <returns>The transformed image URL.</returns>
	/// <remarks>
	/// This uses the <see cref="IResponsiveImageHelper.ApplyPixelDensity"/> method to apply the pixel density internally
	/// unless overridden.
	/// </remarks>
	protected virtual string ApplyPixelDensity(string sanitizedImageUrl, int pixelDensity) => ResponsiveImageHelper.ApplyPixelDensity(sanitizedImageUrl, pixelDensity);
}