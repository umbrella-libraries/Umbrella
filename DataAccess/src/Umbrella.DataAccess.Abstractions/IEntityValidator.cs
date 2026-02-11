
using System.ComponentModel.DataAnnotations;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A collection of helper methods used for entity validation.
/// </summary>
public interface IEntityValidator
{
	/// <summary>
	/// Validates the property number range.
	/// </summary>
	/// <typeparam name="TProperty">The type of the property.</typeparam>
	/// <param name="value">The value.</param>
	/// <param name="propertyName">Name of the property.</param>
	/// <param name="min">The minimum.</param>
	/// <param name="max">The maximum.</param>
	/// <param name="required">Specifies if a value is required for the property.</param>
	/// <returns>The validation result.</returns>
	ValidationResult? ValidatePropertyNumberRange<TProperty>(TProperty? value, string propertyName, TProperty min, TProperty max, bool required = true) where TProperty : struct, IComparable<TProperty>;

	/// <summary>
	/// Validates the length of the property string.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <param name="propertyName">Name of the property.</param>
	/// <param name="minLength">The minimum length.</param>
	/// <param name="maxLength">The maximum length.</param>
	/// <param name="required">Specifies if a value is required for the property.</param>
	/// <returns>The validation result.</returns>
	ValidationResult? ValidatePropertyStringLength(string value, string propertyName, int minLength, int maxLength, bool required = true);
}