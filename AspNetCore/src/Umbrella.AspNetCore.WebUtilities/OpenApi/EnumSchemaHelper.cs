#if NET10_0_OR_GREATER
using System.Collections.Concurrent;

namespace Umbrella.AspNetCore.WebUtilities.OpenApi;

/// <summary>
/// Shared helpers for documenting enums, used by <see cref="UmbrellaEnumSchemaTransformer"/> and
/// <see cref="UmbrellaEnumParameterOperationTransformer"/> so that the two cannot describe the same enum differently.
/// </summary>
internal static class EnumSchemaHelper
{
	private static readonly ConcurrentDictionary<Type, bool> _flagsCache = new();
	private static readonly ConcurrentDictionary<Type, object[]> _distinctValueCache = new();

	/// <summary>
	/// Determines whether the enum is a set of combinable flags.
	/// </summary>
	/// <param name="type">The enum type.</param>
	/// <returns><see langword="true"/> when the enum is decorated with <see cref="FlagsAttribute"/>.</returns>
	/// <remarks>
	/// A flags enum must never be constrained with a schema <c>enum</c> keyword. Combined values such as
	/// <c>Read | Write</c> are legal but are not declared members, so listing only the members would document valid
	/// input as invalid.
	/// </remarks>
	public static bool IsFlags(Type type) => _flagsCache.GetOrAdd(type, static x => x.IsDefined(typeof(FlagsAttribute), inherit: false));

	/// <summary>
	/// Gets the declared values of an enum, collapsing aliases.
	/// </summary>
	/// <param name="type">The enum type.</param>
	/// <returns>One value per distinct underlying number, in declaration order.</returns>
	/// <remarks>
	/// Several names may share a single underlying value. Emitting one entry per name would produce a schema
	/// <c>enum</c> containing duplicates, which JSON Schema requires to be unique.
	/// </remarks>
	public static IReadOnlyList<object> GetDistinctValues(Type type) => _distinctValueCache.GetOrAdd(type, static x =>
	{
		HashSet<string> seen = new(StringComparer.Ordinal);
		List<object> distinct = [];

		foreach (object value in Enum.GetValues(x))
		{
			if (seen.Add(ToNumericString(value)))
				distinct.Add(value);
		}

		return [.. distinct];
	});

	/// <summary>
	/// Gets the underlying value of an enum member as a string.
	/// </summary>
	/// <param name="value">The enum value.</param>
	/// <returns>The underlying value.</returns>
	/// <remarks>
	/// The format specifier is used rather than a numeric conversion because an enum may be backed by
	/// <see cref="ulong"/>, whose upper range cannot be represented as an <see cref="long"/>.
	/// </remarks>
	public static string ToNumericString(object value) => ((Enum)value).ToString("D");

	/// <summary>
	/// Builds the description used to explain which values an enum accepts.
	/// </summary>
	/// <param name="description">The existing description, if any.</param>
	/// <param name="type">The enum type.</param>
	/// <param name="asString">Specifies whether the enum is documented using its member names.</param>
	/// <returns>The description.</returns>
	public static string BuildDescription(string? description, Type type, bool asString)
	{
		IReadOnlyList<object> values = GetDistinctValues(type);

		IEnumerable<string> parts = asString
			? values.Select(x => Enum.GetName(type, x)).OfType<string>()
			: values.Select(x => $"{ToNumericString(x)} = {Enum.GetName(type, x)}");

		string summary = IsFlags(type)
			? $"Combinable values: {string.Join(", ", parts)}. Values may be combined."
			: $"Permitted values: {string.Join(", ", parts)}.";

		return string.IsNullOrWhiteSpace(description) ? summary : description.TrimEnd() + " " + summary;
	}
}
#endif
