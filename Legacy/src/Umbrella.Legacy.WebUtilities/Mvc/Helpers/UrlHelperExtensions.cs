using System.Web.Mvc;
using Umbrella.Legacy.WebUtilities.Extensions;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extension methods for use with the <see cref="UrlHelper"/> type.
/// </summary>
public static class UrlHelperExtensions
{
	/// <summary>
	/// Runs the <paramref name="contentPath"/> through the <see cref="UrlHelper.Content(string)"/> method and then converts the output to lowercase using
	/// the rules of the invariant culture.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="contentPath">The content path.</param>
	/// <returns></returns>
	public static string ContentLower(this UrlHelper helper, string contentPath)
	{
		if (helper is null)
			throw new ArgumentNullException(nameof(helper));

		return helper.Content(contentPath).ToLowerInvariant();
	}

	/// <summary>
	/// Converts the relative content path to an absolute URL.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="contentPath">The content path.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <returns>The absolute URL.</returns>
	public static string ContentAbsolute(this UrlHelper helper, string contentPath, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0)
	{
		if (helper is null)
			throw new ArgumentNullException(nameof(helper));

		return contentPath.ToAbsoluteUrl(helper.RequestContext.HttpContext.Request.Url, schemeOverride, hostOverride, portOverride);
	}

	/// <summary>
	/// Converts the relative content path to an absolute URL and then converts it to lowercase using
	/// the rules of the invariant culture.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="contentPath">The content path.</param>
	/// <param name="schemeOverride">The scheme override.</param>
	/// <param name="hostOverride">The host override.</param>
	/// <param name="portOverride">The port override.</param>
	/// <returns>The absolute URL.</returns>
	public static string ContentAbsoluteLower(this UrlHelper helper, string contentPath, string? schemeOverride = null, string? hostOverride = null, int portOverride = 0)
		=> ContentAbsolute(helper, contentPath, schemeOverride, hostOverride, portOverride).ToLowerInvariant();
}
