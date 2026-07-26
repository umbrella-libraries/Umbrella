using System.Web;
using System.Web.Mvc;
using Umbrella.Legacy.WebUtilities.Extensions;

namespace Umbrella.Legacy.WebUtilities.Mvc.Filters;

/// <summary>
/// A custom MVC action filter to ensure that responses are not cached by Internet Explorer for AJAX requests.
/// This involves User Agent sniffing to ensure only IE is targeted.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class InternetExplorerCacheHeaderFilterAttribute : ActionFilterAttribute
{
	/// <inheritdoc />
	public override void OnResultExecuted(ResultExecutedContext filterContext)
	{
		if (filterContext is null)
			throw new ArgumentNullException(nameof(filterContext));

		var response = filterContext.HttpContext.Response;

		if (response.IsSuccessStatusCode())
		{
			bool isInternetExplorer = filterContext.HttpContext.Request.IsInternetExplorer();

			if (isInternetExplorer)
			{
				bool isXMLHttpRequest = filterContext.HttpContext.Request.IsAjaxOrFetchRequest();

				if (isXMLHttpRequest)
				{
					// Set standard HTTP/1.0 no-cache header (no-store, no-cache, must-revalidate)
					// Set IE extended HTTP/1.1 no-cache headers (post-check, pre-check)
					response.Cache.SetCacheability(HttpCacheability.NoCache);
					response.Cache.AppendCacheExtension("no-store, must-revalidate, post-check=0, pre-check=0");

					// Set standard HTTP/1.0 no-cache header.
					response.AppendHeader("Pragma", "no-cache");

					// Set the Expires header.
					response.AppendHeader("Expires", "0");
				}
			}
		}

		base.OnResultExecuted(filterContext);
	}
}
