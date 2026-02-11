


using System.Globalization;
using Umbrella.DataAnnotations.Helpers;

namespace Umbrella.DataAnnotations;

/// Specifies that a data field is required to match a specified regular expression contingent on whether another property
/// on the same object as the property this attribute is being used on matches conditions specified
/// using the constructor.
public sealed class RegularExpressionIfAttribute : RequiredIfAttribute
{
	/// <summary>
	/// Gets or sets the regex pattern.
	/// </summary>
	public string Pattern { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="RegularExpressionIfAttribute"/> class.
	/// </summary>
	/// <param name="pattern">The pattern.</param>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="operator">The operator.</param>
	/// <param name="comparisonValue">The dependent value.</param>
	public RegularExpressionIfAttribute(string pattern, string dependentProperty, EqualityOperator @operator, object comparisonValue)
		: base(dependentProperty, @operator, comparisonValue)
	{
		Pattern = pattern;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RegularExpressionIfAttribute"/> class.
	/// </summary>
	/// <param name="pattern">The pattern.</param>
	/// <param name="dependentProperty">The dependent property.</param>
	/// <param name="comparisonValue">The dependent value.</param>
	public RegularExpressionIfAttribute(string pattern, string dependentProperty, object comparisonValue)
		: this(pattern, dependentProperty, EqualityOperator.EqualTo, comparisonValue)
	{
	}

	/// <inheritdoc />
	public override bool IsValid(object? value, object? actualDependentPropertyValue, object model)
		=> !Metadata.IsValid(actualDependentPropertyValue, ComparisonValue, ReturnTrueOnEitherNull, this) || OperatorMetadata.Get(EqualityOperator.RegExMatch).IsValid(value, Pattern, ReturnTrueOnEitherNull, this);

	/// <inheritdoc />
	protected override IEnumerable<KeyValuePair<string, object>> GetClientValidationParameters()
		=> base.GetClientValidationParameters()
			.Union(new[]
			{
				new KeyValuePair<string, object>("Pattern", Pattern),
			});

	/// <inheritdoc />
	public override string FormatErrorMessage(string name)
	{
		if (string.IsNullOrEmpty(ErrorMessageResourceName) && string.IsNullOrEmpty(ErrorMessage))
			ErrorMessage = DefaultErrorMessageFormat;

		return string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, DependentProperty, ComparisonValue, Pattern);
	}

	/// <inheritdoc />
	public override string DefaultErrorMessageFormat => "{0} must be in the format of {3} due to {1} being " + Metadata.ErrorMessage + " {2}";
}