


using System.Text.RegularExpressions;

namespace Umbrella.DataAnnotations.RegularExpressions;

/// <summary>
/// Contains regular expressions for validating URLs.
/// </summary>
public static partial class UrlRegularExpressions
{
	/// <summary>
	/// A regular expression used to validate URLs.
	/// </summary>
	/*lang=regex*/
	public const string UrlRegexString = @"^(?:(http|https):\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$";

	/// <summary>
	/// A regular expression used to validate URLs that require a scheme (http or https).
	/// </summary>
	/*lang=regex*/
	public const string UrlSchemeRequiredRegexString = @"^(?:(http|https):\/\/)[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$";

	/// <summary>
	/// A regular expression used to validate URLs.
	/// </summary>
	public static Regex UrlRegex { get; } = new(UrlRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// A regular expression used to validate URLs that require a scheme (http or https).
	/// </summary>
	public static Regex UrlSchemeRequiredRegex { get; } = new(UrlSchemeRequiredRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
}