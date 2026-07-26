
using System.Text.RegularExpressions;

namespace Umbrella.DataAnnotations.RegularExpressions;

/// <summary>
/// Contains regular expressions for validating postcodes.
/// </summary>
public static partial class PostcodeRegularExpressions
{
	/// <summary>
	/// A regular expression used to validate UK postcodes.
	/// </summary>
	/*lang=regex*/
	public const string UKPostcodeRegexString = "^((([A-Pa-pR-UWYZr-uwyz](\\d([A-HJKSTUWa-hjkstuw]|\\d)?|[A-Ha-hK-Yk-y]\\d([AaBbEeHhMmNnPpRrVvWwXxYy]|\\d)?))\\s*(\\d[ABD-HJLNP-UW-Zabd-hjlnp-uw-z]{2})?)|[Gg][Ii][Rr]\\s*0[Aa][Aa])$";

	/// <summary>
	/// A regular expression used to partially validate UK postcodes, i.e. just the first part.
	/// </summary>
	/*lang=regex*/
	public const string UKPartialPostcodeRegexString = @"^[a-z]{1,2}\d{1,2}.*$";

	/// <summary>
	/// A regular expression used to validate UK postcodes.
	/// </summary>
	public static readonly Regex UKPostcodeRegex = CreateUKPostcodeRegex();

	/// <summary>
	/// A regular expression used to partially validate UK postcodes, i.e. just the first part.
	/// </summary>
	public static readonly Regex UKPartialPostcodeRegex = CreateUKPartialPostcodeRegex();

#if NET8_0_OR_GREATER
	[GeneratedRegex(UKPostcodeRegexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateUKPostcodeRegex();

	[GeneratedRegex(UKPartialPostcodeRegexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateUKPartialPostcodeRegex();
#else
	private static Regex CreateUKPostcodeRegex() => new(UKPostcodeRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static Regex CreateUKPartialPostcodeRegex() => new(UKPartialPostcodeRegexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
#endif
}
