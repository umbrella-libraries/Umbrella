


using System.Globalization;
using Umbrella.Utilities.CalendarGrid;
using Umbrella.Utilities.Extensions;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Converters;

/// <summary>
/// A converter that splits a collection of <see cref="CalendarGridMonth{TStatus, TData}" /> items into groups of 7 for displaying as a calendar on screen.
/// </summary>
/// <typeparam name="TStatus">The type of the status of a <see cref="CalendarGridDay{TStatus, TData}"/>.</typeparam>
/// <typeparam name="TData">The type of the custom data stored against a <see cref="CalendarGridDay{TStatus, TData}"/>.</typeparam>
public class CalendarGridMonthGroupConverter<TStatus, TData> : IValueConverter
	where TStatus : struct, Enum
	where TData : class
{
	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is CalendarGridMonth<TStatus, TData> month && month.Count > 0
		? month.Split(7)
		: Array.Empty<CalendarGridDay<TStatus, TData>[]>();

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}