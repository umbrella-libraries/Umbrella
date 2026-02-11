namespace Umbrella.DataAnnotations;

/// <summary>
/// Specifies that the value of a property should be greater than or equal to the value of another named property on the same type.
/// </summary>
/// <seealso cref="IsAttribute" />
public sealed class GreaterThanOrEqualToAttribute : IsAttribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GreaterThanOrEqualToAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	public GreaterThanOrEqualToAttribute(string dependentProperty)
		: base(EqualityOperator.GreaterThanOrEqualTo, dependentProperty)
	{
	}
}