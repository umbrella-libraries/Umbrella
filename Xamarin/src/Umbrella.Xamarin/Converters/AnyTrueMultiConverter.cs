


using System.Globalization;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Converters;

/// <summary>
/// A multi-converter that returns <see langword="true"/> when any source parameters are <see langword="true"/>.
/// </summary>
/// <seealso cref="IMultiValueConverter" />
public class AnyTrueMultiConverter : IMultiValueConverter
{
	/// <inheritdoc />
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		=> values is null || !targetType.IsAssignableFrom(typeof(bool)) ? false : (object)values.OfType<bool>().Any(x => x);

	/// <inheritdoc />
	public object[]? ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}