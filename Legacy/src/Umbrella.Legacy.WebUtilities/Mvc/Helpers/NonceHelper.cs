using System.Web;
using System.Web.Mvc;
using Umbrella.Legacy.WebUtilities.Extensions;
using Umbrella.WebUtilities.Exceptions;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extensions methods for use with the <see cref="HtmlHelper"/> type, specifically for use with nonce values.
/// </summary>
public static class NonceHelper
{
	/// <summary>
	/// Gets the current request nonce.
	/// </summary>
	/// <param name="htmlHelper">The HTML helper.</param>
	/// <returns>The nonce value for the current request.</returns>
	/// <exception cref="UmbrellaWebException">A nonce for the current request could not be found either in the OWIN environment or in the items dictionary of HttpContextBase.</exception>
	public static string? GetCurrentRequestNonce(this HtmlHelper htmlHelper)
	{
		if (htmlHelper is null)
			throw new ArgumentNullException(nameof(htmlHelper));

		string? value = htmlHelper.ViewContext.HttpContext.GetOwinContext().GetCurrentRequestNonce();

		if (string.IsNullOrWhiteSpace(value))
			value = htmlHelper.ViewContext.HttpContext.GetCurrentRequestNonce();

		if (string.IsNullOrWhiteSpace(value))
			throw new UmbrellaWebException($"A nonce for the current request could not be found either in the OWIN environment or in the items dictionary of HttpContextBase.");

		return value;
	}
}
