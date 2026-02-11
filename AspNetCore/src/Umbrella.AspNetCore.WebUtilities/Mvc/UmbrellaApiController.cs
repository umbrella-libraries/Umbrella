
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Shared.Constants;
using Umbrella.AppFramework.Shared.Security;
using Umbrella.AppFramework.Shared.Security.Extensions;
using Umbrella.AspNetCore.WebUtilities.Extensions;
using Umbrella.Utilities.Http.Constants;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc;

/// <summary>
/// Serves as the base class for API controllers and encapsulates API specific functionality.
/// </summary>
[ApiController]
public abstract class UmbrellaApiController : ControllerBase
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the hosting environment.
	/// </summary>
	protected IWebHostEnvironment HostingEnvironment { get; }

	/// <summary>
	/// Gets a value indicating whether the application is running in development mode.
	/// </summary>
	protected bool IsDevelopment => HostingEnvironment.IsDevelopment();

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaApiController"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	protected UmbrellaApiController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment)
	{
		Logger = logger;
		HostingEnvironment = hostingEnvironment;

	}

	/// <summary>
	/// Appends the a file access token as a query string parameter with the key <see cref="AppQueryStringKeys.FileAccessToken" /> using the value stored
	/// using the <see cref="UmbrellaAppClaimType.FileAccessToken "/> claim, if it exists on the current <see cref="ClaimsPrincipal"/>.
	/// </summary>
	/// <param name="uri">The URI to append the token to.</param>
	/// <returns>The URI with the appended token if the <see cref="UmbrellaAppClaimType.FileAccessToken" /> claim exists; otherwise the value of the <paramref name="uri"/> parameter.</returns>
	protected string AppendFileAccessToken(string uri)
	{
		string? accessToken = User.GetFileAccessToken();

		return string.IsNullOrWhiteSpace(accessToken) ? uri : QueryHelpers.AddQueryString(uri, AppQueryStringKeys.FileAccessToken, accessToken);
	}

	/// <summary>
	/// Creates an <see cref="IActionResult"/> based on the specified <see cref="IOperationResult"/> instance.
	/// </summary>
	/// <param name="operationResult">The operation result.</param>
	/// <returns>The <see cref="IActionResult"/> based on the specified <see cref="IOperationResult"/>.</returns>
	/// <exception cref="NotImplementedException"></exception>
	protected IActionResult OperationResult(IOperationResult operationResult) => operationResult?.Status switch
	{
		OperationResultStatus.GenericSuccess => Ok(),
		OperationResultStatus.GenericFailure => InternalServerError(operationResult.PrimaryValidationMessage ?? "There has been a problem."),
		OperationResultStatus.NotFound => NotFound(operationResult.PrimaryValidationMessage ?? "Not Found"),
		OperationResultStatus.Conflict => Conflict(operationResult.PrimaryValidationMessage ?? "Conflict"),
		OperationResultStatus.Forbidden => Forbidden(operationResult.PrimaryValidationMessage ?? "Forbidden"),
		OperationResultStatus.NoContent => NoContent(),
		OperationResultStatus.NotAllowed => MethodNotAllowed(operationResult.PrimaryValidationMessage ?? "Method Not Allowed"),
		OperationResultStatus.InvalidOperation when operationResult.ValidationResults is { Count: > 0 } => ValidationProblem(operationResult.ValidationResults.ToModelStateDictionary()),
		OperationResultStatus.InvalidOperation => BadRequest(operationResult.PrimaryValidationMessage ?? "Invalid Operation"),
		OperationResultStatus.Created => Created(),
		_ => throw new SwitchExpressionException(operationResult?.Status)
	};

	/// <summary>
	/// Creates an <see cref="IActionResult"/> based on the specified <see cref="IOperationResult"/>.
	/// </summary>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <param name="operationResult">The operation result.</param>
	/// <returns>An <see cref="IActionResult"/> based on the specified <see cref="IOperationResult"/>.</returns>
	protected IActionResult OperationResult<TModel>(IOperationResult operationResult)
	{
		Guard.IsNotNull(operationResult);

		return (operationResult, operationResult.Status) switch
		{
			(IOperationResult<TModel> typedResult, OperationResultStatus.GenericSuccess) => Ok(typedResult.Result),
			(IOperationResult<TModel> typedResult, OperationResultStatus.Created) => typedResult.Result is not null ? Created(typedResult.Result) : Created(),
			_ => OperationResult(operationResult),
		};
	}

	/// <summary>
	/// Creates a failure result based on the specified <see cref="OperationResultException"/>.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <returns>The action result.</returns>
	/// <exception cref="SwitchExpressionException"></exception>
	protected IActionResult OperationResultFailure(OperationResultException exception)
	{
		Guard.IsNotNull(exception);

		switch (exception.Status)
		{
			case OperationResultStatus.GenericFailure when exception.ValidationResults is not { Count: > 0 }:
				_ = Logger.WriteError(state: new { exception.Status }, message: exception.Message);
				return BadRequest(exception.Message);
			case OperationResultStatus.NotFound when exception.ValidationResults is not { Count: > 0 }:
				_ = Logger.WriteWarning(state: new { exception.Status }, message: exception.Message);
				return NotFound(exception.Message);
			case OperationResultStatus.Conflict when exception.ValidationResults is not { Count: > 0 }:
				_ = Logger.WriteError(state: new { exception.Status }, message: exception.Message);
				return Conflict(exception.Message);
			case OperationResultStatus.GenericFailure when exception.ValidationResults is { Count: > 0 }:
				_ = Logger.WriteError(state: new { exception.Status }, message: exception.PrimaryValidationMessage);
				return ValidationProblem(exception.ValidationResults.ToModelStateDictionary());
			case OperationResultStatus.NotFound when exception.ValidationResults is { Count: > 0 }:
				_ = Logger.WriteWarning(state: new { exception.Status }, message: exception.PrimaryValidationMessage);
				return NotFound(exception.PrimaryValidationMessage ?? "Not Found");
			case OperationResultStatus.Conflict when exception.ValidationResults is { Count: > 0 }:
				_ = Logger.WriteError(state: new { exception.Status }, message: exception.PrimaryValidationMessage);
				return Conflict(exception.PrimaryValidationMessage ?? "Conflict");
			default:
				throw new SwitchExpressionException(exception.Status);
		}
	}

	/// <summary>
	/// Creates a 201 Created <see cref="StatusCodeResult"/>.
	/// </summary>
	/// <returns>A <see cref="StatusCodeResult"/> of 201.</returns>
	protected virtual new StatusCodeResult Created() => StatusCode(201);

	/// <summary>
	/// Creates a 201 Created <see cref="CreatedResult"/> with the specified content.
	/// </summary>
	/// <param name="content">The content.</param>
	/// <returns>A <see cref="CreatedResult"/> of 201.</returns>
	protected virtual CreatedResult Created(object content) => Created("", content);

	/// <summary>
	/// Creates a 400 BadRequest <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 400.</returns>
	protected virtual ObjectResult BadRequest(string reason, string? code = null) => UmbrellaValidationProblem(reason, statusCode: 400, title: "BadRequest", code: code);

	/// <summary>
	/// Creates a 401 Unauthorized <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 401.</returns>
	protected virtual ObjectResult Unauthorized(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 401, title: "Unauthorized", code: code);

	/// <summary>
	/// Creates a 403 Forbidden <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 403.</returns>
	protected virtual ObjectResult Forbidden(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 403, title: "Forbidden", code: code);

	/// <summary>
	/// Creates a 404 NotFound <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 404.</returns>
	protected virtual ObjectResult NotFound(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 404, title: "Not Found", code: code);

	/// <summary>
	/// Creates a 405 MethodNotAllowed <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 405.</returns>
	protected virtual ObjectResult MethodNotAllowed(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 405, title: "Method Not Allowed", code: code);

	/// <summary>
	/// Creates a 409 Conflict <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 409.</returns>
	protected virtual ObjectResult Conflict(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 409, title: "Conflict", code: code);

	/// <summary>
	/// Creates a 409 Conflict <see cref="ObjectResult"/> with the specified reason with the code set to <see cref="HttpProblemCodes.ConcurrencyStampMismatch"/>.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <returns>A <see cref="ObjectResult"/> of 409.</returns>
	protected virtual ObjectResult ConcurrencyConflict(string reason) => UmbrellaProblem(reason, statusCode: 409, title: "Concurrency Conflict", code: HttpProblemCodes.ConcurrencyStampMismatch);

	/// <summary>
	/// Creates a 429 TooManyRequests <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 429.</returns>
	protected virtual ObjectResult TooManyRequests(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 429, title: "Too Many Requests", code: code);

	/// <summary>
	/// Creates a 500 InternalServerError <see cref="ObjectResult"/> with the specified reason.
	/// </summary>
	/// <param name="reason">The reason.</param>
	/// <param name="code">The error code.</param>
	/// <returns>A <see cref="ObjectResult"/> of 500.</returns>
	protected virtual ObjectResult InternalServerError(string reason, string? code = null) => UmbrellaProblem(reason, statusCode: 500, title: "Error", code: code);

	/// <summary>
	/// Creates an <see cref="ObjectResult"/> containing an <see cref="UmbrellaProblemDetails"/> instance.
	/// </summary>
	/// <param name="detail">The detail.</param>
	/// <param name="instance">The instance.</param>
	/// <param name="statusCode">The status code.</param>
	/// <param name="title">The title.</param>
	/// <param name="type">The type.</param>
	/// <param name="code">The error code.</param>
	/// <returns>The <see cref="ObjectResult"/> containing an instance of <see cref="UmbrellaProblemDetails"/>.</returns>
	protected virtual ObjectResult UmbrellaProblem(string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, string? code = null)
	{
		var problemDetails = new UmbrellaProblemDetails
		{
			Code = code,
			Detail = detail,
			Instance = instance,
			Status = statusCode,
			Title = title,
			Type = type,
			CorrelationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
		};

		return new ObjectResult(problemDetails)
		{
			ContentTypes = { "application/problem+json" },
			StatusCode = statusCode
		};
	}

	/// <summary>
	/// Creates an ObjectResult that produces a validation problem response using the UmbrellaValidationProblemDetails
	/// format.
	/// </summary>
	/// <remarks>The returned response conforms to the RFC 7807 problem details specification, with additional
	/// fields for application-specific error codes and validation errors. The 'TraceId' property is set to the current
	/// activity ID or the HTTP context trace identifier to assist with request tracing.</remarks>
	/// <param name="detail">A detailed description of the validation problem. This value is included in the response's 'detail' field. Can be
	/// null.</param>
	/// <param name="instance">A URI reference that identifies the specific occurrence of the problem. This value is included in the response's
	/// 'instance' field. Can be null.</param>
	/// <param name="statusCode">The HTTP status code to set for the response. If null, a default status code may be used.</param>
	/// <param name="title">A short, human-readable summary of the problem type. This value is included in the response's 'title' field. Can be
	/// null.</param>
	/// <param name="type">A URI reference that identifies the problem type. This value is included in the response's 'type' field. Can be
	/// null.</param>
	/// <param name="code">An application-specific error code that provides additional information about the validation problem. Can be null.</param>
	/// <param name="errors">A dictionary containing validation errors, where each key is the name of a field and the value is an array of error
	/// messages for that field. If null, an empty dictionary is used.</param>
	/// <returns>An ObjectResult containing an UmbrellaValidationProblemDetails object with the specified details, formatted as
	/// 'application/problem+json'.</returns>
	protected virtual ObjectResult UmbrellaValidationProblem(string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, string? code = null, Dictionary<string, string[]>? errors = null)
	{
		var problemDetails = new UmbrellaValidationProblemDetails
		{
			Code = code,
			Detail = detail,
			Errors = errors ?? [],
			Instance = instance,
			Status = statusCode,
			Title = title,
			Type = type,
			TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
		};

		return new ObjectResult(problemDetails)
		{
			ContentTypes = { "application/problem+json" },
			StatusCode = statusCode
		};
	}
}