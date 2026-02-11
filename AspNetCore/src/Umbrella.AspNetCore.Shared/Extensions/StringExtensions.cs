using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Umbrella.Utilities.Constants;
using Umbrella.Utilities.Extensions;

namespace Umbrella.AspNetCore.Shared.Extensions;

/// <summary>
/// Blazor specific extension methods for use with strings.
/// </summary>
public static class StringExtensions
{
	/// <summary>
	/// Encodes the specified <paramref name="value"/> as HTML and then replaces all encoded new line characters with the specified <paramref name="replacement"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="replacement">The replacement.</param>
	/// <returns>The HTML encoded output.</returns>
	public static MarkupString ReplaceNewLines(this string? value, string replacement = "<br />")
		=> string.IsNullOrWhiteSpace(value)
		? default
		: (MarkupString)HtmlEncoder.Default.Encode(value).NormalizeHtmlEncodedNewLines().Replace(StringEncodingConstants.HtmlEncodedCrLfToken, replacement, StringComparison.Ordinal);
}