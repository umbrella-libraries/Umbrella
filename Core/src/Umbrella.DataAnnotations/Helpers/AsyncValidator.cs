using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Umbrella.DataAnnotations.Helpers;

/// <summary>
/// Provides asynchronous validation methods for validating properties and objects using <see cref="ValidationAttribute"/>s with support for
/// <see cref="AsyncValidationAttribute"/>s.
/// </summary>
public static class AsyncValidator
{
	private static readonly ValidationAttributeStore _store = ValidationAttributeStore.Instance;

	/// <summary>
	/// Attempts to validate a property value using the specified validation context and attributes, and returns a value
	/// that indicates whether the value is valid.
	/// </summary>
	/// <remarks>If <paramref name="validationResults"/> is provided, all validation errors are added to the
	/// collection. If it is null, the method returns after the first validation error is found. This method does not throw
	/// on validation failure; instead, it returns <see langword="false"/> and populates the results collection if
	/// provided.</remarks>
	/// <param name="value">The value of the property to validate. May be null if the property allows null values.</param>
	/// <param name="validationContext">The context information about the property being validated. Must not be null.</param>
	/// <param name="validationResults">A collection to receive any validation errors. If null, validation stops on the first error found.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the property value
	/// is valid; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="validationContext"/> is null.</exception>
	public static async Task<bool> TryValidatePropertyAsync(object? value, ValidationContext validationContext,
		ICollection<ValidationResult>? validationResults, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		var propertyType = _store.GetPropertyType(validationContext);
		string propertyName = validationContext.MemberName!;

		EnsureValidPropertyType(propertyName, propertyType, value);

		bool result = true;
		bool breakOnFirstError = validationResults == null;

		var attributes = _store.GetPropertyValidationAttributes(validationContext);

		var errors = await GetValidationErrorsAsync(value, validationContext, attributes, breakOnFirstError, cancellationToken).ConfigureAwait(false);

		foreach (var err in errors)
		{
			result = false;
			validationResults?.Add(err.ValidationResult);
		}

		return result;
	}

	/// <summary>
	/// Asynchronously validates the specified object and its properties using the provided validation context.
	/// </summary>
	/// <param name="instance">The object to validate. Cannot be null.</param>
	/// <param name="validationContext">The context that describes the object to validate, including information such as service providers and items.
	/// Cannot be null.</param>
	/// <param name="validationResults">A collection to hold any validation errors. If null, validation results are not returned.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the object is
	/// valid; otherwise, <see langword="false"/>.</returns>
	public static Task<bool> TryValidateObjectAsync(object instance, ValidationContext validationContext, ICollection<ValidationResult>? validationResults, CancellationToken cancellationToken = default)
		=> TryValidateObjectAsync(instance, validationContext, validationResults, false, cancellationToken);

	/// <summary>
	/// Asynchronously validates the specified object and its properties using the provided validation context and
	/// validation rules.
	/// </summary>
	/// <remarks>If <paramref name="validationResults"/> is provided, all validation errors are collected;
	/// otherwise, validation stops at the first error. This method supports cancellation via the <paramref
	/// name="cancellationToken"/> parameter.</remarks>
	/// <param name="instance">The object to validate. Must match the object specified in <paramref name="validationContext"/>.</param>
	/// <param name="validationContext">The context that describes the object to validate, including information such as the object instance and service
	/// providers. Cannot be null.</param>
	/// <param name="validationResults">A collection to receive the validation results. If null, validation stops on the first validation failure and no
	/// results are returned.</param>
	/// <param name="validateAllProperties">true to validate all properties; otherwise, false to validate only properties marked for required validation.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task result is true if the object is valid;
	/// otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> or <paramref name="validationContext"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="instance"/> does not match <paramref name="validationContext"/>.ObjectInstance.</exception>
	public static async Task<bool> TryValidateObjectAsync(object instance, ValidationContext validationContext,
		ICollection<ValidationResult>? validationResults, bool validateAllProperties, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (instance is null)
			throw new ArgumentNullException(nameof(instance));

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		if (instance != validationContext.ObjectInstance)
			throw new ArgumentException(SR.Validator_InstanceMustMatchValidationContextInstance, nameof(instance));

		bool result = true;
		bool breakOnFirstError = validationResults == null;

		var errors = await GetObjectValidationErrorsAsync(instance, validationContext, validateAllProperties, breakOnFirstError, cancellationToken).ConfigureAwait(false);

		foreach (var err in errors)
		{
			result = false;
			validationResults?.Add(err.ValidationResult);
		}

		return result;
	}

	/// <summary>
	/// Attempts to validate the specified value using the provided validation attributes and context, and returns a value
	/// that indicates whether the value is valid.
	/// </summary>
	/// <remarks>If <paramref name="validationResults"/> is provided, all validation errors are added to the
	/// collection. If it is null, the method returns as soon as the first validation error is found.</remarks>
	/// <param name="value">The value to validate. May be null if the validation attributes allow it.</param>
	/// <param name="validationContext">The context information about the object being validated. Cannot be null.</param>
	/// <param name="validationResults">A collection to receive any validation errors. If null, validation stops at the first error found.</param>
	/// <param name="validationAttributes">The set of validation attributes to apply to the value. Cannot be null.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the validation operation.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the value is valid;
	/// otherwise, <see langword="false"/>.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="validationContext"/> or <paramref name="validationAttributes"/> is null.</exception>
	public static async Task<bool> TryValidateValueAsync(object? value, ValidationContext validationContext,
		ICollection<ValidationResult>? validationResults, IEnumerable<ValidationAttribute> validationAttributes, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		if (validationAttributes is null)
			throw new ArgumentNullException(nameof(validationAttributes));

		bool result = true;
		bool breakOnFirstError = validationResults is null;

		var errors = await GetValidationErrorsAsync(value, validationContext, validationAttributes, breakOnFirstError, cancellationToken).ConfigureAwait(false);

		foreach (var err in errors)
		{
			result = false;
			validationResults?.Add(err.ValidationResult);
		}

		return result;
	}

	/// <summary>
	/// Asynchronously validates the specified property value using the validation attributes associated with the property
	/// in the provided validation context.
	/// </summary>
	/// <param name="value">The value of the property to validate. May be null if the property allows null values.</param>
	/// <param name="validationContext">The context that describes the property being validated, including metadata such as the object instance and
	/// property name. Cannot be null.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous validation operation. The default value is <see
	/// cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task completes when validation is finished. If
	/// validation fails, a validation exception is thrown.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="validationContext"/> is null.</exception>
	public static async Task ValidatePropertyAsync(object? value, ValidationContext validationContext, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		var propertyType = _store.GetPropertyType(validationContext);

		EnsureValidPropertyType(validationContext.MemberName!, propertyType, value);

		var attributes = _store.GetPropertyValidationAttributes(validationContext);

		var errors = await GetValidationErrorsAsync(value, validationContext, attributes, false, cancellationToken).ConfigureAwait(false);

		if (errors.Count > 0)
			errors[0].ThrowValidationException();
	}

	/// <summary>
	/// Asynchronously validates the specified object instance using the provided validation context.
	/// </summary>
	/// <param name="instance">The object to validate. Cannot be null.</param>
	/// <param name="validationContext">The context that describes the object to validate, including information such as service providers and items.
	/// Cannot be null.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous validation operation.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task completes when validation is finished. If
	/// validation fails, a ValidationException is thrown.</returns>
	public static Task ValidateObjectAsync(object instance, ValidationContext validationContext, CancellationToken cancellationToken = default)
		=> ValidateObjectAsync(instance, validationContext, false, cancellationToken);

	/// <summary>
	/// Asynchronously validates the specified object instance using the provided validation context and validation rules.
	/// </summary>
	/// <remarks>If validation fails, a <see cref="ValidationException"/> is thrown for the first validation error
	/// encountered. To retrieve all validation errors without throwing, use a method that returns the validation results
	/// instead.</remarks>
	/// <param name="instance">The object to validate. Must match the object specified in <paramref name="validationContext"/>.</param>
	/// <param name="validationContext">The context that describes the object to validate, including information such as service providers and display
	/// metadata. Cannot be null.</param>
	/// <param name="validateAllProperties">true to validate all properties of the object; otherwise, false to validate only properties marked for validation.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous validation operation.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task completes when validation is finished. If
	/// validation fails, a <see cref="ValidationException"/> is thrown.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> or <paramref name="validationContext"/> is null.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="instance"/> does not match <paramref name="validationContext"/>.ObjectInstance.</exception>
	public static async Task ValidateObjectAsync(object instance, ValidationContext validationContext,
		bool validateAllProperties, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (instance is null)
			throw new ArgumentNullException(nameof(instance));

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		if (instance != validationContext.ObjectInstance)
			throw new ArgumentException(SR.Validator_InstanceMustMatchValidationContextInstance, nameof(instance));

		var errors = await GetObjectValidationErrorsAsync(instance, validationContext, validateAllProperties, false, cancellationToken).ConfigureAwait(false);

		if (errors.Count > 0)
			errors[0].ThrowValidationException();
	}

	/// <summary>
	/// Asynchronously validates the specified value using the provided validation context and validation attributes.
	/// Throws a validation exception if the value does not satisfy any of the validation attributes.
	/// </summary>
	/// <param name="value">The value to validate. May be null if the validation attributes allow it.</param>
	/// <param name="validationContext">The context information about the object being validated. Cannot be null.</param>
	/// <param name="validationAttributes">A collection of validation attributes to apply to the value. Cannot be null.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task completes when validation is finished or a
	/// validation exception is thrown.</returns>
	/// <exception cref="ArgumentNullException">Thrown if validationContext or validationAttributes is null.</exception>
	public static async Task ValidateValueAsync(object? value, ValidationContext validationContext,
		IEnumerable<ValidationAttribute> validationAttributes, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		if (validationAttributes is null)
			throw new ArgumentNullException(nameof(validationAttributes));

		var errors = await GetValidationErrorsAsync(value, validationContext, validationAttributes, false, cancellationToken).ConfigureAwait(false);

		if (errors.Count > 0)
			errors[0].ThrowValidationException();
	}

	/// <summary>
	/// Asynchronously validates the specified object and returns a list of validation errors, if any are found.
	/// </summary>
	/// <remarks>Validation is performed in three stages: property-level validation, type-level validation
	/// attributes, and custom validation via IValidatableObject if implemented. The method respects the cancellation token
	/// and will throw an OperationCanceledException if cancellation is requested.</remarks>
	/// <param name="instance">The object to validate. This instance is checked against property and type-level validation attributes, as well as
	/// any custom validation logic implemented via IValidatableObject.</param>
	/// <param name="validationContext">The context information about the object to validate, including service providers and items used during validation.
	/// Cannot be null.</param>
	/// <param name="validateAllProperties">true to validate all properties of the object; otherwise, false to validate only required properties.</param>
	/// <param name="breakOnFirstError">true to stop validation and return immediately after the first error is found; otherwise, false to collect all
	/// validation errors.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous validation operation.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains a list of ValidationError objects
	/// describing any validation errors found. The list is empty if the object is valid.</returns>
	private static async Task<List<ValidationError>> GetObjectValidationErrorsAsync(object instance,
		ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var errors = await GetObjectPropertyValidationErrorsAsync(instance, validationContext, validateAllProperties, breakOnFirstError, cancellationToken).ConfigureAwait(false);

		if (errors.Count > 0)
			return errors;

		var attributes = _store.GetTypeValidationAttributes(validationContext);

		errors.AddRange(await GetValidationErrorsAsync(instance, validationContext, attributes, breakOnFirstError, cancellationToken).ConfigureAwait(false));

		if (errors.Count > 0)
			return errors;

		if (instance is IValidatableObject validatable)
		{
			var results = validatable.Validate(validationContext);

			if (results != null)
			{
				foreach (var result in results)
				{
					if (result != ValidationResult.Success)
					{
						errors.Add(new ValidationError(null, instance, result));
					}
				}
			}
		}

		return errors;
	}

	private static async Task<List<ValidationError>> GetObjectPropertyValidationErrorsAsync(object instance,
		ValidationContext validationContext, bool validateAllProperties, bool breakOnFirstError, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var properties = GetPropertyValues(instance, validationContext);
		var errors = new List<ValidationError>();

		foreach (var property in properties)
		{
			var attributes = _store.GetPropertyValidationAttributes(property.Key);

			if (validateAllProperties)
			{
				errors.AddRange(await GetValidationErrorsAsync(property.Value, property.Key, attributes, breakOnFirstError, cancellationToken).ConfigureAwait(false));
			}
			else
			{
				foreach (ValidationAttribute attribute in attributes)
				{
					if (attribute is RequiredAttribute reqAttr)
					{
						ValidationResult? validationResult = attribute is AsyncValidationAttribute asyncAttr
							? await asyncAttr.GetValidationResultAsync(property.Value, property.Key, cancellationToken).ConfigureAwait(false)
							: reqAttr.GetValidationResult(property.Value, property.Key);

						if (validationResult != ValidationResult.Success)
							errors.Add(new ValidationError(reqAttr, property.Value, validationResult!));

						break;
					}
				}
			}

			if (breakOnFirstError && errors.Count > 0)
				break;
		}

		return errors;
	}

	private static async Task<List<ValidationError>> GetValidationErrorsAsync(object? value,
		ValidationContext validationContext, IEnumerable<ValidationAttribute> attributes, bool breakOnFirstError, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		var errors = new List<ValidationError>();

		ValidationError? validationError;
		RequiredAttribute? required = null;

		foreach (var attribute in attributes)
		{
			required = attribute as RequiredAttribute;

			if (required is not null)
			{
				if (!await TryValidateAsync(value, validationContext, required, cancellationToken).ConfigureAwait(false))
				{
					validationError = new ValidationError(required, value, required.GetValidationResult(value, validationContext)!);

					errors.Add(validationError);

					return errors; // Required failure aborts
				}

				break;
			}
		}

		foreach (var attr in attributes)
		{
			if (attr != required)
			{
				if (!await TryValidateAsync(value, validationContext, attr, cancellationToken).ConfigureAwait(false))
				{
					var validationResult = attr is AsyncValidationAttribute asyncAttr
						? await asyncAttr.GetValidationResultAsync(value, validationContext, cancellationToken).ConfigureAwait(false)
						: attr.GetValidationResult(value, validationContext);

					validationError = new ValidationError(attr, value, validationResult!);

					errors.Add(validationError);

					if (breakOnFirstError)
						break;
				}
			}
		}

		return errors;
	}

	private static async Task<bool> TryValidateAsync(object? value, ValidationContext validationContext, ValidationAttribute attribute, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Debug.Assert(validationContext != null);

		ValidationResult? validationResult = attribute is AsyncValidationAttribute asyncAttribute
			? await asyncAttribute.GetValidationResultAsync(value, validationContext!, cancellationToken).ConfigureAwait(false)
			: attribute.GetValidationResult(value, validationContext!);

		return validationResult == ValidationResult.Success;
	}

	private static ValidationContext CreateValidationContext(object instance, ValidationContext validationContext)
	{
		if (validationContext is null)
			throw new ArgumentNullException(nameof(validationContext));

		return new ValidationContext(instance, validationContext, validationContext.Items);
	}

	private static bool CanBeAssigned(Type destinationType, object? value)
	{
		if (value is null)
			return !destinationType.IsValueType || (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(Nullable<>));

		return destinationType.IsInstanceOfType(value);
	}

	private static void EnsureValidPropertyType(string propertyName, Type propertyType, object? value)
	{
		if (!CanBeAssigned(propertyType, value))
			throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, SR.Validator_Property_Value_Wrong_Type, propertyName, propertyType), nameof(value));
	}

	private static List<KeyValuePair<ValidationContext, object?>> GetPropertyValues(object instance, ValidationContext validationContext)
	{
		var properties = TypeDescriptor.GetProperties(instance.GetType());
		var items = new List<KeyValuePair<ValidationContext, object?>>(properties.Count);

		foreach (PropertyDescriptor property in properties)
		{
			var context = CreateValidationContext(instance, validationContext);
			context.MemberName = property.Name;

			if (_store.GetPropertyValidationAttributes(context).Any())
				items.Add(new KeyValuePair<ValidationContext, object?>(context, property.GetValue(instance)));
		}

		return items;
	}

	private sealed class ValidationError
	{
		private readonly object? _value;
		private readonly ValidationAttribute? _validationAttribute;

		internal ValidationError(ValidationAttribute? validationAttribute, object? value, ValidationResult validationResult)
		{
			_validationAttribute = validationAttribute;
			ValidationResult = validationResult;
			_value = value;
		}

		internal ValidationResult ValidationResult { get; }

		[DoesNotReturn]
		internal void ThrowValidationException() => throw new ValidationException(ValidationResult, _validationAttribute, _value);
	}
}