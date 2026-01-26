using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Diagnostics;
using Humanizer;
using Umbrella.Utilities.Helpers;

namespace Umbrella.Utilities.Extensions;

/// <summary>
/// A set of extension methods for <see cref="Enum"/> types.
/// </summary>
/// <remarks>
/// Please note, these extension methods assume the underlying enum type is <see cref="int"/> .
/// </remarks>
public static class EnumExtensions
{
	private static readonly ConcurrentDictionary<Enum, string> _enumDisplayStringDictionary = new();
	private static readonly ConcurrentDictionary<Enum, string?> _enumShortDisplayStringDictionary = new();
	private static readonly ConcurrentDictionary<Enum, string?> _enumDisplayDescriptionStringDictionary = new();

	/// <summary>
	/// Converts the specified enum value that uses the <see cref="FlagsAttribute"/> to encapsulate multiple values into a
	/// string containing the names of the enum values using the specified parameters.
	/// </summary>
	/// <typeparam name="TEnum">The type of the enum.</typeparam>
	/// <param name="options">The options.</param>
	/// <param name="valueTransformer">The value transformer.</param>
	/// <param name="separator">The separator.</param>
	/// <returns>A string containing the names of the values encapsulated by the enum.</returns>
	public static string ToFlagsString<TEnum>(this TEnum options, Func<string, string>? valueTransformer = null, string separator = ",")
		where TEnum : struct, Enum
	{
		Guard.IsNotNull(separator, nameof(separator));

		var lstAllOption = EnumHelper<TEnum>.AllFlagsExceptMinMax;

		var lstOption = new List<string>();

		foreach (TEnum item in lstAllOption)
		{
			if (options.HasFlag(item))
				lstOption.Add(valueTransformer?.Invoke(item.ToString()) ?? item.ToString());
		}

		return string.Join(separator, lstOption);
	}

	/// <summary>
	/// Converts the specified enum value that uses the <see cref="FlagsAttribute"/> to encapsulate multiple values into a
	/// string containing the friendly display names of the enum values using the specified parameters.
	/// </summary>
	/// <typeparam name="TEnum">The type of the enum.</typeparam>
	/// <param name="options">The options.</param>
	/// <param name="separator">The separator.</param>
	/// <returns>A string containing the friendly display names of the values encapsulated by the enum.</returns>
	public static string ToFlagsDisplayString<TEnum>(this TEnum options, string separator = ",")
		where TEnum : struct, Enum
	{
		Guard.IsNotNull(separator, nameof(separator));

		var lstAllOption = EnumHelper<TEnum>.AllFlagsExceptMinMax;

		var lstOption = new List<string>();

		foreach (TEnum item in lstAllOption)
		{
			if (options.HasFlag(item))
				lstOption.Add(item.ToDisplayString());
		}

		return string.Join(separator, lstOption);
	}

	/// <summary>
	/// Converts the specified enum value to a friendly string that can be displayed in a UI by trying the following in
	/// order: <list type="bullet"> <item> Use a <see cref="DisplayAttribute"/> .</item> <item> Use a
	/// <see cref="DisplayNameAttribute"/> .</item> <item> Use Humanizer to convert the enum to a friendly string using
	/// <see cref="LetterCasing.Title"/> .</item> </list>
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The display name according to the specified rules.</returns>
	public static string ToDisplayString(this Enum value) => _enumDisplayStringDictionary.GetOrAdd(value, option => option.GetType().GetFields().Single(x => x.Name == option.ToString()).GetDisplayText());

	/// <summary>
	/// Converts the specified enum value to a friendly short string that can be displayed in a UI by trying to read the
	/// <see cref="DisplayAttribute.ShortName"/> property of a <see cref="DisplayAttribute"/> that has been applied to the
	/// member specified using the given expression.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The short display name according to the specified rules.</returns>
	public static string? ToShortDisplayString(this Enum value) => _enumShortDisplayStringDictionary.GetOrAdd(value, option => option.GetType().GetFields().Single(x => x.Name == option.ToString()).GetShortDisplayText());

	/// <summary>
	/// Converts the specified enum value to a friendly description string that can be displayed in a UI by trying the
	/// following in order: <list type="bullet"> <item> Use a <see cref="DisplayAttribute"/>.</item>
	/// <item> Use a <see cref="DescriptionAttribute"/>.</item> </list>
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The display description according to the specified rules.</returns>
	public static string? ToDisplayDescriptionString(this Enum value) => _enumDisplayDescriptionStringDictionary.GetOrAdd(value, option => option.GetType().GetFields().Single(x => x.Name == option.ToString()).GetDisplayDescriptionText());
}