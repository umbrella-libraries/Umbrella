


namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="char"/> type.
/// </summary>
public static class CharExtensions
{
	/// <summary>
	/// Remaps the international character to ASCII.
	/// </summary>
	/// <param name="character">The character.</param>
	/// <returns>The ASCII equivalent.</returns>
	/// <exception cref="ArgumentOutOfRangeException">character - The specified character must be greater than the unicode character 128.</exception>
	public static string RemapInternationalCharacterToAscii(this char character)
	{
		if (character < 128)
			throw new ArgumentOutOfRangeException(nameof(character), "The specified character must be greater than the unicode character 128.");

		char s = char.ToLowerInvariant(character);

#if NET6_0_OR_GREATER
		if ("àåáâäãåą".Contains(s, StringComparison.Ordinal))
#else
		if ("àåáâäãåą".Contains(s))
#endif
		{
			return "a";
		}
#if NET6_0_OR_GREATER
		else if ("èéêëę".Contains(s, StringComparison.Ordinal))
#else
		else if ("èéêëę".Contains(s))
#endif
		{
			return "e";
		}
#if NET6_0_OR_GREATER
		else if ("ìíîïı".Contains(s, StringComparison.Ordinal))
#else
		else if ("ìíîïı".Contains(s))
#endif
		{
			return "i";
		}
#if NET6_0_OR_GREATER
		else if ("òóôõöøőð".Contains(s, StringComparison.Ordinal))
#else
		else if ("òóôõöøőð".Contains(s))
#endif
		{
			return "o";
		}
#if NET6_0_OR_GREATER
		else if ("ùúûüŭů".Contains(s, StringComparison.Ordinal))
#else
		else if ("ùúûüŭů".Contains(s))
#endif
		{
			return "u";
		}
#if NET6_0_OR_GREATER
		else if ("çćčĉ".Contains(s, StringComparison.Ordinal))
#else
		else if ("çćčĉ".Contains(s))
#endif
		{
			return "c";
		}
#if NET6_0_OR_GREATER
		else if ("żźž".Contains(s, StringComparison.Ordinal))
#else
		else if ("żźž".Contains(s))
#endif
		{
			return "z";
		}
#if NET6_0_OR_GREATER
		else if ("śşšŝ".Contains(s, StringComparison.Ordinal))
#else
		else if ("śşšŝ".Contains(s))
#endif
		{
			return "s";
		}
#if NET6_0_OR_GREATER
		else if ("ñń".Contains(s, StringComparison.Ordinal))
#else
		else if ("ñń".Contains(s))
#endif
		{
			return "n";
		}
#if NET6_0_OR_GREATER
		else if ("ýÿ".Contains(s, StringComparison.Ordinal))
#else
		else if ("ýÿ".Contains(s))
#endif
		{
			return "y";
		}
#if NET6_0_OR_GREATER
		else if ("ğĝ".Contains(s, StringComparison.Ordinal))
#else
		else if ("ğĝ".Contains(s))
#endif
		{
			return "g";
		}
		else if (character is 'ř')
		{
			return "r";
		}
		else if (character is 'ł')
		{
			return "l";
		}
		else if (character is 'đ')
		{
			return "d";
		}
		else if (character is 'ß')
		{
			return "ss";
		}
		else if (character is 'þ')
		{
			return "th";
		}
		else if (character is 'ĥ')
		{
			return "h";
		}
		else if (character is 'ĵ')
		{
			return "j";
		}
		else
		{
			return "";
		}
	}
}