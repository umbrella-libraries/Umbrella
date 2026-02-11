
using System.Text.RegularExpressions;

namespace Umbrella.DataAnnotations.RegularExpressions;

/// <summary>
/// Contains regular expressions for validating phone numbers.
/// </summary>
public static class PhoneRegularExpressions
{
	/// <summary>
	/// A regular expression used to validate UK mobile numbers.
	/// </summary>
	/*lang=regex*/
	public const string UKMobileRegexString = @"^07\d{3}\s?\d{6}$";

	/// <summary>
	/// A regular expression used to validate UK phone numbers.
	/// </summary>
	/*lang=regex*/
	public const string UKPhoneRegexString = @"^(\(?\+?[0-9]*\)?)?[0-9_\- \(\)]*$";

	/// <summary>
	/// A regular expression used to validate UK mobile numbers.
	/// </summary>
	public static Regex UKMobileRegex { get; } = new(UKMobileRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// A regular expression used to validate UK phone numbers.
	/// </summary>
	public static Regex UKPhoneRegex { get; } = new(UKPhoneRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
}