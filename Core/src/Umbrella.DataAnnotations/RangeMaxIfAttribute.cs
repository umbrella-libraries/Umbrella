namespace Umbrella.DataAnnotations;

/// <summary>
/// Specifies that a property's value must not exceed a specified maximum when a dependent property's value meets a
/// given condition.
/// </summary>
/// <remarks>
/// Use this attribute to enforce a conditional maximum value constraint on a property based on the value of another
/// property. The maximum value is only applied if the dependent property's value satisfies the specified comparison
/// condition. This attribute is commonly used in data validation scenarios where business rules require conditional
/// range enforcement. Inherits from <see cref="RangeIfAttribute"/> to provide additional conditional range validation
/// functionality.
/// </remarks>
public sealed class RangeMaxIfAttribute : RangeIfAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RangeMaxIfAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="operator">The operator.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="maximum">The maximum value.</param>
	public RangeMaxIfAttribute(string dependentProperty, EqualityOperator @operator, object comparisonValue, double maximum)
		: base(dependentProperty, @operator, comparisonValue, double.MinValue, maximum)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RangeMaxIfAttribute"/> class with the <see cref="EqualityOperator"/>
	/// set to <see cref="EqualityOperator.EqualTo"/> .
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="maximum">The maximum value.</param>
	public RangeMaxIfAttribute(string dependentProperty, object comparisonValue, double maximum)
		: base(dependentProperty, EqualityOperator.EqualTo, comparisonValue, double.MinValue, maximum)
	{
	}
}
