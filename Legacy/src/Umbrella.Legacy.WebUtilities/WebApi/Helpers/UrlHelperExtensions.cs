using System.Web.Mvc;
using System.Web.Routing;

namespace Umbrella.Legacy.WebUtilities.WebApi.Helpers;

/// <summary>
/// <see cref="UrlHelper"/> extension methods targeting WebAPI.
/// </summary>
public static class UrlHelperExtensions
{
	/// <summary>
	/// Used to generate an outbound URL for a WebAPI controller.
	/// </summary>
	/// <param name="helper">The <see cref="UrlHelper"/> instance.</param>
	/// <param name="controller">The name of the WebAPI controller.</param>
	/// <param name="values">Route values.</param>
	/// <param name="routeName">The name of the route.</param>
	/// <returns>The generated URL.</returns>
	public static string RouteWebApiUrl(this UrlHelper helper, string controller, IDictionary<string, object>? values = null, string routeName = "DefaultApi")
	{
		if (helper is null)
			throw new ArgumentNullException(nameof(helper));

		values ??= new Dictionary<string, object>();

		values.Add("httproute", "");
		values.Add("controller", controller);

		return helper.RouteUrl(routeName, new RouteValueDictionary(values)).ToLowerInvariant();
	}
}
