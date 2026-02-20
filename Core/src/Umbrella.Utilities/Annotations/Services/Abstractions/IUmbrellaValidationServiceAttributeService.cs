using Umbrella.Utilities.Primitives;

namespace Umbrella.Utilities.Annotations.Services.Abstractions;

/// <summary>
/// A service interface for validating attributes that implement the <see cref="UmbrellaValidationServiceAttribute{T}"/>
/// base class. This service is responsible for executing the validation logic defined in the attribute and returning
/// the result of the validation operation.
/// </summary>
public interface IUmbrellaValidationServiceAttributeService
{
	/// <summary>
	/// Asynchronously validates the specified value against the provided object instance and returns the result of the
	/// validation operation.
	/// </summary>
	/// <remarks>The validation logic may vary depending on the type and state of the provided object instance. This
	/// method is intended for scenarios where validation rules are associated with specific object instances and may
	/// involve asynchronous operations such as database or service calls.</remarks>
	/// <param name="objectInstance">The object instance that provides the context for validation. This instance may contain validation rules or
	/// metadata that influence the validation process. Cannot be null.</param>
	/// <param name="value">The value to validate. This value is checked for compliance with the validation rules defined by the object
	/// instance.</param>
	/// <param name="errorMessage">An optional error message to include if validation fails. If null, a default error message may be used.</param>
	/// <returns>A task that represents the asynchronous validation operation. The task result contains an <see
	/// cref="OperationResult"/> indicating whether the value is valid and, if not, includes details about the validation
	/// failure.</returns>
	Task<OperationResult> IsValidAsync(object objectInstance, object? value, string? errorMessage);
}