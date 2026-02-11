


using System.Globalization;
using System.Web;
using System.Web.Mvc;

namespace Umbrella.Legacy.WebUtilities.Mvc.Tags;

/// <summary>
/// A tag that can be used to render an <![CDATA[<img>]]> tag with a srcset attribute that has URLs using different pixel density values
/// using a well-known naming convention.
/// </summary>
/// <remarks>
/// <para>
/// e.g. if the path is specified as /image/pic.png, calling <see cref="WithDensities(int[])"/> with values of 1, 2, 3 will result in the following:
/// </para>
/// <para>
/// srcset="/image/pic.png 1x, /image/pic@2x.png 2x, /image/pic@3x.png"
/// </para>
/// </remarks>
/// <seealso cref="IHtmlString" />
public class ResponsiveImageTag : IHtmlString
{
	#region Protected Members
	/// <summary>
	/// Gets the path.
	/// </summary>
	protected string Path { get; }

	/// <summary>
	/// Gets the map virtual path function.
	/// </summary>
	protected Func<string, string> MapVirtualPathFunc { get; }

	/// <summary>
	/// Gets the pixel densities.
	/// </summary>
	protected HashSet<int> PixelDensities { get; } = [1];

	/// <summary>
	/// Gets the HTML attributes.
	/// </summary>
	protected Dictionary<string, string> HtmlAttributes { get; }
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="ResponsiveImageTag"/> class.
	/// </summary>
	/// <param name="path">The path.</param>
	/// <param name="altText">The alt text.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <param name="mapVirtualPath">The map virtual path.</param>
	public ResponsiveImageTag(string path, string altText, IDictionary<string, object>? htmlAttributes, Func<string, string> mapVirtualPath)
	{
		Path = path;
		MapVirtualPathFunc = mapVirtualPath ?? throw new ArgumentNullException(nameof(mapVirtualPath));

		if (htmlAttributes is null)
			HtmlAttributes = [];
		else
			HtmlAttributes = htmlAttributes.ToDictionary(x => x.Key, x => x.Value.ToString());

		HtmlAttributes.Add("src", mapVirtualPath(path));
		HtmlAttributes.Add("alt", altText);
	}
	#endregion

	#region IHtmlString Members		
	/// <summary>
	/// Converts this tag to an HTML string.
	/// </summary>
	public virtual string ToHtmlString()
	{
		var imgTag = new TagBuilder("img");

		if (PixelDensities.Count > 1)
			AddSrcsetAttribute(imgTag);

		imgTag.MergeAttributes(HtmlAttributes);

		return imgTag.ToString(TagRenderMode.SelfClosing);
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Add the specified pixel densities to the <see cref="PixelDensities"/> collection.
	/// </summary>
	/// <param name="densities">The densities.</param>
	/// <returns>The current <see cref="ResponsiveImageTag"/> instance.</returns>
	public ResponsiveImageTag WithDensities(params int[] densities)
	{
		if (densities is null)
			throw new ArgumentNullException(nameof(densities));

		foreach (int density in densities)
			_ = PixelDensities.Add(density);

		return this;
	}

	/// <summary>
	/// Applies a fixed width and height to the rendered img tag by setting both to the specified <paramref name="size"/>.
	/// </summary>
	/// <param name="size">The size.</param>
	/// <returns>The current <see cref="ResponsiveImageTag"/> instance.</returns>
	public ResponsiveImageTag WithFixedSize(int size)
	{
		string strSize = size.ToString(CultureInfo.InvariantCulture);

		HtmlAttributes["width"] = strSize;
		HtmlAttributes["height"] = strSize;

		return this;
	}

	/// <summary>
	/// Applies a fixed width and height to the rendered img tag.
	/// </summary>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <returns>The current <see cref="ResponsiveImageTag"/> instance.</returns>
	public ResponsiveImageTag WithFixedSize(int width, int height)
	{
		HtmlAttributes["width"] = width.ToString(CultureInfo.InvariantCulture);
		HtmlAttributes["height"] = height.ToString(CultureInfo.InvariantCulture);

		return this;
	}
	#endregion

	#region Protected Methods

	/// <summary>
	/// Adds the srcset attribute to the specified <paramref name="imgTag"/> based on the
	/// specified <see cref="PixelDensities"/>.
	/// </summary>
	/// <param name="imgTag">The img tag.</param>
	protected virtual void AddSrcsetAttribute(TagBuilder imgTag)
	{
		if (imgTag is null)
			throw new ArgumentNullException(nameof(imgTag));

		string path = HtmlAttributes["src"];

		int densityIndex = path.LastIndexOf('.');

		IEnumerable<string> srcsetImagePaths =
			from density in PixelDensities
			orderby density
			let densityX = $"{density}x"
			let highResImagePath = density > 1 ? path.Insert(densityIndex, $"@{densityX}") : path
			select MapVirtualPathFunc(highResImagePath) + " " + densityX;

		imgTag.Attributes["srcset"] = string.Join(", ", srcsetImagePaths);
	}
	#endregion
}