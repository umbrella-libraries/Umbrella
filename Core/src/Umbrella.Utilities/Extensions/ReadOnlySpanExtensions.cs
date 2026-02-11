using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for use with <see cref="ReadOnlySpan{T}"/> instances.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Some methods used for benchmarking.")]
public static class ReadOnlySpanExtensions
{
	#region ToLower	
	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to lowercase using the rules of the specified <paramref name="culture"/> and writes them to the specified <paramref name="destination"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="destination">The destination.</param>
	/// <param name="culture">The culture.</param>
	/// <remarks>If the <paramref name="culture"/> is not specified, <see cref="CultureInfo.CurrentCulture"/> is used.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToLowerSlim(this in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo? culture = null) =>
#if NET462 || DEBUG
		ToLowerSlimNetClr(source, destination, culture ?? CultureInfo.CurrentCulture);
#else
            ToLowerSlimCoreClr(source, destination, culture ?? CultureInfo.CurrentCulture);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToLowerSlimNetClr(in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo culture)
	{
		Guard.IsEqualTo(source.Length, destination.Length);

		for (int i = 0; i < source.Length; i++)
		{
			destination[i] = char.ToLower(source[i], culture);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToLowerSlimCoreClr(in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo culture) => source.ToLower(destination, culture);

	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to lowercase using the rules of the invariant culture and writes them to the specified <paramref name="destination"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="destination">The destination.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToLowerInvariantSlim(this in ReadOnlySpan<char> source, in Span<char> destination) => ToLowerSlim(source, destination, CultureInfo.InvariantCulture);
	#endregion

	#region ToUpper
	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to uppercase using the rules of the specified <paramref name="culture"/> and writes them to the specified <paramref name="destination"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="destination">The destination.</param>
	/// <param name="culture">The culture.</param>
	/// <remarks>If the <paramref name="culture"/> is not specified, <see cref="CultureInfo.CurrentCulture"/> is used.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToUpperSlim(this in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo? culture = null) =>
#if NET462 || DEBUG
		ToUpperSlimNetClr(source, destination, culture ?? CultureInfo.CurrentCulture);
#else
            ToUpperSlimCoreClr(source, destination, culture ?? CultureInfo.CurrentCulture);
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToUpperSlimNetClr(in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo culture)
	{
		Guard.IsEqualTo(source.Length, destination.Length);

		for (int i = 0; i < source.Length; i++)
		{
			destination[i] = char.ToUpper(source[i], culture);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ToUpperSlimCoreClr(in ReadOnlySpan<char> source, in Span<char> destination, CultureInfo culture) => source.ToUpper(destination, culture);

	/// <summary>
	/// Converts all characters in the specified <paramref name="source"/> to uppercase using the rules of the invariant culture and writes them to the specified <paramref name="destination"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="destination">The destination.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ToUpperInvariantSlim(this in ReadOnlySpan<char> source, in Span<char> destination) => ToUpperSlim(source, destination, CultureInfo.InvariantCulture);
	#endregion
}