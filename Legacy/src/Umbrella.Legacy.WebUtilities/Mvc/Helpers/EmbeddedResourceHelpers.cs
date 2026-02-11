
using System.Web.Mvc;
using System.Web.UI;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extension methods for use with the <see cref="UrlHelper"/> type, specifically for accessing embeddeded assembly resources.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Extension method.")]
public static class EmbeddedResourceHelpers
{
	private static readonly Page _page = new();

	/// <summary>
	/// Gets a URL that can be used to access an embedded resource.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="type">The type of the resource. This just needs to be a type inside the assembly that contains the resource.</param>
	/// <param name="resourceId">The resource identifier.</param>
	/// <returns>A URL to the resource.</returns>
	public static string GetWebResourceUrl(this UrlHelper helper, Type type, string resourceId) => _page.ClientScript.GetWebResourceUrl(type ?? typeof(EmbeddedResourceHelpers), resourceId);
}