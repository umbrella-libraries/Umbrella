
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.Mvc;

/// <summary>
/// A generic API Controller that can be used to perform CRUD operations on entities that interact with types that implement <see cref="IGenericDbRepository{TEntity, TRepoOptions, TEntityKey}"/>.
/// </summary>
/// <remarks>
/// This controller extends the <see cref="UmbrellaDataAccessApiController" /> by exposing public API endpoints which call into the base methods
/// that do the processing using the generic type parameters specified on this controller type. It also simplifies usage by using commonly used default values
/// for method parameters. <see cref="UmbrellaDataAccessApiController"/> can be used instead of this controller and provides more flexibility, however, this comes at the
/// expense of simplicity and maintainability of controller that extend that base controller instead of this one.
/// </remarks>
/// <typeparam name="TSlimModel">The type of the slim model.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TCreateModel">The type of the create model.</typeparam>
/// <typeparam name="TCreateResultModel">The type of the create result model.</typeparam>
/// <typeparam name="TUpdateModel">The type of the update model.</typeparam>
/// <typeparam name="TUpdateResultModel">The type of the update result model.</typeparam>
/// <typeparam name="TRepository">The type of the repository.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
/// <seealso cref="UmbrellaDataAccessApiController" />
[UmbrellaProducesResponseType(StatusCodes.Status401Unauthorized)]
[UmbrellaProducesResponseType(StatusCodes.Status403Forbidden)]
[UmbrellaProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
[UmbrellaProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
[UmbrellaProducesResponseType(StatusCodes.Status500InternalServerError)]
public abstract class UmbrellaGenericRepositoryApiController<TSlimModel, TPaginatedResultModel, TModel, TCreateModel, TCreateResultModel, TUpdateModel, TUpdateResultModel, TRepository, TEntity, TRepositoryOptions, TEntityKey> : UmbrellaDataAccessApiController
	where TPaginatedResultModel : PaginatedResultModel<TSlimModel>, new()
	where TCreateResultModel : ICreateResultModel<TEntityKey>
	where TUpdateModel : IUpdateModel<TEntityKey>
	where TUpdateResultModel : IUpdateResultModel
	where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TRepositoryOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Gets the repository.
	/// </summary>
	protected Lazy<TRepository> Repository { get; }

	/// <summary>
	/// Gets a value indicating whether the <c>SearchSlim</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool SlimReadEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Get</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool ReadEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Post</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool CreateEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Put</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool UpdateEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Delete</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool DeleteEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether imperative authorization checks are performed when the <c>SearchSlim</c> endpoint is executed.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers where imperative authorization checks are not required.
	/// </remarks>
	protected virtual bool AuthorizationSlimReadChecksEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether imperative authorization checks are performed when the <c>Get</c> endpoint is executed.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers where imperative authorization checks are not required.
	/// </remarks>
	protected virtual bool AuthorizationReadChecksEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether imperative authorization checks are performed when the <c>Post</c> endpoint is executed.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers where imperative authorization checks are not required.
	/// </remarks>
	protected virtual bool AuthorizationCreateChecksEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether imperative authorization checks are performed when the <c>Put</c> endpoint is executed.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers where imperative authorization checks are not required.
	/// </remarks>
	protected virtual bool AuthorizationUpdateChecksEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether imperative authorization checks are performed when the <c>Delete</c> endpoint is executed.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// controllers where imperative authorization checks are not required.
	/// </remarks>
	protected virtual bool AuthorizationDeleteChecksEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether or not entities loaded during calls to the <c>Get</c> endpoint
	/// are loaded with change tracking enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="false" /> and should only be enabled under exceptional circumstances, e.g. when dealing with EF Core shadow properties.
	/// </remarks>
	protected virtual bool GetTrackChanges { get; }

	/// <summary>
	/// Gets a value indicating whether calls to the <c>Get</c> endpoint should be synchronized
	/// using the <see cref="IEntity{TEntityKey}.Id"/> of the entity being loaded.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="false"/>.
	/// </remarks>
	protected virtual bool GetLock { get; }

	/// <summary>
	/// Gets a value indicating whether calls to the <c>Post</c> endpoint should be synchronized
	/// using a synchronization key created by a call to the <see cref="UmbrellaDataAccessApiController.GetCreateSynchronizationRootKey"/> method.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="false"/>.
	/// </remarks>
	protected virtual bool PostLock { get; }

	/// <summary>
	/// Gets a value indicating whether calls to the <c>Put</c> endpoint should be synchronized
	/// using the <see cref="IEntity{TEntityKey}.Id"/> of the entity being updated.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="false"/>.
	/// </remarks>
	protected virtual bool PutLock { get; }

	/// <summary>
	/// Gets a value indicating whether calls to the <c>Delete</c> endpoint should be synchronized
	/// using the <see cref="IEntity{TEntityKey}.Id"/> of the entity being deleted.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="false"/>.
	/// </remarks>
	protected virtual bool DeleteLock { get; }

	/// <summary>
	/// Gets the <see cref="IncludeMap{TEntity}"/> used by the <c>SearchSlim</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but should be specified when entities associated using navigation properties need to be loaded in addition
	/// to the primary entity of type <typeparamref name="TEntity"/>.
	/// </remarks>
	protected virtual IncludeMap<TEntity>? SearchSlimIncludeMap { get; }

	/// <summary>
	/// Gets the <see cref="IncludeMap{TEntity}"/> used by the <c>Get</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but should be specified when entities associated using navigation properties need to be loaded in addition
	/// to the primary entity of type <typeparamref name="TEntity"/>.
	/// </remarks>
	protected virtual IncludeMap<TEntity>? GetIncludeMap { get; }

	/// <summary>
	/// Gets the <see cref="IncludeMap{TEntity}"/> used by the <c>Put</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but should be specified when entities associated using navigation properties need to be loaded in addition
	/// to the primary entity of type <typeparamref name="TEntity"/>.
	/// </remarks>
	protected virtual IncludeMap<TEntity>? PutIncludeMap { get; }

	/// <summary>
	/// Gets the <see cref="IncludeMap{TEntity}"/> used by the <c>Delete</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but should be specified when entities associated using navigation properties need to be loaded in addition
	/// to the primary entity of type <typeparamref name="TEntity"/>.
	/// </remarks>
	protected virtual IncludeMap<TEntity>? DeleteIncludeMap { get; }

	/// <summary>
	/// Gets the <typeparamref name="TRepositoryOptions"/> used by the <c>SearchSlim</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but can be provided in order to pass options into the repository calls that can be
	/// used by them internally to customise data processing based on these options.
	/// </remarks>
	protected virtual TRepositoryOptions? SearchSlimRepoOptions { get; }

	/// <summary>
	/// Gets the <typeparamref name="TRepositoryOptions"/> used by the <c>Get</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but can be provided in order to pass options into the repository calls that can be
	/// used by them internally to customise data processing based on these options.
	/// </remarks>
	protected virtual TRepositoryOptions? GetRepoOptions { get; }

	/// <summary>
	/// Gets the <typeparamref name="TRepositoryOptions"/> used by the <c>Post</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but can be provided in order to pass options into the repository calls that can be
	/// used by them internally to customise data processing based on these options.
	/// </remarks>
	protected virtual TRepositoryOptions? PostRepoOptions { get; }

	/// <summary>
	/// Gets the <typeparamref name="TRepositoryOptions"/> used by the <c>Put</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but can be provided in order to pass options into the repository calls that can be
	/// used by them internally to customise data processing based on these options.
	/// </remarks>
	protected virtual TRepositoryOptions? PutRepoOptions { get; }

	/// <summary>
	/// Gets the <typeparamref name="TRepositoryOptions"/> used by the <c>Delete</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// This is initially <see langword="null"/> but can be provided in order to pass options into the repository calls that can be
	/// used by them internally to customise data processing based on these options.
	/// </remarks>
	protected virtual TRepositoryOptions? DeleteRepoOptions { get; }

	/// <summary>
	/// Gets the child <see cref="RepoOptions"/> used by the <c>SearchSlim</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is initially <see langword="null"/> but can be provided in order to pass child options into the repository calls that can be
	/// used by them internally to pass down to depedent repositories.
	/// </para>
	/// <para>
	/// e.g. A Person may have a collection of Pet entities which means
	/// that there would be a PersonRepository and a PetRepository. If the PersonRepository performs some processing on the Pet entity collection,
	/// it might do this by calling into the PetRepository. Specifying these child options means that they will be passed down to child repositories
	/// such as the PetRepository automatically.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<RepoOptions> SearchSlimChildRepoOptions { get; } = [];

	/// <summary>
	/// Gets the child <see cref="RepoOptions"/> used by the <see cref="GetAsync"/> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is initially <see langword="null"/> but can be provided in order to pass child options into the repository calls that can be
	/// used by them internally to pass down to depedent repositories.
	/// </para>
	/// <para>
	/// e.g. A Person may have a collection of Pet entities which means
	/// that there would be a PersonRepository and a PetRepository. If the PersonRepository performs some processing on the Pet entity collection,
	/// it might do this by calling into the PetRepository. Specifying these child options means that they will be passed down to child repositories
	/// such as the PetRepository automatically.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<RepoOptions> GetChildRepoOptions { get; } = [];

	/// <summary>
	/// Gets the child <see cref="RepoOptions"/> used by the <c>Post</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is initially <see langword="null"/> but can be provided in order to pass child options into the repository calls that can be
	/// used by them internally to pass down to depedent repositories.
	/// </para>
	/// <para>
	/// e.g. A Person may have a collection of Pet entities which means
	/// that there would be a PersonRepository and a PetRepository. If the PersonRepository performs some processing on the Pet entity collection,
	/// it might do this by calling into the PetRepository. Specifying these child options means that they will be passed down to child repositories
	/// such as the PetRepository automatically.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<RepoOptions> PostChildRepoOptions { get; } = [];

	/// <summary>
	/// Gets the child <see cref="RepoOptions"/> used by the <c>Put</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is initially <see langword="null"/> but can be provided in order to pass child options into the repository calls that can be
	/// used by them internally to pass down to depedent repositories.
	/// </para>
	/// <para>
	/// e.g. A Person may have a collection of Pet entities which means
	/// that there would be a PersonRepository and a PetRepository. If the PersonRepository performs some processing on the Pet entity collection,
	/// it might do this by calling into the PetRepository. Specifying these child options means that they will be passed down to child repositories
	/// such as the PetRepository automatically.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<RepoOptions> PutChildRepoOptions { get; } = [];

	/// <summary>
	/// Gets the child <see cref="RepoOptions"/> used by the <c>Delete</c> endpoint when loading entities from the repository.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is initially <see langword="null"/> but can be provided in order to pass child options into the repository calls that can be
	/// used by them internally to pass down to depedent repositories.
	/// </para>
	/// <para>
	/// e.g. A Person may have a collection of Pet entities which means
	/// that there would be a PersonRepository and a PetRepository. If the PersonRepository performs some processing on the Pet entity collection,
	/// it might do this by calling into the PetRepository. Specifying these child options means that they will be passed down to child repositories
	/// such as the PetRepository automatically.
	/// </para>
	/// </remarks>
	protected virtual IReadOnlyCollection<RepoOptions> DeleteChildRepoOptions { get; } = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaGenericRepositoryApiController{TSlimModel, TPaginatedResultModel, TModel, TCreateModel, TCreateResultModel, TUpdateModel, TUpdateResultModel, TRepository, TEntity, TRepositoryOptions, TEntityKey}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <param name="mapper">The mapper.</param>
	/// <param name="repository">The repository.</param>
	/// <param name="dataAccessService">The data access service.</param>
	protected UmbrellaGenericRepositoryApiController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment,
		IUmbrellaMapper mapper,
		Lazy<TRepository> repository,
		IUmbrellaRepositoryCoreDataService dataAccessService)
		: base(logger, hostingEnvironment, mapper, dataAccessService)
	{
		Repository = repository;
	}

	/// <summary>
	/// An API endpoint used to load paginated entities in bulk from the repository based on the specified <paramref name="sorters"/> and <paramref name="filters"/>
	/// with each result mapped to a collection of <typeparamref name="TSlimModel"/> wrapped in a <typeparamref name="TPaginatedResultModel"/>.
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
	/// <seealso cref="UmbrellaDataAccessApiController.ReadAllAsync"/>
	[HttpGet("SearchSlim")]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual Task<IActionResult> SearchSlimAsync(int pageNumber, int pageSize, [FromQuery] SortExpression<TEntity>[]? sorters = null, [FromQuery] FilterExpression<TEntity>[]? filters = null, FilterExpressionCombinator? filterCombinator = null, CancellationToken cancellationToken = default)
		=> SlimReadEndpointEnabled
		? ReadAllAsync<TEntity, TEntity, TEntityKey, TRepositoryOptions, TSlimModel, TPaginatedResultModel>(
			pageNumber,
			pageSize,
			sorters,
			filters,
			filterCombinator,
			LoadSearchSlimDataAsync,
			cancellationToken,
			null,
			AfterCreateSearchSlimModelAsync,
			AfterReadSlimEntityAsync,
			SearchSlimRepoOptions,
			SearchSlimChildRepoOptions,
			AuthorizationSlimReadChecksEnabled,
			BeforeSearchSlimAsync)
		: Task.FromResult<IActionResult>(MethodNotAllowed("Unsupported Endpoint"));

	/// <summary>
	/// An API endpoint used to load a single <typeparamref name="TEntity"/> in from the repository based on the specified <paramref name="id"/> and return a
	/// mapped <typeparamref name="TModel"/>.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	/// <exception cref="NotSupportedException">Unsupported Endpoint</exception>
	/// <remarks>
	/// This endpoint calls into the <c>ReadAsync</c> base controller method.
	/// Please see this for further details regarding behaviour.
	/// </remarks>
	/// <seealso cref="UmbrellaDataAccessApiController.ReadAsync"/>
	[HttpGet]
	[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
	[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual Task<IActionResult> GetAsync(TEntityKey id, CancellationToken cancellationToken = default)
		=> ReadEndpointEnabled
		? ReadAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel>(
			id,
			Repository,
			cancellationToken,
			LoadReadEntityAsync,
			null,
			(entity, model) => AfterReadEntityAsync(entity, model, cancellationToken),
			GetTrackChanges,
			GetIncludeMap,
			GetRepoOptions,
			GetChildRepoOptions,
			AuthorizationReadChecksEnabled,
			GetLock,
			BeforeReadAsync)
		: Task.FromResult<IActionResult>(MethodNotAllowed("Unsupported Endpoint"));

	/// <summary>
	/// An API endpoint used to create a new <typeparamref name="TEntity"/> in the repository based on the provided <typeparamref name="TCreateModel"/> which returns
	/// a <typeparamref name="TCreateResultModel"/> if successful.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TCreateResultModel"/> when successful or
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
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	public virtual Task<IActionResult> PostAsync(TCreateModel model, CancellationToken cancellationToken = default)
		=> CreateEndpointEnabled
		? CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TCreateModel, TCreateResultModel>(
			model,
			Repository,
			cancellationToken,
			() => BeforeCreateMappingModelToEntityAsync(model, cancellationToken),
			null,
			entity => BeforeCreateEntityAsync(entity, model, cancellationToken),
			null,
			(entity, result) => AfterCreateEntityAsync(entity, model, result, cancellationToken),
			PostRepoOptions,
			PostChildRepoOptions,
			AuthorizationCreateChecksEnabled,
			PostLock)
		: Task.FromResult<IActionResult>(MethodNotAllowed("Unsupported Endpoint"));

	/// <summary>
	/// An API endpoint used to update an existing <typeparamref name="TEntity"/> in the repository based on the provided <typeparamref name="TUpdateModel"/> which returns
	/// a <typeparamref name="TUpdateResultModel"/> if successful.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TUpdateResultModel"/> when successful or
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
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	public virtual Task<IActionResult> PutAsync(TUpdateModel model, CancellationToken cancellationToken = default)
		=> UpdateEndpointEnabled
		? UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TUpdateModel, TUpdateResultModel>(
			model,
			Repository,
			cancellationToken,
			entity => BeforeUpdateMappingModelToEntityAsync(entity, model, cancellationToken),
			null,
			entity => BeforeUpdateEntityAsync(entity, model, cancellationToken),
			null,
			(entity, result) => AfterUpdateEntityAsync(entity, model, result, cancellationToken),
			PutIncludeMap,
			PutRepoOptions,
			PutChildRepoOptions,
			AuthorizationUpdateChecksEnabled,
			PutLock)
		: Task.FromResult<IActionResult>(MethodNotAllowed("Unsupported Endpoint"));

	/// <summary>
	/// An API endpoint used to delete a single <typeparamref name="TEntity"/> in from the repository based on the specified <paramref name="id"/>.
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
	[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
	[UmbrellaProducesResponseType(StatusCodes.Status409Conflict)]
	public virtual Task<IActionResult> DeleteAsync(TEntityKey id, CancellationToken cancellationToken = default)
		=> DeleteEndpointEnabled
		? DeleteAsync(
			id,
			Repository,
			BeforeDeleteEntityAsync,
			AfterDeleteEntityAsync,
			cancellationToken,
			DeleteIncludeMap,
			DeleteRepoOptions,
			DeleteChildRepoOptions,
			AuthorizationDeleteChecksEnabled,
			DeleteLock)
		: Task.FromResult<IActionResult>(MethodNotAllowed("Unsupported Endpoint"));

	/// <summary>
	/// Loads the paginated results from the <typeparamref name="TRepository"/> using the specified parameters. This method is called internally by the <c>SearchSlim</c> method.
	/// </summary>
	/// <remarks>
	/// This calls the <c>FindAllAsync</c> method on the <typeparamref name="TRepository"/> by default. Override this method to change this behaviour.
	/// </remarks>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The paginated collection of entities from the <typeparamref name="TRepository"/>.</returns>
	protected virtual Task<PaginatedResultModel<TEntity>> LoadSearchSlimDataAsync(int pageNumber, int pageSize, SortExpression<TEntity>[]? sorters, FilterExpression<TEntity>[]? filters, FilterExpressionCombinator? filterCombinator, TRepositoryOptions? options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken) => Repository.Value.FindAllAsync(pageNumber, pageSize, false, SearchSlimIncludeMap, sorters, filters, filterCombinator ?? FilterExpressionCombinator.And, options, childOptions, cancellationToken: cancellationToken);

	/// <summary>
	/// Loads the entity with the specified <paramref name="id"/> from the <typeparamref name="TRepository"/> using the specified parameters.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="trackChanges">Whether to track changes.</param>
	/// <param name="includeMap">The include map.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The entity with the specified <paramref name="id"/>.</returns>
	protected virtual Task<TEntity?> LoadReadEntityAsync(TEntityKey id, bool trackChanges, IncludeMap<TEntity>? includeMap, TRepositoryOptions? options, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken) => Repository.Value.FindByIdAsync(id, trackChanges, includeMap, options, childOptions, cancellationToken);

	/// <summary>
	/// This is called by the <c>SearchSlim</c> endpoint before any entities are loaded from the <typeparamref name="TRepository"/>.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to validate the search request and return a non-null result to short-circuit the operation.
	/// </remarks>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> containing an error. Return <see langword="null"/> to continue the operation.</returns>
	protected virtual Task<IOperationResult?> BeforeSearchSlimAsync(int pageNumber, int pageSize, SortExpression<TEntity>[]? sorters, FilterExpression<TEntity>[]? filters, FilterExpressionCombinator? filterCombinator, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Get</c> endpoint before the entity is loaded from the <typeparamref name="TRepository"/>.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to validate the read request and return a non-null result to short-circuit the operation.
	/// </remarks>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> containing an error. Return <see langword="null"/> to continue the operation.</returns>
	protected virtual Task<IOperationResult?> BeforeReadAsync(TEntityKey id, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>SearchSlim</c> endpoint immediately after the <typeparamref name="TPaginatedResultModel"/> has been created containing the mapped
	/// entity instances onto <typeparamref name="TSlimModel"/> instances.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output model.
	/// </remarks>
	/// <param name="results">The results.</param>
	/// <param name="model">The model.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterCreateSearchSlimModelAsync(PaginatedResultModel<TEntity> results, TPaginatedResultModel model, SortExpression<TEntity>[]? sorters, FilterExpression<TEntity>[]? filters, FilterExpressionCombinator? filterCombinator, CancellationToken cancellationToken) => Task.CompletedTask;

	/// <summary>
	/// This is called by the <c>SearchSlim</c> endpoint immediately after the <see cref="AfterCreateSearchSlimModelAsync"/> has been called and is called for each entity / model pair.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> AfterReadSlimEntityAsync(TEntity entity, TSlimModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Get</c> endpoint after the entity has been read, auth checks have been completed, and the entity has been mapped to an <typeparamref name="TModel"/>.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output model.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> AfterReadEntityAsync(TEntity entity, TModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Post</c> endpoint before the <typeparamref name="TModel"/> has been mapped to a new instance of <typeparamref name="TEntity"/>.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeCreateMappingModelToEntityAsync(TCreateModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Post</c> endpoint after the <typeparamref name="TModel"/> has been mapped to a new instance of <typeparamref name="TEntity"/>
	/// but before auth checks are performed.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeCreateEntityAsync(TEntity entity, TCreateModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Put</c> endpoint before the <typeparamref name="TModel"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeUpdateMappingModelToEntityAsync(TEntity entity, TUpdateModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Put</c> endpoint after the <typeparamref name="TModel"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>
	/// but before auth checks are performed.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeUpdateEntityAsync(TEntity entity, TUpdateModel model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Delete</c> endpoint before the <typeparamref name="TEntity"/> has been deleted from the <typeparamref name="TRepository"/>.
	/// It is called immediately after loading and performing auth checks.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeDeleteEntityAsync(TEntity entity, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Post</c> endpoint after the <typeparamref name="TCreateModel"/> has been mapped to a new instance of <typeparamref name="TEntity"/>,
	/// saved to the database and the <typeparamref name="TCreateResultModel"/> has been created.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterCreateEntityAsync(TEntity entity, TCreateModel model, TCreateResultModel result, CancellationToken cancellationToken) => Task.CompletedTask;

	/// <summary>
	/// This is called by the <c>Put</c> endpoint after the <typeparamref name="TUpdateModel"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>,
	/// saved to the database and the <typeparamref name="TUpdateResultModel"/> has been created.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterUpdateEntityAsync(TEntity entity, TUpdateModel model, TUpdateResultModel result, CancellationToken cancellationToken) => Task.CompletedTask;

	/// <summary>
	/// This is called by the <c>Delete</c> endpoint after the <typeparamref name="TEntity"/> has been deleted from the <typeparamref name="TRepository"/>.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterDeleteEntityAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;
}
