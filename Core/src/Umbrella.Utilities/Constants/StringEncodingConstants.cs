


using System.Text;
using System.Text.Encodings.Web;

namespace Umbrella.Utilities.Constants;

/// <summary>
/// Defines commonly used string encoding constants.
/// </summary>
public static class StringEncodingConstants
{
	/// <summary>
	/// The preamble string. This is also referred to as the zero-width non-breaking whitespace character.
	/// </summary>
	public static readonly string PreambleString = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

	/// <summary>
	/// The HTML encoded CRLF string, i.e. \r\n
	/// </summary>
	public static readonly string HtmlEncodedCrLfToken = HtmlEncoder.Default.Encode("\r\n");

	/// <summary>
	/// The HTML encoded LF string, i.e. \n
	/// </summary>
	public static readonly string HtmlEncodedLfToken = HtmlEncoder.Default.Encode("\n");
}