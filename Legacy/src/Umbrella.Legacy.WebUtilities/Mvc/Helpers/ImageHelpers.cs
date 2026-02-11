
using System.Web.Mvc;
using System.Web.Routing;
using Umbrella.Legacy.WebUtilities.Mvc.Tags;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extension methods for use with the <see cref="HtmlHelper"/> type, specifically for creating instance of <see cref="ResponsiveImageTag"/>.
/// </summary>
public static class ImageHelpers
{
	/// <summary>
	/// Creates a new <see cref="ResponsiveImageTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="path">The path.</param>
	/// <param name="altText">The alt text.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <returns>The generated <see cref="ResponsiveImageTag"/>.</returns>
	public static ResponsiveImageTag ResponsiveImage(this HtmlHelper helper, string path, string altText, object? htmlAttributes = null)
	{
		RouteValueDictionary attributesDictionary = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);

		return helper.ResponsiveImage(path, altText, attributesDictionary);
	}

	/// <summary>
	/// Creates a new <see cref="ResponsiveImageTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="path">The path.</param>
	/// <param name="altText">The alt text.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <returns>The generated <see cref="ResponsiveImageTag"/>.</returns>
	public static ResponsiveImageTag ResponsiveImage(this HtmlHelper helper, string path, string altText, IDictionary<string, object>? htmlAttributes)
	{
		var urlHelper = new UrlHelper(helper.ViewContext.RequestContext);

		return new ResponsiveImageTag(path, altText, htmlAttributes, urlHelper.Content);
	}
}