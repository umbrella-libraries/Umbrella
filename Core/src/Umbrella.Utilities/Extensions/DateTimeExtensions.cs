namespace Umbrella.Utilities.Extensions;

/// <summary>
/// Contains useful DateTime extension methods
/// </summary>
public static class DateTimeExtensions
{
	/// <summary>
	/// Changes the specified <paramref name="dateTime"/> instance's <see cref="DateTime.Day"/> property to 1.
	/// </summary>
	/// <param name="dateTime">The datetime to change.</param>
	/// <returns>A new instance with the <see cref="DateTime.Day"/> property set to 1.</returns>
	public static DateTime ToMonthStart(this DateTime dateTime) => dateTime.AddDays(-dateTime.Day + 1);

	/// <summary>
	/// Changes the specified <paramref name="dateTime"/> instance's <see cref="DateTime.Day"/> property to the last day of
	/// the month according to the value of it's <see cref="DateTime.Month"/> and <see cref="DateTime.Year"/> properties.
	/// </summary>
	/// <param name="dateTime">The datetime to change.</param>
	/// <returns>A new instance with the <see cref="DateTime.Day"/> property set to the last day of the month.</returns>
	public static DateTime ToMonthEnd(this DateTime dateTime) => dateTime.AddDays(DateTime.DaysInMonth(dateTime.Year, dateTime.Month) - dateTime.Day);
}