


using System.Web;
using System.Web.Mvc;

namespace Umbrella.Legacy.WebUtilities.Mvc.Helpers;

/// <summary>
/// Extension methods for use with the <see cref="HtmlHelper" /> type.
/// </summary>
public static class StringHelpers
{
	/// <summary>
	/// Replaces all occurences of the \n character with a HTML br tag.
	/// </summary>
	/// <param name="helper">The helper.</param>
	/// <param name="value">The value.</param>
	/// <returns>The updated HTML.</returns>
	public static IHtmlString Nl2Br(this HtmlHelper helper, string value)
		=> helper.Raw(value.NormalizeNewLines().Replace("\r\n", "<br />"));
}