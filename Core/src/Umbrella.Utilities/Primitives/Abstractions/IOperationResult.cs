
using System.ComponentModel.DataAnnotations;

namespace Umbrella.Utilities.Primitives.Abstractions;

/// <summary>
/// Encapsulates the result of an operation.
/// </summary>
public interface IOperationResult
{
	/// <summary>
	/// Determines whether the operation was successful.
	/// </summary>
	bool IsSuccess { get; }

	/// <summary>
	/// Gets the primary validation message which is the first message in the <see cref="ValidationResults"/> collection.
	/// </summary>
	string? PrimaryValidationMessage { get; }

	/// <summary>
	/// Gets the status of the operation.
	/// </summary>
	/// <remarks>Defaults to <see cref="OperationResultStatus.GenericSuccess"/></remarks>
	OperationResultStatus Status { get; }

	/// <summary>
	/// A list of validation results that contain messages detailing why it might be the case that the operation could not be completed.
	/// </summary>
	IReadOnlyCollection<ValidationResult>? ValidationResults { get; }

	/// <summary>
	/// Converts the current operation result to a strongly typed result of the specified reference type.
	/// </summary>
	/// <typeparam name="TResult">The reference type to which the operation result is cast. Must be a class.</typeparam>
	/// <returns>An <see cref="IOperationResult{TResult}"/> representing the result of the operation with the specified type parameter.</returns>
	OperationResult<TResult> ToTypedOperationResult<TResult>();
}

/// <summary>
/// Encapsulates the result of an operation with a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IOperationResult<out TResult> : IOperationResult
{
	/// <summary>
	/// The item that the associated operation was being performed on.
	/// </summary>
	TResult? Result { get; }
}