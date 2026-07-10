using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc;

/// <summary>
/// Serves as the base class for API controllers that expose custom-shaped endpoints backed by a data service.
/// This is the data-service counterpart of <see cref="UmbrellaDataAccessApiController"/>: where that controller
/// provides protected helpers over the core data access service for repository-backed custom endpoints, this
/// controller provides the standard endpoint execution envelope over an injected <typeparamref name="TDataService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Derive from this controller when the endpoint surface does not fit
/// <see cref="UmbrellaGenericRepositoryDataServiceApiController{TSlimItem, TPaginatedResultModel, TItem, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult, TEntityKey, TRepositoryDataService}"/>,
/// e.g. singleton resources, partial CRUD, or services exposing custom operations. The
/// <typeparamref name="TDataService"/> type is deliberately unconstrained so that services implementing only a
/// subset of <c>IGenericDataService</c>, or fully custom service interfaces, can be used.
/// </para>
/// <para>
/// Endpoints should be written as expression-bodied members that pass an expression lambda returning the service's
/// <see cref="Task{TResult}"/> directly to <see cref="ExecuteOperationAsync{TResult}"/> (or the non-generic overload for
/// operations returning a plain <see cref="IOperationResult"/>). Avoid <see langword="async"/> lambdas — they can bind
/// to the wrong overload and lose the typed response body.
/// </para>
/// <para>
/// The execution envelope mirrors the generic data service controller endpoints: the cancellation token is checked,
/// the operation is invoked against the lazily-resolved service, the resulting <see cref="IOperationResult"/> is mapped
/// to the appropriate HTTP response, and unhandled exceptions are logged and converted to a <c>500</c> response outside
/// the Development environment.
/// </para>
/// </remarks>
/// <typeparam name="TDataService">The type of the data service used to perform the operations.</typeparam>
/// <seealso cref="UmbrellaApiController" />
public abstract class UmbrellaDataServiceApiController<TDataService> : UmbrellaApiController
{
	/// <summary>
	/// Gets the lazy-initialized data service instance.
	/// </summary>
	/// <remarks>The data service is created only when first accessed. Use this property to access
	/// service operations without incurring the cost of initialization until needed.</remarks>
	protected Lazy<TDataService> DataService { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataServiceApiController{TDataService}"/> class.
	/// </summary>
	/// <param name="logger">The logger used to record diagnostic and operational information.</param>
	/// <param name="hostingEnvironment">The web hosting environment in which the application is running.</param>
	/// <param name="dataService">A lazily initialized data service that provides the operations for the controller.</param>
	protected UmbrellaDataServiceApiController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment,
		Lazy<TDataService> dataService)
		: base(logger, hostingEnvironment)
	{
		DataService = dataService;
	}

	/// <summary>
	/// Executes the specified data service <paramref name="operation"/> using the standard endpoint execution envelope
	/// and maps the resulting <see cref="IOperationResult{TResult}"/> to the appropriate HTTP response.
	/// </summary>
	/// <typeparam name="TResult">The type of the result the operation produces.</typeparam>
	/// <param name="operation">The operation to invoke against the data service. This should be an expression lambda returning the service's <see cref="Task{TResult}"/> directly.</param>
	/// <param name="errorMessage">The error message returned in the <c>500</c> response when the operation throws an unhandled exception.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="logState">The optional anonymous object containing the endpoint's significant input parameters, recorded when an unhandled exception is logged.</param>
	/// <param name="memberName">The compiler-provided name of the calling endpoint method. Do not specify a value.</param>
	/// <param name="filePath">The compiler-provided file path of the calling endpoint method. Do not specify a value.</param>
	/// <param name="lineNumber">The compiler-provided line number of the call site. Do not specify a value.</param>
	/// <returns>
	/// The action result containing the mapped operation result when the operation completes, or a <c>500</c>
	/// <see cref="ProblemDetails"/> response containing <paramref name="errorMessage"/> when it throws outside
	/// the Development environment.
	/// </returns>
	protected async Task<IActionResult> ExecuteOperationAsync<TResult>(
		Func<TDataService, CancellationToken, Task<IOperationResult<TResult?>>> operation,
		string errorMessage,
		CancellationToken cancellationToken,
		object? logState = null,
		[CallerMemberName] string memberName = "",
		[CallerFilePath] string filePath = "",
		[CallerLineNumber] int lineNumber = 0)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(operation);

		try
		{
			IOperationResult<TResult?> result = await operation(DataService.Value, cancellationToken).ConfigureAwait(false);

			return OperationResult<TResult>(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, logState, returnValue: !IsDevelopment, methodName: memberName, filePath: filePath, lineNumber: lineNumber))
		{
			return InternalServerError(errorMessage);
		}
	}

	/// <summary>
	/// Executes the specified data service <paramref name="operation"/> using the standard endpoint execution envelope
	/// and maps the resulting <see cref="IOperationResult"/> to the appropriate HTTP response.
	/// </summary>
	/// <param name="operation">The operation to invoke against the data service. This should be an expression lambda returning the service's <see cref="Task{TResult}"/> directly.</param>
	/// <param name="errorMessage">The error message returned in the <c>500</c> response when the operation throws an unhandled exception.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="logState">The optional anonymous object containing the endpoint's significant input parameters, recorded when an unhandled exception is logged.</param>
	/// <param name="memberName">The compiler-provided name of the calling endpoint method. Do not specify a value.</param>
	/// <param name="filePath">The compiler-provided file path of the calling endpoint method. Do not specify a value.</param>
	/// <param name="lineNumber">The compiler-provided line number of the call site. Do not specify a value.</param>
	/// <returns>
	/// The action result containing the mapped operation result when the operation completes, or a <c>500</c>
	/// <see cref="ProblemDetails"/> response containing <paramref name="errorMessage"/> when it throws outside
	/// the Development environment.
	/// </returns>
	protected async Task<IActionResult> ExecuteOperationAsync(
		Func<TDataService, CancellationToken, Task<IOperationResult>> operation,
		string errorMessage,
		CancellationToken cancellationToken,
		object? logState = null,
		[CallerMemberName] string memberName = "",
		[CallerFilePath] string filePath = "",
		[CallerLineNumber] int lineNumber = 0)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(operation);

		try
		{
			IOperationResult result = await operation(DataService.Value, cancellationToken).ConfigureAwait(false);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, logState, returnValue: !IsDevelopment, methodName: memberName, filePath: filePath, lineNumber: lineNumber))
		{
			return InternalServerError(errorMessage);
		}
	}
}
