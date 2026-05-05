using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc;

/// <summary>
/// Provides a generic base API controller for performing CRUD operations on repository-backed entities using a data
/// service abstraction. This controller supports paginated queries, retrieval, creation, updating, and deletion of
/// entities, with all operations routed through a repository data service.
/// </summary>
/// <remarks>This controller is intended to be used as a base class for API controllers that expose
/// repository-backed data services. It provides standard endpoints for searching, retrieving, creating, updating, and
/// deleting entities, delegating all data access logic to the injected repository data service. The controller is
/// designed for extensibility and can be customized by overriding its virtual methods. All endpoints are asynchronous
/// and support cancellation via <see cref="CancellationToken"/>.</remarks>
/// <typeparam name="TItem">The type representing the full entity model, which must implement <see cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TSlimItem">The type representing a lightweight or summary view of the entity, used for paginated or list results. Must
/// implement <see cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type representing a paginated result containing a collection of <typeparamref name="TSlimItem"/> items. Must
/// inherit from <see cref="PaginatedResultModel{TSlimItem}"/>.</typeparam>
/// <typeparam name="TCreateItem">The type used to represent the data required to create a new entity.</typeparam>
/// <typeparam name="TCreateResult">The type representing the result returned after a successful create operation.</typeparam>
/// <typeparam name="TUpdateItem">The type used to represent the data required to update an existing entity. Must implement <see
/// cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TUpdateResult">The type representing the result returned after a successful update operation.</typeparam>
/// <typeparam name="TEntityKey">The type of the key used to uniquely identify entities. Must implement <see cref="IEquatable{TEntityKey}"/>.</typeparam>
/// <typeparam name="TRepositoryDataService">The type of the repository data service used to perform data operations. Must implement <see
/// cref="IUmbrellaRepositoryDataService{TItem, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult,
/// TUpdateItem, TUpdateResult, TRepository, TEntity, TRepositoryOptions, TEntityKey}"/>.</typeparam>
[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
[UmbrellaProducesResponseType(StatusCodes.Status500InternalServerError)]
public abstract class UmbrellaGenericRepositoryDataServiceApiController<TSlimItem, TPaginatedResultModel, TItem, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult, TEntityKey, TRepositoryDataService> : UmbrellaApiController
	where TSlimItem : class, IKeyedItem<TEntityKey>
	where TPaginatedResultModel : PaginatedResultModel<TSlimItem>
	where TItem : class, IKeyedItem<TEntityKey>
	where TUpdateItem : class, IKeyedItem<TEntityKey>
	where TEntityKey : IEquatable<TEntityKey>
	where TRepositoryDataService : IGenericDataService<TItem, TEntityKey, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaGenericRepositoryDataServiceApiController{TItem, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult, TEntityKey, TRepositoryDataService}"/> class with the specified
	/// dependencies required for data service operations and API controller functionality.
	/// </summary>
	/// <param name="logger">The logger used to record diagnostic and operational information.</param>
	/// <param name="hostingEnvironment">The web hosting environment in which the application is running.</param>
	/// <param name="repositoryDataService">A lazily initialized repository data service that provides data access operations for the controller.</param>
	protected UmbrellaGenericRepositoryDataServiceApiController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment,
		Lazy<TRepositoryDataService> repositoryDataService)
		: base(logger, hostingEnvironment)
	{
		RepositoryDataService = repositoryDataService;
	}

	/// <summary>
	/// Gets the lazy-initialized repository data service instance.
	/// </summary>
	/// <remarks>The repository data service is created only when first accessed. Use this property to access
	/// repository-related operations without incurring the cost of initialization until needed.</remarks>
	protected Lazy<TRepositoryDataService> RepositoryDataService { get; }

	/// <summary>
	/// An API endpoint used to load paginated entities in bulk from the repository based on the specified <paramref name="sorters"/> and <paramref name="filters"/>
	/// with each result mapped to a collection of <typeparamref name="TSlimItem"/> wrapped in a <typeparamref name="TPaginatedResultModel"/>.
	/// </summary>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TPaginatedResultModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c>ReadAllAsync</c> base controller method.
	/// Please see this for further details regarding behaviour.
	/// </remarks>
	[HttpGet("SearchSlim")]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	public virtual async Task<IActionResult> SearchSlimAsync(int pageNumber, int pageSize, [FromQuery] IEnumerable<SortExpressionDescriptor>? sorters = null, [FromQuery] IEnumerable<FilterExpressionDescriptor>? filters = null, FilterExpressionCombinator? filterCombinator = null, CancellationToken cancellationToken = default)
	{
		try
		{
			IOperationResult<TPaginatedResultModel?> result = await RepositoryDataService.Value.FindAllSlimAsync(pageNumber, pageSize, sorters, filters, filterCombinator ?? FilterExpressionCombinator.And, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { pageNumber, pageSize, sorters, filters, filterCombinator }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to load the requested resources.");
		}
	}

	/// <summary>
	/// An API endpoint used to load a single entity from the repository based on the specified <paramref name="id"/> and
	/// return a mapped <typeparamref name="TItem"/>.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TItem"/> when successful
	/// or a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c> ReadAsync</c> base controller method. Please see this for further details
	/// regarding behaviour.
	/// </remarks>
	[HttpGet]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	public virtual async Task<IActionResult> GetAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.FindByIdAsync(id, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to load the requested resource.");
		}
	}

	/// <summary>
	/// An API endpoint used to create a new entity in the repository based on the provided <typeparamref name="TCreateItem"/> which returns
	/// a <typeparamref name="TCreateResult"/> if successful.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TCreateResult"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c>CreateAsync</c> base controller method.
	/// Please see this for further details regarding behaviour.
	/// </remarks>
	/// <seealso cref="UmbrellaDataAccessApiController.CreateAsync"/>
	[HttpPost]
	[UmbrellaProducesResponseType(StatusCodes.Status201Created)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	[UmbrellaProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public virtual async Task<IActionResult> PostAsync(TCreateItem model, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.CreateAsync(model, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { model }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to create the requested resource.");
		}
	}

	/// <summary>
	/// An API endpoint used to update an existing entity in the repository based on the provided <typeparamref name="TUpdateItem"/> which returns
	/// a <typeparamref name="TUpdateResult"/> if successful.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TUpdateResult"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c>UpdateAsync</c> base controller method.
	/// Please see this for further details regarding behaviour.
	/// </remarks>
	/// <seealso cref="UmbrellaDataAccessApiController.UpdateAsync"/>
	[HttpPut]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	[UmbrellaProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public virtual async Task<IActionResult> PutAsync(TUpdateItem model, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.UpdateAsync(model, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { model }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to update the requested resource.");
		}
	}

	/// <summary>
	/// An API endpoint used to delete a single entity in the repository based on the specified <paramref name="id"/>.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <c>204</c> status code when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c>DeleteAsync</c> base controller method.
	/// Please see this for further details regarding behaviour.
	/// </remarks>
	/// <seealso cref="UmbrellaDataAccessApiController.DeleteAsync"/>
	[HttpDelete]
	[UmbrellaProducesResponseType(StatusCodes.Status204NoContent)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	public virtual async Task<IActionResult> DeleteAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.DeleteAsync(id, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to delete the requested resource.");
		}
	}

	/// <summary>
	/// Checks whether an entity with the specified identifier exists.
	/// </summary>
	/// <param name="id">The unique identifier of the entity to check for existence.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
	/// <returns>An <see cref="IActionResult"/> containing a boolean value indicating whether the entity exists. The result is <see langword="true"/> if the entity exists; otherwise, <see langword="false"/>.</returns>
	[HttpGet("ExistsById")]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	public virtual async Task<IActionResult> ExistsByIdAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.ExistsByIdAsync(id, cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to check for the existence of the requested resource.");
		}
	}

	/// <summary>
	/// Handles an HTTP GET request to retrieve the total count of resources asynchronously.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
	/// <returns>An <see cref="IActionResult"/> containing the total count of resources if successful; otherwise, an error response.</returns>
	[HttpGet("TotalCount")]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
	public virtual async Task<IActionResult> TotalCountAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var result = await RepositoryDataService.Value.FindTotalCountAsync(cancellationToken);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error occurred while attempting to retrieve the total count of resources.");
		}
	}
}