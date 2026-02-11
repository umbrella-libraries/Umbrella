


using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.ObjectModel;

namespace Umbrella.Utilities.CalendarGrid;

/// <summary>
/// Used to represent a month of a year in a calendar.
/// </summary>
/// <typeparam name="TStatus">The type of the custom status that can be associated with a day of a month.</typeparam>
/// <typeparam name="TData">The type of the custom data that can be associated with a day of a month.</typeparam>
public class CalendarGridMonth<TStatus, TData> : UmbrellaObservableRangeCollection<CalendarGridDay<TStatus, TData>>
	where TStatus : struct, Enum
	where TData : class
{
	/// <summary>
	/// Gets the name of the month.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the month number between 1 and 12 inclusive.
	/// </summary>
	public int Month { get; }

	/// <summary>
	/// Gets the year between 1 and 9999 inclusive.
	/// </summary>
	public int Year { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="CalendarGridMonth{TStatus, TData}"/> class without days specified upfront.
	/// </summary>
	/// <param name="month">The month.</param>
	/// <param name="year">The year.</param>
	public CalendarGridMonth(int month, int year)
		: this(month, year, Array.Empty<CalendarGridDay<TStatus, TData>>())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CalendarGridMonth{TStatus, TData}"/> class.
	/// </summary>
	/// <param name="month">The month.</param>
	/// <param name="year">The year.</param>
	/// <param name="collection">The collection of days in this month.</param>
	public CalendarGridMonth(int month, int year, IEnumerable<CalendarGridDay<TStatus, TData>> collection)
		: base(collection)
	{
		Guard.IsBetweenOrEqualTo(month, 1, 12, nameof(month));
		Guard.IsBetweenOrEqualTo(year, 1, 9999, nameof(year));

		Month = month;
		Year = year;

		Name = month switch
		{
			1 => "January",
			2 => "February",
			3 => "March",
			4 => "April",
			5 => "May",
			6 => "June",
			7 => "July",
			8 => "August",
			9 => "September",
			10 => "October",
			11 => "November",
			12 => "December",
			_ => ""
		};
	}
}