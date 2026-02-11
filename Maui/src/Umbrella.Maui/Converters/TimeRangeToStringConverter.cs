
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Umbrella.Maui.Converters;

/// <summary>
/// A converter used to convert two instances of <see cref="TimeSpan"/> to a string representing a time range in 12-hour clock format,
/// e.g. 07:00:00 and 16:00:00 will be converted to "07:00 AM - 04:00 PM"
/// </summary>
/// <seealso cref="IMultiValueConverter" />
public class TimeRangeToStringConverter : IMultiValueConverter
{
	/// <inheritdoc />
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		TimeSpan?[] timeSpans = values.OfType<TimeSpan?>().ToArray();

		return timeSpans.Length is 2 && timeSpans.All(x => x.HasValue)
			? FormatTime(timeSpans[0]!.Value) + " - " + FormatTime(timeSpans[1]!.Value)
			: parameter;
	}

	/// <inheritdoc />
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();

	private static string FormatTime(in TimeSpan time)
	{
		string period = time.Hours < 12 ? "AM" : "PM";
		int periodHours = time.Hours > 12 ? time.Hours - 12 : time.Hours;

		return $"{periodHours:00}:{time.Minutes:00} {period}";
	}
}