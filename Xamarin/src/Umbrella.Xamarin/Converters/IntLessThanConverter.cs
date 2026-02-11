


using System.Globalization;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Converters;

/// <summary>
/// A converter that is used to determine if a specified integer value is less than the supplied parameter value.
/// </summary>
/// <seealso cref="IValueConverter" />
public class IntLessThanConverter : IValueConverter
{
	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		=> value is int source && ((parameter is int target && source < target) || (int.TryParse(parameter as string, out target) && source < target));

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}