using System.Globalization;
using System.Runtime.CompilerServices;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for use with <see cref="Span{T}"/> instances.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Some methods used for benchmarking.")]
public static class SpanExtensions
{
	#region Write String	
	/// <summary>
	/// Writes the specified <paramref name="value"/> to the specified <paramref name="span"/> at the <paramref name="startIndex"/>.
	/// </summary>
	/// <param name="span">The span.</param>
	/// <param name="startIndex">The start index.</param>
	/// <param name="value">The value.</param>
	/// <returns>
	/// The position of the span after writing the specified <paramref name="value"/>.
	/// e.g. if the span has a length of 10 and a string with length 5 is written from <paramref name="startIndex"/> 2,
	/// this method will return 7.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Write(this in Span<char> span, int startIndex, string value)
	{
		CommunityToolkit.Diagnostics.Guard.IsNotNull(value);

#if NET462 || DEBUG
		return WriteInternalNetClr(span, startIndex, value);
#else
		return WriteInternalCoreClr(span, startIndex, value);
#endif
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int WriteInternalNetClr(in Span<char> source, int startIndex, string value)
	{
		for (int i = 0; i < value.Length; i++)
		{
			source[startIndex++] = value[i];
		}

		return startIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int WriteInternalCoreClr(in Span<char> source, int startIndex, string value)
	{
		value.AsSpan().CopyTo(source.Slice(startIndex, value.Length));

		return startIndex + value.Length;
	}
	#endregion

	#region Write ReadOnlySpan<char>
	/// <summary>
	/// Writes the specified <paramref name="value"/> to the specified <paramref name="span"/> at the <paramref name="startIndex"/>.
	/// </summary>
	/// <param name="span">The span.</param>
	/// <param name="startIndex">The start index.</param>
	/// <param name="value">The value.</param>
	/// <returns>
	/// The position of the span after writing the specified <paramref name="value"/>.
	/// e.g. if the span has a length of 10 and a value with length 5 is written from <paramref name="startIndex"/> 2,
	/// this method will return 7.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Write(this in Span<char> span, int startIndex, in ReadOnlySpan<char> value) =>
#if NET462 || DEBUG
		WriteInternalNetClr(span, startIndex, value);
#else
            WriteInternalCoreClr(span, startIndex, value);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int WriteInternalNetClr(in Span<char> source, int startIndex, in ReadOnlySpan<char> value)
	{
		for (int i = 0; i < value.Length; i++)
		{
			source[startIndex++] = value[i];
		}

		return startIndex;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int WriteInternalCoreClr(in Span<char> source, int startIndex, in ReadOnlySpan<char> value)
	{
		value.CopyTo(source.Slice(startIndex, value.Length));

		return startIndex + value.Length;
	}
	#endregion

	#region ToLower	
	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to lowercase using the rules of the specified <paramref name="culture"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="culture">The culture.</param>
	/// <remarks>If the <paramref name="culture"/> is not specified, <see cref="CultureInfo.CurrentCulture"/> is used.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToLower(this in Span<char> source, CultureInfo? culture = null) =>
#if NET462 || DEBUG
		ToLowerNetClr(source, culture ?? CultureInfo.CurrentCulture);
#else
            ToLowerCoreClr(source, culture ?? CultureInfo.CurrentCulture);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToLowerNetClr(in Span<char> source, CultureInfo culture)
	{
		for (int i = 0; i < source.Length; i++)
		{
			source[i] = char.ToLower(source[i], culture);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToLowerCoreClr(in Span<char> source, CultureInfo culture)
	{
		for (int i = 0; i < source.Length; i++)
		{
			source[i] = char.ToLower(source[i], culture);
		}
	}

	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to lowercase using the rules of the invariant culture.
	/// </summary>
	/// <param name="source">The source.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToLowerInvariant(this in Span<char> source) => ToLower(source, CultureInfo.InvariantCulture);
	#endregion

	#region ToUpper	
	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to uppercase using the rules of the specified <paramref name="culture"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="culture">The culture.</param>
	/// <remarks>If the <paramref name="culture"/> is not specified, <see cref="CultureInfo.CurrentCulture"/> is used.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToUpper(this in Span<char> source, CultureInfo? culture = null) =>
#if NET462 || DEBUG
		ToUpperNetClr(source, culture ?? CultureInfo.CurrentCulture);
#else
            ToUpperCoreClr(source, culture ?? CultureInfo.CurrentCulture);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToUpperNetClr(in Span<char> source, CultureInfo culture)
	{
		for (int i = 0; i < source.Length; i++)
		{
			source[i] = char.ToUpper(source[i], culture);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToUpperCoreClr(in Span<char> source, CultureInfo culture)
	{
		for (int i = 0; i < source.Length; i++)
		{
			source[i] = char.ToUpper(source[i], culture);
		}
	}

	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to uppercase using the rules of the invariant culture.
	/// </summary>
	/// <param name="source">The source.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToUpperInvariant(this in Span<char> source) => ToUpper(source, CultureInfo.InvariantCulture);
	#endregion
}
