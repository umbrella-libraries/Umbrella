


namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Extension methods for the <see cref="bool"/> type.
/// </summary>
public static class BooleanExtensions
{
	/// <summary>
	/// Converts to the specified <see cref="bool"/> to "Yes" when <see langword="true"/>
	/// and "No" when <see langword="false"/>.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <returns>The converted value.</returns>
	public static string ToYesNoString(this bool value) => value ? "Yes" : "No";
}