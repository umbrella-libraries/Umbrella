using System.Globalization;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using Microsoft.Owin;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extension methods for both the <see cref="HtmlHelper"/> and <see cref="IOwinContext"/> types for use with accessing the AntiForgery Token
/// for the current request.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Extension methods.")]
public static class AntiForgeryHelper
{
	/// <summary>
	/// Generated a data attribute containing the current anti-forgery token. The data attribute name is: data-request-verification-token
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <returns>The data attribute containing the token.</returns>
	public static HtmlString RequestVerificationToken(this HtmlHelper helper)
	{
		IOwinContext context = HttpContext.Current.GetOwinContext();

		return new HtmlString(string.Format(CultureInfo.InvariantCulture, "data-request-verification-token=\"{0}\"", GetAntiForgeryTokenValue(context)));
	}

	/// <summary>
	/// Gets the anti forgery token value for the current request.
	/// </summary>
	/// <param name="context">The context.</param>
	/// <returns>The token.</returns>
	public static string GetAntiForgeryTokenValue(this IOwinContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		string value = context.Request.Cookies["_RequestVerificationToken"];

		AntiForgery.GetTokens(value, out string cookieToken, out string formToken);

		return cookieToken + ":" + formToken;
	}
}
