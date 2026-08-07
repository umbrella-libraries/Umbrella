namespace Umbrella.DataAnnotations;

/// <summary>
/// A validation attribute that ensures a <see cref="Guid"/> property is not empty (i.e., not equal to
/// <see cref="Guid.Empty"/> ). <see langword="null"/> values are considered valid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NonEmptyGuidAttribute : ValidationAttribute
{
	/// <inheritdoc/>
	public override bool IsValid(object? value) => value is null || (value is Guid guid && guid != Guid.Empty);
}