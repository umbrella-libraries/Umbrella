namespace Umbrella.DataAnnotations;

/// <summary>
/// Specifies that a property's value must not be below a specified minimum when a dependent property's value meets a
/// given condition.
/// </summary>
/// <remarks>
/// Use this attribute to enforce a conditional minimum value constraint on a property based on the value of another
/// property. The minimum value is only applied if the dependent property's value satisfies the specified comparison
/// condition. This attribute is commonly used in data validation scenarios where business rules require conditional
/// range enforcement. Inherits from <see cref="RangeIfAttribute"/> to provide additional conditional range validation
/// functionality.
/// </remarks>
public sealed class RangeMinIfAttribute : RangeIfAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RangeMinIfAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="operator">The operator.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="minimum">The minimum value.</param>
	public RangeMinIfAttribute(string dependentProperty, EqualityOperator @operator, object comparisonValue, double minimum)
		: base(dependentProperty, @operator, comparisonValue, minimum, double.MaxValue)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RangeMinIfAttribute"/> class with the <see cref="EqualityOperator"/>
	/// set to <see cref="EqualityOperator.EqualTo"/> .
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="minimum">The minimum value.</param>
	public RangeMinIfAttribute(string dependentProperty, object comparisonValue, double minimum)
		: base(dependentProperty, EqualityOperator.EqualTo, comparisonValue, minimum, double.MaxValue)
	{
	}
}
