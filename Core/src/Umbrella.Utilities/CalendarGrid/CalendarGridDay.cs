
using System.ComponentModel;
using Umbrella.Utilities.Helpers;

namespace Umbrella.Utilities.CalendarGrid;

/// <summary>
/// Used to represent an individual day stored as part of a <see cref="CalendarGridMonth{TStatus, TData}"/>.
/// </summary>
/// <typeparam name="TStatus">The enum type of the custom status of the day.</typeparam>
/// <typeparam name="TData">The data type used to store contextual data.</typeparam>
/// <seealso cref="INotifyPropertyChanged"/>
/// <remarks>
/// This type is intended for use as part of a <see cref="CalendarGridMonth{TStatus, TData}"/> and not for use in isolation.
/// Instances of this type can either represent specific dates or can be blank so that they are used as placeholders for empty days on the calendar.
/// For example, a calendar that displays dates in a grid from Monday to Sunday might start with a one or more empty dates where the month doesn't
/// begin on a Monday. The first day of the month might be a Wednesday in which case Monday and Tuesday would be represented using placeholders
/// with unspecified dates.
/// </remarks>
public class CalendarGridDay<TStatus, TData> : INotifyPropertyChanged
	where TStatus : struct, Enum
	where TData : class
{
	private TStatus _status;
	private TData? _data;

	/// <summary>
	/// Represents an empty day.
	/// </summary>
	public static readonly CalendarGridDay<TStatus, TData> Empty = new();

	/// <summary>
	/// Initializes a new instance of <see cref="CalendarGridDay{TStatus, TData}"/>.
	/// </summary>
	/// <param name="dateTime">The date of this instance. This is optional and can be left unspecified if this instance is a placeholder for an empty day on the grid.</param>
	/// <param name="status">The status of the day that this instance represents.</param>
	public CalendarGridDay(DateTime? dateTime = null, TStatus status = default)
	{
		if (dateTime.HasValue)
		{
			Day = dateTime.Value.Day;
			Date = dateTime;
			IsWeekend = dateTime.Value.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
			IsInPast = dateTime.Value < DateTime.Now.Date;
			IsToday = dateTime.Value.Date == DateTime.Now.Date;
		}

		Status = status;
	}

	/// <summary>
	/// Gets the day number between 1 and 31, depending on the month.
	/// </summary>
	public int? Day { get; }

	/// <summary>
	/// Gets the date.
	/// </summary>
	public DateTime? Date { get; }

	/// <summary>
	/// Gets a value specifying whether this day falls on a <see cref="DayOfWeek.Saturday"/> or <see cref="DayOfWeek.Sunday"/>.
	/// </summary>
	public bool IsWeekend { get; }

	/// <summary>
	/// Gets a value specifying whether this day is in the past at the time that this instance was created based on the value of <see cref="DateTime.Now"/>.
	/// </summary>
	public bool IsInPast { get; }

	/// <summary>
	/// Gets a value specifying whether this day is the current day at the time that this instance was created based on the value of <see cref="DateTime.Now"/>.
	/// </summary>
	public bool IsToday { get; }

	/// <summary>
	/// Gets or sets the enum status of this instance.
	/// </summary>
	public TStatus Status
	{
		get => _status;
		set
		{
			if (this == Empty)
				throw new InvalidOperationException("This property cannot be set for empty instances.");

			if (Day.HasValue)
				_ = INotifyPropertyChangedHelper.SetProperty(ref _status, value, this, PropertyChanged);
		}
	}

	/// <summary>
	/// Gets or sets the contextual data associated with this instance.
	/// </summary>
	public TData? Data
	{
		get => _data;
		set
		{
			if (this == Empty)
				throw new InvalidOperationException("This property cannot be set for empty instances.");

			_data = value;
		}
	}

	/// <inheritdoc />

	public event PropertyChangedEventHandler? PropertyChanged;
}