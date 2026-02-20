using System.Diagnostics.CodeAnalysis;
using Umbrella.DataAnnotations.BaseClasses;
using Umbrella.DataAnnotations.Helpers;

namespace Umbrella.DataAnnotations;

/// <summary>
/// Provides conditional range validation for a property based on the value of another property. The property is
/// validated to ensure its value falls within a specified range only if a dependent property's value satisfies a given
/// condition.
/// </summary>
/// <remarks>
/// Use this attribute to enforce range constraints on a property when another property's value meets a specified
/// comparison, such as being equal to or greater than a certain value. This is useful for scenarios where validation
/// requirements depend on the state of related properties. The range limits and comparison logic can be customized
/// using the provided constructor parameters. Inherit from this class to implement additional conditional validation
/// scenarios.
/// </remarks>
[SuppressMessage("Performance", "CA1813:Avoid unsealed attributes", Justification = "Other attributes extend this attribute so it can't be sealed.")]
public class RangeIfAttribute : ContingentValidationAttribute
{
	private readonly RangeAttribute _rangeAttribute;

	/// <summary>
	/// Gets the operator.
	/// </summary>
	public EqualityOperator Operator { get; }

	/// <summary>
	/// Gets the value that will be compared against the value of the
	/// <see cref="ContingentValidationAttribute.DependentProperty"/>
	/// </summary>
	public object ComparisonValue { get; }

	/// <summary>
	/// Gets the metadata.
	/// </summary>
	protected OperatorMetadata Metadata { get; }

	/// <summary>
	/// Gets the minimum value for the range
	/// </summary>
	public object Minimum => _rangeAttribute.Minimum;

	/// <summary>
	/// Gets the maximum value for the range
	/// </summary>
	public object Maximum => _rangeAttribute.Maximum;

#if NET8_0_OR_GREATER
	/// <summary>
	/// Specifies whether validation should fail for values that are equal to <see cref="Minimum"/> .
	/// </summary>
	public bool MinimumIsExclusive
	{
		get => _rangeAttribute.MinimumIsExclusive;
		set => _rangeAttribute.MinimumIsExclusive = value;
	}

	/// <summary>
	/// Specifies whether validation should fail for values that are equal to <see cref="Maximum"/> .
	/// </summary>
	public bool MaximumIsExclusive
	{
		get => _rangeAttribute.MaximumIsExclusive;
		set => _rangeAttribute.MaximumIsExclusive = value;
	}
#endif

	/// <summary>
	/// Gets the type of the <see cref="Minimum" /> and <see cref="Maximum" /> values (e.g. Int32, Double, or some custom
	/// type)
	/// </summary>
#if NET8_0_OR_GREATER
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
	public Type OperandType => _rangeAttribute.OperandType;

	/// <summary>
	/// Determines whether string values for <see cref="Minimum"/> and <see cref="Maximum"/> are parsed in the invariant
	/// culture rather than the current culture in effect at the time of the validation.
	/// </summary>
	public bool ParseLimitsInInvariantCulture
	{
		get => _rangeAttribute.ParseLimitsInInvariantCulture;
		set => _rangeAttribute.ParseLimitsInInvariantCulture = value;
	}

	/// <summary>
	/// Determines whether any conversions necessary from the value being validated to <see cref="OperandType"/> as set by
	/// the <c> type</c> parameter of the <see cref="RangeAttribute(Type, string, string)"/> constructor are carried out in
	/// the invariant culture rather than the current culture in effect at the time of the validation.
	/// </summary>
	/// <remarks>
	/// This property has no effects with the constructors with <see cref="int"/> or <see cref="double"/> parameters, for
	/// which the invariant culture is always used for any conversions of the validated value.
	/// </remarks>
	public bool ConvertValueInInvariantCulture
	{
		get => _rangeAttribute.ConvertValueInInvariantCulture;
		set => _rangeAttribute.ConvertValueInInvariantCulture = value;
	}

	/// <summary>
	/// Gets or sets the explicit error message string.
	/// </summary>
	/// <value>
	/// This property is intended to be used for non-localizable error messages.
	/// </value>
	public new string? ErrorMessage
	{
		get => _rangeAttribute.ErrorMessage;
		set => _rangeAttribute.ErrorMessage = value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RangeIfAttribute"/> class.
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="operator">The operator.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="minimum">The minimum value.</param>
	/// <param name="maximum">The maximum value.</param>
	public RangeIfAttribute(string dependentProperty, EqualityOperator @operator, object comparisonValue, double minimum, double maximum)
		: base(dependentProperty)
	{
		Operator = @operator;
		ComparisonValue = comparisonValue;
		Metadata = OperatorMetadata.Get(Operator);

		_rangeAttribute = new RangeAttribute(minimum, maximum);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RangeIfAttribute"/> class with the <see cref="Operator"/> set to
	/// <see cref="EqualityOperator.EqualTo"/> .
	/// </summary>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="comparisonValue">The comparison value.</param>
	/// <param name="minimum">The minimum value.</param>
	/// <param name="maximum">The maximum value.</param>
	public RangeIfAttribute(string dependentProperty, object comparisonValue, double minimum, double maximum)
		: this(dependentProperty, EqualityOperator.EqualTo, comparisonValue, minimum, maximum)
	{
	}

	/// <inheritdoc />
	public override string FormatErrorMessage(string name) => _rangeAttribute.FormatErrorMessage(name);

	/// <inheritdoc />
	public override bool IsValid(object? value, object? actualDependentPropertyValue, object model)
		=> !Metadata.IsValid(actualDependentPropertyValue, ComparisonValue, ReturnTrueOnEitherNull, this) || _rangeAttribute.IsValid(value);
}