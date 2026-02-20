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
	extension(string? value)
	{
		/// <summary>
		/// Encodes the specified <paramref name="value"/> as HTML and then replaces all encoded new line characters with the
		/// specified <paramref name="replacement"/> .
		/// </summary>
		/// <param name="replacement">The replacement.</param>
		/// <param name="encodeHtml">Whether to encode the HTML.</param>
		/// <returns>The HTML encoded output.</returns>
		public MarkupString ReplaceNewLines(string replacement = "<br />", bool encodeHtml = true)
			=> string.IsNullOrWhiteSpace(value)
			? default
			: (MarkupString)(encodeHtml ? HtmlEncoder.Default.Encode(value) : value).NormalizeHtmlEncodedNewLines().Replace(StringEncodingConstants.HtmlEncodedCrLfToken, replacement, StringComparison.Ordinal);
	}
}