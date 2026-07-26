namespace Umbrella.DataAnnotations;

/// <summary>
/// Specifies that a data field value is both required and that it must have a value of <see langword="true" />.
/// </summary>
public sealed class RequiredTrueAttribute : ValidationAttribute
{
	/// <inheritdoc />
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value is true)
			return ValidationResult.Success;

		string? memberName = validationContext?.MemberName;

		return new ValidationResult(
			FormatErrorMessage(validationContext?.DisplayName ?? string.Empty),
			!string.IsNullOrWhiteSpace(memberName) ? [memberName!] : Array.Empty<string>());
	}
}
