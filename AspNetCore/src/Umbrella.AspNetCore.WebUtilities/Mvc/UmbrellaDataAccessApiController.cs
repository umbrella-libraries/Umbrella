
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Hosting;
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

// TODO: Maybe we could allow shaped queries for the ReadAsync method too?
// Also, could look into creating an overload of the Generic controller that allows specifying shaped entities to avoid having to create
// custom endpoints? Would need a generic solution to this though somehow so we don't need to create a new method on the generic repository.
// Hmmmm... Maybe just do the changes for ReadAsync for now to make doing that a bit easier.
// Will need a GetEndpoint property on GenericRemoteRepository and pass this down to the call as per FindAllSlimEndpoint.

// TODO: We could expand on this approach with an UmbrellaDataAccessPageModel and UmbrellaGenericRepositoryPageModel
// Also, an UmbrellaDataAccessController and UmbrellaGenericRepositoryController.
// We could encapsulate all of the core logic into a utility class of some kind and pass in a enum to change the behaviour, e.g. Mode = Api, Mvc, RazorPage
// and that would then determine if we returned, e.g. Page(model) - RazorPages, View(model) - Mvc, Ok(model) - Api.
// Would be a good way of doing things for non-blazor projects where we use server page rendering.

// TODO: For the Create and Update methods, add in a before mapping callback.
// Also, pass in the incoming model to all callbacks where possible.

/// <summary>
/// A generic API Controller that can be used to perform CRUD operations on entities that interact with types that implement <see cref="IGenericDbRepository{TEntity, TRepoOptions, TEntityKey}"/>.
/// </summary>
/// <remarks>
/// This controller is the basis for the <see cref="UmbrellaGenericRepositoryApiController{TSlimModel, TPaginatedResultModel, TModel, TCreateModel, TCreateResultModel, TUpdateModel, TUpdateResultModel, TRepository, TEntity, TRepositoryOptions, TEntityKey}"/>
/// which is easier to use and maintain so consider using that in the first instance instead of this controller.
/// </remarks>
/// <seealso cref="UmbrellaApiController" />
public abstract class UmbrellaDataAccessApiController : UmbrellaApiController
{
	/// <summary>
	/// Gets the mapper.
	/// </summary>
	protected IUmbrellaMapper Mapper { get; }

	/// <summary>
	/// Gets the data access service used for database operations.
	/// </summary>
	protected IUmbrellaRepositoryCoreDataService DataAccessService { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataAccessApiController"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <param name="mapper">The mapper.</param>
	/// <param name="dataAccessService">The data access service.</param>
	protected UmbrellaDataAccessApiController(
		ILogger logger,
		IWebHostEnvironment hostingEnvironment,
		IUmbrellaMapper mapper,
		IUmbrellaRepositoryCoreDataService dataAccessService)
		: base(logger, hostingEnvironment)
	{
		Mapper = mapper;
		DataAccessService = dataAccessService;
	}

	/// <summary>
	/// Used to load paginated entities in bulk from the repository based on the specified <paramref name="sorters"/> and <paramref name="filters"/>
	/// with each result mapped to a collection of <typeparamref name="TItemModel"/> wrapped in a <typeparamref name="TPaginatedResultModel"/>.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Invokes the <paramref name="loadReadAllDataAsyncDelegate"/> to load all <typeparamref name="TEntityResult"/> instances from the repository.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on all loaded entities.</item>
	/// <item>Creates the <typeparamref name="TPaginatedResultModel"/> and maps the <typeparamref name="TEntityResult"/> instances to <typeparamref name="TItemModel"/> instances using the the <paramref name="mapReadAllEntitiesDelegate"/>, falling back to using the <see cref="Mapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="afterCreateSearchSlimPaginatedModelAsyncDelegate"/> to allow the <typeparamref name="TPaginatedResultModel"/> to be augmented.</item>
	/// <item>Invokes the <paramref name="afterCreateSlimModelAsyncDelegate"/> on each instance of <typeparamref name="TItemModel"/> on the <typeparamref name="TPaginatedResultModel"/> to allow each instance to be augmented.</item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TEntityResult">The type of the entity result.</typeparam>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
	/// <typeparam name="TItemModel">The type of the item model.</typeparam>
	/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="loadReadAllDataAsyncDelegate">The delegate used to load the data.</param>
	/// <param name="mapReadAllEntitiesDelegate">The delegate used to map the results loaded from the repository. If not specified, the <see cref="Mapper"/> is used.</param>
	/// <param name="afterCreateSearchSlimPaginatedModelAsyncDelegate">The delegate that is invoked after the paginated result model has been created.</param>
	/// <param name="afterCreateSlimModelAsyncDelegate">The delegate that is invoked when result models have been created.</param>
	/// <param name="options">The repository options.</param>
	/// <param name="childOptions">The child repository options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities loaded from the repository.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TPaginatedResultModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	protected virtual async Task<IActionResult> ReadAllAsync<TEntityResult, TEntity, TEntityKey, TRepositoryOptions, TItemModel, TPaginatedResultModel>(
		int pageNumber,
		int pageSize,
		SortExpression<TEntityResult>[]? sorters,
		FilterExpression<TEntity>[]? filters,
		FilterExpressionCombinator? filterCombinator,
		Func<int, int, SortExpression<TEntityResult>[]?, FilterExpression<TEntity>[]?, FilterExpressionCombinator?, TRepositoryOptions?, IEnumerable<RepoOptions>?, CancellationToken, Task<PaginatedResultModel<TEntityResult>>> loadReadAllDataAsyncDelegate,
		CancellationToken cancellationToken,
		Func<IReadOnlyCollection<TEntityResult>, TItemModel[]>? mapReadAllEntitiesDelegate = null,
		Func<PaginatedResultModel<TEntityResult>, TPaginatedResultModel, SortExpression<TEntityResult>[]?, FilterExpression<TEntity>[]?, FilterExpressionCombinator?, CancellationToken, Task>? afterCreateSearchSlimPaginatedModelAsyncDelegate = null,
		Func<TEntityResult, TItemModel, CancellationToken, Task<IOperationResult?>>? afterCreateSlimModelAsyncDelegate = null,
		TRepositoryOptions? options = null,
		IEnumerable<RepoOptions>? childOptions = null,
		bool enableAuthorizationChecks = true)
		where TEntityResult : class, IEntity<TEntityKey>
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TPaginatedResultModel : PaginatedResultModel<TItemModel>, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(loadReadAllDataAsyncDelegate);

		try
		{
			ClampPaginationParameters(ref pageNumber, ref pageSize);

			IOperationResult result = await DataAccessService.ReadAllAsync<TEntityResult, TEntity, TEntityKey, TRepositoryOptions, TItemModel, TPaginatedResultModel>(
				pageNumber,
				pageSize,
				sorters,
				filters,
				filterCombinator,
				loadReadAllDataAsyncDelegate,
				cancellationToken,
				mapReadAllEntitiesDelegate,
				afterCreateSearchSlimPaginatedModelAsyncDelegate,
				afterCreateSlimModelAsyncDelegate,
				options,
				childOptions,
				enableAuthorizationChecks)
				.ConfigureAwait(false);

			return OperationResult<TPaginatedResultModel>(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { pageNumber, pageSize, sorters = sorters?.ToSortExpressionDescriptors(), filters = filters?.ToFilterExpressionDescriptors() }, returnValue: !IsDevelopment))
		{
			return InternalServerError("An error has occurred whilst trying to get the list of items.");
		}
	}

	/// <summary>
	/// Used to load a single <typeparamref name="TEntity"/> in from the repository based on the specified <paramref name="id"/> and return a
	/// mapped <typeparamref name="TModel"/>. 
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Synchronize execution of this method if enabled using <paramref name="synchronizeAccess"/>.</item>
	/// <item>Load the <typeparamref name="TEntity"/> from the <typeparamref name="TRepository"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Maps the <typeparamref name="TEntity"/> to the <typeparamref name="TModel"/> using the <paramref name="mapperCallback"/> falling back to use the <see cref="Mapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="afterReadEntityCallback"/> to augment the <typeparamref name="TModel"/>.</item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TRepository">The type of the repository.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <param name="id">The identifier.</param>
	/// <param name="repository">The repository.</param>
	/// <param name="loadReadEntityAsyncDelegate">The optional delegate used to load the entity. If not specified, the <c>FindByIdAsync</c> method on the <paramref name="repository"/> will be used.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="mapperCallback">The mapper callback. If not specified, <see cref="Mapper"/> is used to perform the mapping.</param>
	/// <param name="afterReadEntityCallback">The after read entity callback.</param>
	/// <param name="trackChanges">if set to <see langword="true"/> enable change tracking on the database context.</param>
	/// <param name="map">The map.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities loaded from the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using the <paramref name="id"/> and type name of the entity.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	protected async Task<IActionResult> ReadAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel>(
		TEntityKey id,
		Lazy<TRepository> repository,
		CancellationToken cancellationToken,
		Func<TEntityKey, bool, IncludeMap<TEntity>?, TRepositoryOptions?, IEnumerable<RepoOptions>?, CancellationToken, Task<TEntity?>>? loadReadEntityAsyncDelegate = null,
		Func<TEntity, TModel>? mapperCallback = null,
		Func<TEntity, TModel, Task<IOperationResult?>>? afterReadEntityCallback = null,
		bool trackChanges = false,
		IncludeMap<TEntity>? map = null,
		TRepositoryOptions? options = null,
		IEnumerable<RepoOptions>? childOptions = null,
		bool enableAuthorizationChecks = true,
		bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		try
		{
			IOperationResult result = await DataAccessService.ReadAsync(
				id,
				repository,
				cancellationToken,
				loadReadEntityAsyncDelegate,
				mapperCallback,
				afterReadEntityCallback,
				trackChanges,
				map,
				options,
				childOptions,
				enableAuthorizationChecks,
				synchronizeAccess)
				.ConfigureAwait(false);

			return OperationResult<TModel>(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !IsDevelopment))
		{
			return InternalServerError("There has been a problem getting the specified item.");
		}
	}

	/// <summary>
	/// Used to create a new <typeparamref name="TEntity"/> in the repository based on the provided <typeparamref name="TModel"/> which returns
	/// a <typeparamref name="TResultModel"/> if successful.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Synchronize execution of this method if enabled using <paramref name="synchronizeAccess"/>.</item>
	/// <item>Invokes the <paramref name="beforeMappingCallback"/> to allow execution of custom code before performing mapping.</item>
	/// <item>Maps the <typeparamref name="TModel"/> to a new instance of <typeparamref name="TEntity"/> using the <paramref name="mapperInputCallback"/> falling back to use the <see cref="Mapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="beforeCreateEntityCallback"/> to augment the newly created <typeparamref name="TEntity"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Saves the new entity to the <typeparamref name="TRepository"/>.</item>
	/// <item>
	/// Creates the <typeparamref name="TResultModel"/> by mapping the entity using the <paramref name="mapperOutputCallback"/> if specified falling back to using the <see cref="Mapper"/>.
	/// </item>
	/// <item>
	/// Invokes the <paramref name="afterCreateEntityCallback"/> that can be specified to augment the <typeparamref name="TResultModel"/>.
	/// </item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TRepository">The type of the repository.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <typeparam name="TResultModel">The type of the result model.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="repository">The repository.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="beforeMappingCallback"></param>
	/// <param name="mapperInputCallback">The mapper input callback.</param>
	/// <param name="beforeCreateEntityCallback">The before create entity callback.</param>
	/// <param name="mapperOutputCallback">The mapper output callback.</param>
	/// <param name="afterCreateEntityCallback">The after create entity callback.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities persisted to the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using a key generated using the <see cref="GetCreateSynchronizationRootKey"/> method. This method must be overridden on the controller to make this work.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TResultModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	protected async Task<IActionResult> CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
		TModel model,
		Lazy<TRepository> repository,
		CancellationToken cancellationToken,
		Func<Task<IOperationResult?>>? beforeMappingCallback = null,
		Func<TModel, TEntity>? mapperInputCallback = null,
		Func<TEntity, Task<IOperationResult?>>? beforeCreateEntityCallback = null,
		Func<TEntity, TResultModel>? mapperOutputCallback = null,
		Func<TEntity, TResultModel, Task>? afterCreateEntityCallback = null,
		TRepositoryOptions? options = null,
		IEnumerable<RepoOptions>? childOptions = null,
		bool enableAuthorizationChecks = true,
		bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TResultModel : ICreateResultModel<TEntityKey>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		try
		{
			IOperationResult result = await DataAccessService.CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
				model,
				repository,
				cancellationToken,
				beforeMappingCallback,
				mapperInputCallback,
				beforeCreateEntityCallback,
				mapperOutputCallback,
				afterCreateEntityCallback,
				options,
				childOptions,
				enableAuthorizationChecks,
				synchronizeAccess,
				GetCreateSynchronizationRootKey)
				.ConfigureAwait(false);

			return OperationResult<TResultModel>(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !IsDevelopment))
		{
			return InternalServerError("There has been a problem creating the specified item.");
		}
	}

	/// <summary>
	/// Used to update an existing <typeparamref name="TEntity"/> in the repository based on the provided <typeparamref name="TModel"/> which returns
	/// a <typeparamref name="TResultModel"/> if successful.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Synchronize execution of this method if enabled using <paramref name="synchronizeAccess"/>.</item>
	/// <item>Loads the existing <typeparamref name="TEntity"/> from the <typeparamref name="TRepository"/>.</item>
	/// <item>Checks for mismatches between <c>ConcurrencyStamp</c> property values.</item>
	/// <item>Invokes the <paramref name="beforeMappingCallback"/> to allow execution of custom code before performing mapping.</item>
	/// <item>Maps the <typeparamref name="TModel"/> to an existing instance of <typeparamref name="TEntity"/> using the <paramref name="mapperInputCallback"/> falling back to use the <see cref="Mapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="beforeUpdateEntityCallback"/> to augment the updated <typeparamref name="TEntity"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Saves the updated entity to the <typeparamref name="TRepository"/>.</item>
	/// <item>
	/// Creates the <typeparamref name="TResultModel"/> by mapping the entity using the <paramref name="mapperOutputCallback"/> if specified falling back to using the <see cref="Mapper"/>.
	/// </item>
	/// <item>
	/// Invokes the <paramref name="afterUpdateEntityCallback"/> that can be specified to augment the <typeparamref name="TResultModel"/>.
	/// </item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TRepository">The type of the repository.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
	/// <typeparam name="TModel">The type of the model.</typeparam>
	/// <typeparam name="TResultModel">The type of the result model.</typeparam>
	/// <param name="model">The model.</param>
	/// <param name="repository">The repository.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="beforeMappingCallback">The optional callback invoked before mapping.</param>
	/// <param name="mapperInputCallback">The mapper input callback.</param>
	/// <param name="beforeUpdateEntityCallback">The before update entity callback.</param>
	/// <param name="mapperOutputCallback">The mapper output callback.</param>
	/// <param name="afterUpdateEntityCallback">The after update entity callback.</param>
	/// <param name="map">The map.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities persisted to the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using the <c>Id</c> and type name of the entity.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <typeparamref name="TResultModel"/> when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	protected async Task<IActionResult> UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
		TModel model,
		Lazy<TRepository> repository,
		CancellationToken cancellationToken,
		Func<TEntity, Task<IOperationResult?>>? beforeMappingCallback = null,
		Func<TModel, TEntity, TEntity>? mapperInputCallback = null,
		Func<TEntity, Task<IOperationResult?>>? beforeUpdateEntityCallback = null,
		Func<TEntity, TResultModel>? mapperOutputCallback = null,
		Func<TEntity, TResultModel, Task>? afterUpdateEntityCallback = null,
		IncludeMap<TEntity>? map = null,
		TRepositoryOptions? options = null,
		IEnumerable<RepoOptions>? childOptions = null,
		bool enableAuthorizationChecks = true,
		bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TModel : IUpdateModel<TEntityKey>
		where TResultModel : IUpdateResultModel
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		try
		{
			IOperationResult result = await DataAccessService.UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
				model,
				repository,
				cancellationToken,
				beforeMappingCallback,
				mapperInputCallback,
				beforeUpdateEntityCallback,
				mapperOutputCallback,
				afterUpdateEntityCallback,
				map,
				options,
				childOptions,
				enableAuthorizationChecks,
				synchronizeAccess)
				.ConfigureAwait(false);

			return OperationResult<TResultModel>(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !IsDevelopment))
		{
			return InternalServerError("There has been a problem updating the specified item.");
		}
	}

	/// <summary>
	/// Used to delete an existing <typeparamref name="TEntity"/> in the repository based on the provided <paramref name="id"/> which returns
	/// a <c>204 - No Content</c> status code if successful.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Synchronize execution of this method if enabled using <paramref name="synchronizeAccess"/>.</item>
	/// <item>Loads the existing <typeparamref name="TEntity"/> from the <typeparamref name="TRepository"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Invokes the <paramref name="beforeDeleteEntityAsyncCallback"/> to perform work before deleting the entity.</item>
	/// <item>Deletes the entity from the repository.</item>
	/// <item>Invokes the <paramref name="afterDeleteEntityAsyncCallback"/> to perform work after the entity has been deleted.</item>
	/// </list>
	/// </remarks>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <typeparam name="TRepository">The type of the repository.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of the repository options.</typeparam>
	/// <param name="id">The identifier.</param>
	/// <param name="repository">The repository.</param>
	/// <param name="beforeDeleteEntityAsyncCallback">The before delete entity asynchronous callback.</param>
	/// <param name="afterDeleteEntityAsyncCallback">The after delete entity asynchronous callback.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="map">The map.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities persisted to the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using the <paramref name="id"/> and type name of the entity.</param>
	/// <returns>
	/// The action result containing the endpoint response which either be a <c>204 - No Content</c> status code when successful or
	/// a <see cref="ProblemDetails"/> response and / or erroneous state code as appropriate.
	/// </returns>
	protected async Task<IActionResult> DeleteAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
		TEntityKey id,
		Lazy<TRepository> repository,
		Func<TEntity, CancellationToken, Task<IOperationResult?>> beforeDeleteEntityAsyncCallback,
		Func<TEntity, CancellationToken, Task> afterDeleteEntityAsyncCallback,
		CancellationToken cancellationToken,
		IncludeMap<TEntity>? map = null,
		TRepositoryOptions? options = null,
		IEnumerable<RepoOptions>? childOptions = null,
		bool enableAuthorizationChecks = true,
		bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);
		Guard.IsNotNull(beforeDeleteEntityAsyncCallback);
		Guard.IsNotNull(afterDeleteEntityAsyncCallback);

		try
		{
			IOperationResult result = await DataAccessService.DeleteAsync(
				id,
				repository,
				beforeDeleteEntityAsyncCallback,
				afterDeleteEntityAsyncCallback,
				cancellationToken,
				map,
				options,
				childOptions,
				enableAuthorizationChecks,
				synchronizeAccess)
				.ConfigureAwait(false);

			return OperationResult(result);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !IsDevelopment))
		{
			return InternalServerError("There has been a problem deleting the specified item.");
		}
	}

	/// <summary>
	/// Clamps the pagination parameters.
	/// </summary>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <remarks>
	/// By default, this clamps the page number to ensure it has a minimum value of <c>1</c>
	/// and the page size to ensure it is between <c>1</c> and <c>50</c> inclusive.
	/// </remarks>
	protected virtual void ClampPaginationParameters(ref int pageNumber, ref int pageSize)
	{
		pageNumber = Math.Max(pageNumber, 1);
		pageSize = Math.Clamp(pageSize, 1, 50);
	}

	/// <summary>
	/// Gets the synchronization key to be used internally by the <see cref="CreateAsync"/> method. It is important
	/// that this key is scoped appropriately to ensure correct locking behaviour.
	/// </summary>
	/// <param name="model">The incoming model passed into the action method.</param>
	/// <returns>A tuple containing the type and the key used to performing synchronization.</returns>
	protected virtual (Type type, string key)? GetCreateSynchronizationRootKey(object model) => null;
}