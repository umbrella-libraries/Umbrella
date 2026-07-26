using System.Web.Mvc;
using System.Web.Routing;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Legacy.WebUtilities.DynamicImage.Mvc.Tags;
using Umbrella.Legacy.WebUtilities.Extensions;

namespace Umbrella.Legacy.WebUtilities.DynamicImage.Mvc.Helpers;

/// <summary>
/// <see cref="HtmlHelper"/> extension methods for generating responsive image tags using the <see cref="Umbrella.DynamicImage"/> infrastructure.
/// </summary>
public static class ImageHelpers
{
	/// <summary>
	/// Create a new <see cref="ResponsiveDynamicImageTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The HTML helper.</param>
	/// <param name="utility">The utility.</param>
	/// <param name="path">The path.</param>
	/// <param name="altText">The alt text.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <param name="format">The format.</param>
	/// <param name="toAbsolutePath">if set to <see langword="true"/>, ensures all generated URLs are absolute.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <returns>The generated <see cref="ResponsiveDynamicImageTag"/>.</returns>
	public static ResponsiveDynamicImageTag ResponsiveDynamicImage(this HtmlHelper helper, IDynamicImageUtility utility, string path, string altText, int width, int height, DynamicResizeMode resizeMode, object? htmlAttributes = null, DynamicImageFormat format = DynamicImageFormat.Jpeg, bool toAbsolutePath = false, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0, string dynamicImagePathPrefix = DynamicImageConstants.DefaultPathPrefix)
	{
		RouteValueDictionary attributesDictionary = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);

		return helper.ResponsiveDynamicImage(utility, path, altText, width, height, resizeMode, attributesDictionary, format, toAbsolutePath, schemeOverride, hostOverride, portOverride, dynamicImagePathPrefix);
	}

	/// <summary>
	/// Create a new <see cref="ResponsiveDynamicImageTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="utility">The utility.</param>
	/// <param name="path">The path.</param>
	/// <param name="altText">The alt text.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <param name="format">The format.</param>
	/// <param name="toAbsolutePath">if set to <see langword="true"/>, ensures all generated URLs are absolute.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <returns>The generated <see cref="ResponsiveDynamicImageTag"/>.</returns>
	public static ResponsiveDynamicImageTag ResponsiveDynamicImage(this HtmlHelper helper, IDynamicImageUtility utility, string path, string altText, int width, int height, DynamicResizeMode resizeMode, IDictionary<string, object> htmlAttributes, DynamicImageFormat format = DynamicImageFormat.Jpeg, bool toAbsolutePath = false, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0, string dynamicImagePathPrefix = DynamicImageConstants.DefaultPathPrefix)
	{
		if (helper is null)
			throw new ArgumentNullException(nameof(helper));

		var urlHelper = new UrlHelper(helper.ViewContext.RequestContext);

		return new ResponsiveDynamicImageTag(utility, dynamicImagePathPrefix, path, altText, width, height, resizeMode, htmlAttributes, format, urlHelper.Content, toAbsolutePath, helper.ViewContext.RequestContext.HttpContext.Request, schemeOverride, hostOverride, portOverride);
	}

	/// <summary>
	/// Create a new <see cref="ResponsiveDynamicImagePictureSourceTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="utility">The utility.</param>
	/// <param name="path">The path.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="mediaAttributeValue">The media attribute value.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <param name="format">The format.</param>
	/// <param name="toAbsolutePath">if set to <see langword="true"/>, ensures all generated URLs are absolute.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <returns>The generated <see cref="ResponsiveDynamicImagePictureSourceTag"/>.</returns>
	public static ResponsiveDynamicImagePictureSourceTag ResponsiveDynamicImagePictureSource(this HtmlHelper helper, IDynamicImageUtility utility, string path, int width, int height, DynamicResizeMode resizeMode, string mediaAttributeValue, object? htmlAttributes = null, DynamicImageFormat format = DynamicImageFormat.Jpeg, bool toAbsolutePath = false, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0, string dynamicImagePathPrefix = DynamicImageConstants.DefaultPathPrefix)
	{
		RouteValueDictionary attributesDictionary = HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes);

		return helper.ResponsiveDynamicImagePictureSource(utility, path, width, height, resizeMode, mediaAttributeValue, attributesDictionary, format, toAbsolutePath, schemeOverride, hostOverride, portOverride, dynamicImagePathPrefix);
	}

	/// <summary>
	/// Create a new <see cref="ResponsiveDynamicImagePictureSourceTag"/> using the specified parameters.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="utility">The utility.</param>
	/// <param name="path">The path.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="resizeMode">The resize mode.</param>
	/// <param name="mediaAttributeValue">The media attribute value.</param>
	/// <param name="htmlAttributes">The HTML attributes.</param>
	/// <param name="format">The format.</param>
	/// <param name="toAbsolutePath">if set to <see langword="true"/>, ensures all generated URLs are absolute.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <param name="dynamicImagePathPrefix">The dynamic image path prefix.</param>
	/// <returns>The generated <see cref="ResponsiveDynamicImagePictureSourceTag"/>.</returns>
	public static ResponsiveDynamicImagePictureSourceTag ResponsiveDynamicImagePictureSource(this HtmlHelper helper, IDynamicImageUtility utility, string path, int width, int height, DynamicResizeMode resizeMode, string mediaAttributeValue, IDictionary<string, object> htmlAttributes, DynamicImageFormat format = DynamicImageFormat.Jpeg, bool toAbsolutePath = false, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0, string dynamicImagePathPrefix = DynamicImageConstants.DefaultPathPrefix)
	{
		if (helper is null)
			throw new ArgumentNullException(nameof(helper));

		if (utility is null)
			throw new ArgumentNullException(nameof(utility));

		var urlHelper = new UrlHelper(helper.ViewContext.RequestContext);

		var options = new DynamicImageOptions(path, width, height, resizeMode, format);

		string url = utility.GenerateVirtualPath(dynamicImagePathPrefix, options);

		if (toAbsolutePath)
			url = url.ToAbsoluteUrl(helper.ViewContext.RequestContext.HttpContext.Request.Url, schemeOverride, hostOverride, portOverride);

		return new ResponsiveDynamicImagePictureSourceTag(url, mediaAttributeValue, htmlAttributes, urlHelper.Content);
	}
}
