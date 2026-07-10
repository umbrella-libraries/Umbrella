using System.ComponentModel.DataAnnotations;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Primitives;

namespace Umbrella.Utilities.Http;

/// <summary>
/// Represents the result of a Http operation.
/// </summary>
public record HttpOperationResult : OperationResult, IHttpOperationResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOperationResult"/> class with a <see cref="OperationResultStatus.GenericSuccess"/> status.
	/// </summary>
	public HttpOperationResult()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOperationResult{TResult}"/> class with a default <see cref="OperationResultStatus.GenericFailure"/> status.
	/// </summary>
	/// <param name="httpProblemDetails">The <see cref="HttpProblemDetails"/>.</param>
	/// <param name="status">The status of the operation.</param>
	public HttpOperationResult(HttpProblemDetails? httpProblemDetails, OperationResultStatus status = OperationResultStatus.GenericFailure)
	{
		ProblemDetails = httpProblemDetails;
		Status = status;
		ValidationResults = httpProblemDetails?.ToValidationResults();
	}

	/// <inheritdoc />
	public HttpProblemDetails? ProblemDetails { get; }

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult"/> with a <see cref="OperationResult.Status"/> of <see cref="OperationResultStatus.GenericSuccess"/>.
	/// </summary>
	/// <returns>The <see cref="IHttpOperationResult"/> instance.</returns>
	public static new IHttpOperationResult Success() => new HttpOperationResult();

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult"/> with a <see cref="OperationResult.Status"/> of
	/// <see cref="OperationResultStatus.Created"/>.
	/// </summary>
	/// <returns>The <see cref="IHttpOperationResult"/> instance.</returns>
	public static IHttpOperationResult Created() => new HttpOperationResult() { Status = OperationResultStatus.Created };

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult"/> with a <see cref="OperationResult.Status"/> of
	/// <see cref="OperationResultStatus.NoContent"/>.
	/// </summary>
	/// <returns>The <see cref="IHttpOperationResult"/> instance.</returns>
	public static new IHttpOperationResult NoContent() => new HttpOperationResult() { Status = OperationResultStatus.NoContent };
}

/// <summary>
/// Represents the result of a Http operation with a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
public record HttpOperationResult<TResult> : OperationResult<TResult>, IHttpOperationResult<TResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOperationResult{TResult}"/> class with a default <see cref="OperationResultStatus.GenericFailure"/> status.
	/// </summary>
	/// <param name="httpProblemDetails">The <see cref="HttpProblemDetails"/>.</param>
	/// <param name="status">The status of the operation.</param>
	public HttpOperationResult(HttpProblemDetails? httpProblemDetails, OperationResultStatus status = OperationResultStatus.GenericFailure)
	{
		ProblemDetails = httpProblemDetails;
		Status = status;
		ValidationResults = httpProblemDetails?.ToValidationResults();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOperationResult{TResult}"/> class with a <see cref="OperationResultStatus.GenericSuccess"/> status and a result.
	/// </summary>
	/// <param name="result">The result of the operation.</param>
	public HttpOperationResult(TResult? result)
	{
		Result = result;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HttpOperationResult{TResult}"/> class with a <see cref="OperationResultStatus.GenericFailure"/> status and a collection of validation results.
	/// </summary>
	/// <param name="validationResults">The collection of validation results.</param>
	public HttpOperationResult(IEnumerable<ValidationResult> validationResults)
	{
		Status = OperationResultStatus.GenericFailure;
		ValidationResults = validationResults.ToArray();
	}

	/// <inheritdoc />
	public HttpProblemDetails? ProblemDetails { get; }

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult{TResult}"/> with a <see cref="OperationResult.Status"/> of
	/// <see cref="OperationResultStatus.GenericSuccess"/> and a result.
	/// </summary>
	/// <param name="result">The result of the operation.</param>
	/// <returns>The <see cref="IHttpOperationResult{TResult}"/> instance.</returns>
	public static new IHttpOperationResult<TResult> Success(TResult? result) => new HttpOperationResult<TResult>(result);

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult{TResult}"/> with a <see cref="OperationResult.Status"/> of
	/// <see cref="OperationResultStatus.Created"/>.
	/// </summary>
	/// <returns>The <see cref="IHttpOperationResult{TResult}"/> instance.</returns>
	public static IHttpOperationResult<TResult> Created() => new HttpOperationResult<TResult>(default(TResult)) { Status = OperationResultStatus.Created };

	/// <summary>
	/// Creates an <see cref="IHttpOperationResult{TResult}"/> with a <see cref="OperationResult.Status"/> of
	/// <see cref="OperationResultStatus.NoContent"/>.
	/// </summary>
	/// <returns>The <see cref="IHttpOperationResult{TResult}"/> instance.</returns>
	public static new IHttpOperationResult<TResult> NoContent() => new HttpOperationResult<TResult>(default(TResult)) { Status = OperationResultStatus.NoContent };
}