using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Constants;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods that operation on <see langword="string"/> instances.
/// </summary>
public static partial class StringExtensions
{
	// lang=regex
	private const string HtmlTagPattern = @"<.*?>";

	private static readonly Regex _htmlTagPatternRegex = CreateHtmlTagPatternRegex();

	/// <summary>
	/// Removes zero-width whitespace characters from the specified <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A new string with the preambles removed.</returns>
	public static string RemovePreambles(this string value)
	{
		Guard.IsNotNull(value);

#if NET6_0_OR_GREATER
		return value.Replace(StringEncodingConstants.PreambleString, null, StringComparison.Ordinal);
#else
		return value.Replace(StringEncodingConstants.PreambleString, null);
#endif
	}

	/// <summary>
	/// Normalizes new line characters to ensure consistency between Windows and Unix platforms.
	/// All new lines will be represented as \r\n strings.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A new string with the new lines normalized.</returns>
	public static string NormalizeNewLines(this string value)
	{
		Guard.IsNotNull(value);

#if NET6_0_OR_GREATER
		return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
#else
		return value.Replace("\r\n", "\n").Replace("\n", "\r\n");
#endif
	}

	/// <summary>
	/// Normalizes new line characters in a HTML encoded string to ensure consistency between Windows and Unix platforms.
	/// All new lines will be represented as encoded \r\n strings.
	/// </summary>
	/// <param name="encodedValue">The encoded value.</param>
	/// <returns>A new HTML encoded string with the new lines normalized.</returns>
	public static string NormalizeHtmlEncodedNewLines(this string encodedValue)
	{
		Guard.IsNotNull(encodedValue);

#if NET6_0_OR_GREATER
		return encodedValue.Replace(StringEncodingConstants.HtmlEncodedCrLfToken, StringEncodingConstants.HtmlEncodedLfToken, StringComparison.Ordinal).Replace(StringEncodingConstants.HtmlEncodedLfToken, StringEncodingConstants.HtmlEncodedCrLfToken, StringComparison.Ordinal);
#else
		return encodedValue.Replace(StringEncodingConstants.HtmlEncodedCrLfToken, StringEncodingConstants.HtmlEncodedLfToken).Replace(StringEncodingConstants.HtmlEncodedLfToken, StringEncodingConstants.HtmlEncodedCrLfToken);
#endif
	}

	/// <summary>
	/// Reduces the whitespace in the specified <paramref name="value"/> by replacing multiple whitespace
	/// characters with single whitespace characters.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A new string with reduced whitespace.</returns>
	public static string ReduceWhitespace(this string value)
	{
		Guard.IsNotNull(value);

		var newString = new StringBuilder();

		bool previousIsWhitespace = false;

		for (int i = 0; i < value.Length; i++)
		{
			if (value[i] == ' ')
			{
				if (previousIsWhitespace)
				{
					continue;
				}

				previousIsWhitespace = true;
			}
			else
			{
				previousIsWhitespace = false;
			}

			_ = newString.Append(value[i]);
		}

		return newString.ToString();
	}

	/// <summary>
	/// Strips HTML non-breaking space values from the <paramref name="value"/> and replaces them with empty strings.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The result or <see langword="null"/> if the <paramref name="value"/> provided was <see langword="null"/>.</returns>
	public static string StripNbsp(this string value)
	{
		Guard.IsNotNull(value);

#if NET6_0_OR_GREATER
		return value.Replace("&nbsp;", "", StringComparison.Ordinal);
#else
		return value.Replace("&nbsp;", "");
#endif
	}

	/// <summary>
	/// Determines whether the specified <paramref name="value"/> has a length between the <paramref name="minLength"/> and <paramref name="maxLength"/> inclusive.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="minLength">The minimum length.</param>
	/// <param name="maxLength">The maximum length.</param>
	/// <param name="allowNull">if set to <see langword="true"/> and the specified <paramref name="value"/> is <see langword="null"/> or whitespace, the return value will be <see langword="true"/>.</param>
	/// <returns>
	///   <see langword="true"/> if the <paramref name="value"/> is between <paramref name="minLength"/> and <paramref name="maxLength"/> inclusive,
	///   or <see langword="true"/> if the <paramref name="value"/> is <see langword="null"/> or whitespace and <paramref name="allowNull"/> is <see langword="true"/>.
	///   All other conditions will return <see langword="false"/>.
	/// </returns>
	public static bool IsValidLength(this string? value, int minLength, int maxLength, bool allowNull = true) => string.IsNullOrWhiteSpace(value) ? allowNull : value!.Length >= minLength && value.Length <= maxLength;

	/// <summary>
	/// Strips HTML tags from the specified <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The value with all HTML tags removed.</returns>
	[Obsolete("Use the dotnet toolkit when released.")]
	public static string StripHtml(this string value)
	{
		Guard.IsNotNull(value);

		return _htmlTagPatternRegex.Replace(value, string.Empty);
	}

	/// <summary>
	/// Converts the any HTML br tags to line breaks.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The converted value.</returns>
	public static string ConvertHtmlBrTagsToLineBreaks(this string value)
	{
		if (string.IsNullOrEmpty(value))
			return value;

		var sb = new StringBuilder(value);

		_ = sb.ConvertHtmlBrTagsToReplacement(Environment.NewLine);

		return sb.ToString();
	}

	/// <summary>
	/// Returns a value indicating whether a specified substring occurs within this string.
	/// </summary>
	/// <param name="target">The string to check.</param>
	/// <param name="value">The string to seek.</param>
	/// <param name="comparisonType">The type of <see cref="StringComparison"/> to use to locate the <paramref name="value"/> in the <paramref name="target"/>.</param>
	/// <returns>true if the value parameter occurs within this string, otherwise, false.</returns>
	public static bool Contains(this string? target, string? value, StringComparison comparisonType) => target is not null && value is not null && target.Contains(value, comparisonType);

	/// <summary>
	/// Converts the specified <paramref name="value"/> to the enum type <typeparamref name="T"/>. If the conversion fails,
	/// the <paramref name="defaultValue"/> is returned.
	/// </summary>
	/// <typeparam name="T">The enum type.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="defaultValue">The default value.</param>
	/// <returns>The enum value or the default value.</returns>
	public static T ToEnum<T>(this string? value, T defaultValue = default) where T : struct, Enum => Enum.TryParse(value, true, out T result) ? result : defaultValue;

	/// <summary>
	/// Trims the specified <paramref name="value"/> and converts it to lowercase using the specified <paramref name="culture"/>.
	/// If the<paramref name= "culture" /> is <see langword= "null" />, <see cref="CultureInfo.CurrentCulture" /> is used.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="culture">The optional culture. If this is <see langword="null" />, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
	/// <returns>The converted string or <see langword="null"/> if the <paramref name="value"/> was null.</returns>
	public static string TrimToLower(this string value, CultureInfo? culture = null)
	{
		if (string.IsNullOrEmpty(value))
			return value;

		ReadOnlySpan<char> span = value.AsSpan().Trim();
		Span<char> lowerSpan = span.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[span.Length] : new char[span.Length];
		span.ToLowerSlim(lowerSpan, culture ?? CultureInfo.CurrentCulture);

		return lowerSpan.ToString();
	}

	/// <summary>
	/// Trims the specified <paramref name="value"/> and converts it to lowercase using the invariant culture.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The converted string or <see langword="null"/> if the <paramref name="value"/> was null.</returns>
	public static string TrimToLowerInvariant(this string value)
		=> TrimToLower(value, CultureInfo.InvariantCulture);

	/// <summary>
	/// Trims the specified <paramref name="value"/> and converts it to uppercase using the specified <paramref name="culture"/>.
	/// If the <paramref name="culture"/> is <see langword="null" />, <see cref="CultureInfo.CurrentCulture"/> is used.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="culture">The optional culture. If this is <see langword="null" />, <see cref="CultureInfo.CurrentCulture"/> is used.</param>
	/// <returns>The converted string or <see langword="null"/> if the <paramref name="value"/> was null.</returns>
	public static string TrimToUpper(this string value, CultureInfo? culture = null)
	{
		if (string.IsNullOrEmpty(value))
			return value;

		ReadOnlySpan<char> span = value.AsSpan().Trim();
		Span<char> upperSpan = span.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[span.Length] : new char[span.Length];
		span.ToUpperSlim(upperSpan, culture!);

		return upperSpan.ToString();
	}

	/// <summary>
	/// Trims the specified <paramref name="value"/> and converts it to uppercase using the invariant culture.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The converted string or <see langword="null"/> if the <paramref name="value"/> was null.</returns>
	public static string TrimToUpperInvariant(this string value)
		=> TrimToUpper(value, CultureInfo.InvariantCulture);

	/// <summary>
	/// Attempts to convert the name of a person into some kind of normalized format
	/// where the name is not presently in a reasonable format, e.g. riCHARd would be better transformed
	/// into Richard. This method only deals with the simplest of cases currently.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The normalized name</returns>
	public static string? ToPersonNameCase(this string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return name;

		Span<char> span = name!.Length <= StackAllocConstants.MaxCharSize ? stackalloc char[name.Length] : new char[name.Length];

		for (int i = 0; i < name.Length; i++)
		{
			char currentChar = name[i];

			// First letter should always be uppercase
			if (i == 0)
			{
				span[i] = char.ToUpper(currentChar, CultureInfo.InvariantCulture);
				continue;
			}

			span[i] = char.ToLower(currentChar, CultureInfo.InvariantCulture);

			if (currentChar == '-' && i < name.Length - 1)
			{
				// Ensure the next letter is in uppercase as we are most likely dealing with a double barrelled name
				span[i + 1] = char.ToUpper(name[i + 1], CultureInfo.InvariantCulture);
				i++;
				continue;
			}
		}

		return span.ToString();
	}

	/// <summary>
	/// Determines whether the specified <paramref name="url"/> is an absolute URL by
	/// checking for a <c>http://</c> or <c>https://</c> prefix, using a <see cref="StringComparison.OrdinalIgnoreCase"/> equality check.
	/// </summary>
	/// <param name="url">The URL.</param>
	/// <returns>
	///   <see langword="true"/> if the <paramref name="url"/> is absolute; otherwise, <see langword="false"/>.
	/// </returns>
	public static bool IsAbsoluteUrl(this string url)
	{
		if (string.IsNullOrWhiteSpace(url))
			return false;

		return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Remaps the international characters in the to their to their ASCII equaivalents, if they can be.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>A string with all non-ASCII characters remapped</returns>
	public static string RemapInternationalCharactersToAscii(this string value)
	{
		Guard.IsNotNull(value);

		int initialLength = value.Length * 2;

		Span<char> span = initialLength <= StackAllocConstants.MaxCharSize ? stackalloc char[initialLength] : new char[initialLength];
		int trueLength = 0;

		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];

			if (c < 128)
			{
				span[i] = c;
				trueLength++;
				continue;
			}

			string converted = c.RemapInternationalCharacterToAscii();

			for (int j = 0; j < converted.Length; j++)
			{
				span[i + j] = converted[j];
				i += converted.Length - 1;
			}

			trueLength += converted.Length;
		}

		Span<char> output = trueLength <= StackAllocConstants.MaxCharSize ? stackalloc char[trueLength] : new char[trueLength];

		for (int i = 0; i < output.Length; i++)
		{
			output[i] = span[i];
		}

		return output.ToString();
	}

	internal static string RemapInternationalCharactersToAscii_StringBuilder(this string value)
	{
		StringBuilder sb = new();

		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];

			_ = c < 128
				? sb.Append(c)
#if NET6_0_OR_GREATER
				: sb.Append(c.RemapInternationalCharacterToAscii());
#else
				: sb.Append(c.RemapInternationalCharacterToAscii());
#endif
		}

		return sb.ToString();
	}

	extension(string? left)
	{
		/// <summary>
		/// Determines whether the current string and the specified string are equal, using a constant-time comparison to help
		/// prevent timing attacks.
		/// </summary>
		/// <remarks>
		/// This method performs a constant-time comparison, which is useful for scenarios where resistance to timing attacks
		/// is important, such as cryptographic operations. If both strings are null, the method returns true. If only one is
		/// null, or if the strings have different lengths, the method returns false.
		/// </remarks>
		/// <param name="right">The string to compare with the current string. Can be null.</param>
		/// <returns>true if the current string and <paramref name="right"/> are equal; otherwise, false.</returns>
		public bool EqualsConstantTime(string? right)
		{
			if (left is null || right is null)
				return left is null && right is null;

			ReadOnlySpan<char> a = left.AsSpan();
			ReadOnlySpan<char> b = right.AsSpan();

			if (a.Length != b.Length)
				return false;

			int diff = 0;

			for (int i = 0; i < a.Length; i++)
				diff |= a[i] ^ b[i];

			return diff == 0;
		}
	}

#if NET7_0_OR_GREATER
	[GeneratedRegex(HtmlTagPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateHtmlTagPatternRegex();
#else
	private static Regex CreateHtmlTagPatternRegex() => new(HtmlTagPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
#endif
}