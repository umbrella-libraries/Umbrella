


namespace Umbrella.DataAnnotations;

/// <summary>
/// A validation attribute that ensures the value of the property is a percentage of the value of another property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class MaxPercentageOfAttribute : IsAttribute
{
	/// <summary>
	/// Gets the maximum percentage of the dependent property. This should be a value between 0 and 1 inclusive.
	/// </summary>
	public double MaxPercentage { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="MaxPercentageOfAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="maxPercentage">The maximum percentage of the dependent property. This should be a value between 0 and 1 inclusive.</param>
	public MaxPercentageOfAttribute(string dependentProperty, double maxPercentage)
		: base(EqualityOperator.MaxPercentageOf, dependentProperty)
	{
		MaxPercentage = maxPercentage;
	}
}