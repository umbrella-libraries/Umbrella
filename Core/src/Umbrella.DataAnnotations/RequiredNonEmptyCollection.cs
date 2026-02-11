using Umbrella.DataAnnotations.Helpers;

namespace Umbrella.DataAnnotations;

/// <summary>
/// A stricter version of the <see cref="MinLengthAttribute" /> which passes validation
/// when the collection is null. This effectively incorporates the behaviour of <see cref="RequiredAttribute"/>
/// and <see cref="MinLengthAttribute"/> in a single attritbute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredNonEmptyCollectionAttribute : ValidationAttribute
{
	/// <inheritdoc />
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		string[] memberNames = !string.IsNullOrWhiteSpace(validationContext.MemberName) ? [validationContext.MemberName] : [];

		if (value is null)
			return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);

		bool isValid = ValidationHelper.IsNonEmptyCollection(value);

		return isValid
			? ValidationResult.Success
			: new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
	}
}