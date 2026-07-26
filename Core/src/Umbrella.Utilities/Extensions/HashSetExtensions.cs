using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// A set of extension methods for the <see cref="HashSet{T}"/> type.
/// </summary>
public static class HashSetExtensions
{
	/// <summary>
	/// Adds a value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// </summary>
	/// <typeparam name="T">The type of elements in the <see cref="HashSet{T}"/>.</typeparam>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNull<T>(this HashSet<T> hashSet, T value)
	{
		Guard.IsNotNull(hashSet, nameof(hashSet));

		return value is not null && hashSet.Add(value);
	}

	/// <summary>
	/// Adds a string value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// In doing so, the string is also trimmed.
	/// </summary>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNullTrim(this HashSet<string> hashSet, string value)
		=> AddNotNull(hashSet!, value?.Trim());

	/// <summary>
	/// Adds a string value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// In doing do, the string value is trimmed and converted to lowercase using the rules of the current culture.
	/// </summary>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNullTrimToLower(this HashSet<string> hashSet, string value)
		=> AddNotNull(hashSet!, value?.TrimToLower());

	/// <summary>
	/// Adds a string value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// In doing do, the string value is trimmed and converted to lowercase using the rules of the invariant culture.
	/// </summary>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNullTrimToLowerInvariant(this HashSet<string> hashSet, string value)
		=> AddNotNull(hashSet!, value?.TrimToLowerInvariant());

	/// <summary>
	/// Adds a string value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// In doing do, the string value is trimmed and converted to uppercase using the rules of the current culture.
	/// </summary>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNullTrimToUpper(this HashSet<string> hashSet, string value)
		=> AddNotNull(hashSet!, value?.TrimToUpper());

	/// <summary>
	/// Adds a string value to the specified <see cref="HashSet{T}"/> only if it is not null.
	/// In doing do, the string value is trimmed and converted to uppercase using the rules of the invariant culture.
	/// </summary>
	/// <param name="hashSet">The <see cref="HashSet{T}"/> to which the <paramref name="value"/> will be added.</param>
	/// <param name="value">The value to be added to the <see cref="HashSet{T}"/>.</param>
	/// <returns>A <see langword="bool"/> indicating whether or not the value had been added to the <paramref name="hashSet"/>.</returns>
	public static bool AddNotNullTrimToUpperInvariant(this HashSet<string> hashSet, string value)
		=> AddNotNull(hashSet!, value?.TrimToUpperInvariant());

	/// <summary>
	/// Adds the specified <paramref name="values"/> to the <paramref name="set"/>.
	/// </summary>
	/// <typeparam name="T">The type of the item in the <paramref name="set"/>.</typeparam>
	/// <param name="set">The set.</param>
	/// <param name="values">The values to add.</param>
	public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> values)
	{
		Guard.IsNotNull(set);
		Guard.IsNotNull(values);

		foreach (T value in values)
		{
			if (value is null)
				continue;

			_ = set.Add(value);
		}
	}
}
