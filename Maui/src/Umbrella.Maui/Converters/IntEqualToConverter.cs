
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Umbrella.Maui.Converters;

/// <summary>
/// A converter that is used to determine if a specified integer value is equal to the supplied parameter value.
/// </summary>
/// <seealso cref="IValueConverter" />
public class IntEqualToConverter : IValueConverter
{
	/// <inheritdoc />
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is int source && (parameter is int target && source == target || int.TryParse(parameter as string, out target) && source == target);

	/// <inheritdoc />
	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}