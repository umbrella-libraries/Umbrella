
using System.Globalization;
using System.Reflection;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Helpers;

/// <summary>
/// A static class containing helper methods for use with Enum types.
/// </summary>
public static class EnumHelper
{
	/// <summary>
	/// Converts the string representation of the name or numeric value of one or more
	/// enumerated constants to an equivalent enumerated object. A parameter specifies
	/// whether the operation is case-sensitive. The return value indicates whether the
	/// conversion succeeded.
	/// </summary>
	/// <param name="enumType">The enumeration type to which to convert the value.</param>
	/// <param name="value">The string representation of the enumeration name or underlying value to convert.</param>
	/// <param name="ignoreCase">true to ignore case; false to consider case.</param>
	/// <param name="result">
	/// When this method returns, result contains an object of type TEnum whose value
	/// is represented by value if the parse operation succeeds. If the parse operation
	/// fails, result contains the default value of the underlying type of TEnum. Note
	/// that this value need not be a member of the TEnum enumeration. This parameter
	/// is passed uninitialized.
	/// </param>
	/// <returns>true if the value parameter was converted successfully; otherwise, false.</returns>
	public static bool TryParseEnum(Type enumType, string? value, bool ignoreCase, out object? result)
	{
		result = null;

		try
		{
			if (value is null)
				return false;

			result = Enum.Parse(enumType, value, ignoreCase);

			return true;
		}
		catch
		{
			return false;
		}
	}
}

/// <summary>
/// A static class containing helper methods for use with Enum types.
/// </summary>
/// <typeparam name="TEnum">The type of the enum.</typeparam>
public static class EnumHelper<TEnum> where TEnum : struct, Enum
{
	private static int? _minFlagValue;
	private static int? _maxFlagValue;
	private static IReadOnlyCollection<TEnum>? _allFlagsExceptMinMax;

	/// <summary>
	/// The collection of all enum values for the specified enum type.
	/// </summary>
	public static IReadOnlyCollection<TEnum> All { get; } = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToArray();

	/// <summary>
	/// The collection of all enum values for the specified enum type as objects.
	/// </summary>
	public static IReadOnlyCollection<object> AllObjects { get; } = Enum.GetValues(typeof(TEnum)).Cast<object>().ToArray();

	/// <summary>
	/// Determines if the enum type supports flags.
	/// </summary>
	public static bool IsFlags { get; } = typeof(TEnum).GetCustomAttribute<FlagsAttribute>() != null;

	/// <summary>
	/// The minimum flag value of the enum type if it supports flags.
	/// </summary>
	public static int MinFlagValue
	{
		get
		{
			if (!IsFlags)
				throw new UmbrellaException("The enum type does not support flags.");

			return _minFlagValue ??= All.Min(x => Convert.ToInt32(x, CultureInfo.InvariantCulture));
		}
	}

	/// <summary>
	/// The maximum flag value of the enum type if it supports flags.
	/// </summary>
	public static int MaxFlagValue
	{
		get
		{
			if (!IsFlags)
				throw new UmbrellaException("The enum type does not support flags.");

			return _maxFlagValue ??= All.Aggregate(0, (x, y) => x | Convert.ToInt32(y, CultureInfo.InvariantCulture));
		}
	}

	/// <summary>
	/// The collection of all enum values for the specified enum type except the <see cref="MinFlagValue"/> and <see cref="MaxFlagValue"/>.
	/// </summary>
	public static IReadOnlyCollection<TEnum> AllFlagsExceptMinMax
	{
		get
		{
			if (!IsFlags)
				throw new UmbrellaException("The enum type does not support flags.");

			return _allFlagsExceptMinMax ??= All.Where(x =>
			{
				int value = Convert.ToInt32(x, CultureInfo.InvariantCulture);

				return (MinFlagValue != 0 || value > MinFlagValue) && value < MaxFlagValue;
			}).ToArray();
		}
	}
}