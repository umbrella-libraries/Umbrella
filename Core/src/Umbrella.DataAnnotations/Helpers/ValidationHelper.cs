using System.Collections;

namespace Umbrella.DataAnnotations.Helpers;

internal static class ValidationHelper
{
	private static readonly MinLengthAttribute _minLengthAttribute = new(1);

	public static bool IsNonEmptyCollection(object? value) => value switch
	{
		null => false,
		Array => _minLengthAttribute.IsValid(value),
		ICollection collection => collection.Count > 0,
		IEnumerable enumerable => enumerable.GetEnumerator().MoveNext(),
		_ => throw new NotImplementedException("The value being validated must be of type Array or implement ICollection.")
	};
}
