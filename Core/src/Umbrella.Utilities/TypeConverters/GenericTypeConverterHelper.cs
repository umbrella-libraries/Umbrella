using System.Globalization;

namespace Umbrella.Utilities.TypeConverters;

/// <summary>
/// A helper class used to convert values between types. This class is an internal API and should not be called from consumer code.
/// </summary>
public static class GenericTypeConverterHelper
{
	/// <summary>
	/// Converts the specified value to the specified type.
	/// </summary>
	/// <typeparam name="T">The target value type.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="fallbackCreator">The fallback creator.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <param name="cultureInfo">The culture information.</param>
	/// <returns>The converted value.</returns>
	public static T Convert<T>(string? value, Func<T> fallbackCreator, Func<string?, T>? customValueConverter = null, CultureInfo? cultureInfo = null)
	{
		Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

		if (!string.IsNullOrEmpty(value))
			return customValueConverter is not null ? customValueConverter(value) : (T)System.Convert.ChangeType(value, type, cultureInfo ?? CultureInfo.CurrentCulture);

		T? fallback = fallbackCreator is not null ? fallbackCreator() : default;

		return type == typeof(string) && fallback is null
			? (T)System.Convert.ChangeType(string.Empty, type, cultureInfo ?? CultureInfo.CurrentCulture)
			: fallback!;
	}

	/// <summary>
	/// Converts the specified value to the specified type.
	/// </summary>
	/// <typeparam name="T">The target value type.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="fallback">The fallback.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The converted value.</returns>
	public static T Convert<T>(string? value, T fallback = default!, Func<string?, T>? customValueConverter = null) => Convert(value, () => fallback, customValueConverter)!;

	/// <summary>
	/// Converts the <paramref name="value"/> to the specified enum type.
	/// </summary>
	/// <typeparam name="T">The enum type.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="fallback">The fallback.</param>
	/// <returns>The converted value.</returns>
	public static T ConvertToEnum<T>(string? value, T fallback = default)
		where T : struct, Enum
	{
		if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out T output))
			return output;

		return fallback;
	}

	/// <summary>
	/// Converts the <paramref name="value"/> to the specified nullable enum type.
	/// </summary>
	/// <typeparam name="T">The nullable enum type.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="fallback">The fallback.</param>
	/// <returns>The converted value.</returns>
	public static T? ConvertToNullableEnum<T>(string? value, T? fallback = null)
		where T : struct, Enum
	{
		if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out T output))
			return output;

		return fallback;
	}
}