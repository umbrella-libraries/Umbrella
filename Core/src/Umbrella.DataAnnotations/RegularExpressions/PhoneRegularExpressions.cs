
using System.Text.RegularExpressions;

namespace Umbrella.DataAnnotations.RegularExpressions;

/// <summary>
/// Contains regular expressions for validating phone numbers.
/// </summary>
public static partial class PhoneRegularExpressions
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
	public static Regex UKMobileRegex { get; } = CreateUKMobileRegex();

	/// <summary>
	/// A regular expression used to validate UK phone numbers.
	/// </summary>
	public static Regex UKPhoneRegex { get; } = CreateUKPhoneRegex();

#if NET8_0_OR_GREATER
	[GeneratedRegex(UKMobileRegexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateUKMobileRegex();

	[GeneratedRegex(UKPhoneRegexString, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateUKPhoneRegex();
#else
	private static Regex CreateUKMobileRegex() => new(UKMobileRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static Regex CreateUKPhoneRegex() => new(UKPhoneRegexString, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif
}
