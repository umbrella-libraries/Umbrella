using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions.Exceptions;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// Provides a generic, extensible base class for implementing data services that interact with a repository using CRUD
/// operations, supporting advanced features such as authorization, synchronization, pagination, and entity mapping.
/// </summary>
/// <remarks>This base class is designed to be inherited by concrete data service implementations that require
/// repository-based data access with support for advanced scenarios such as endpoint enablement, authorization checks,
/// synchronization, and entity mapping. It provides extensibility points for customizing behavior at various stages of
/// the CRUD lifecycle, including hooks for pre- and post-processing, and options for controlling repository and child
/// repository behavior. Derived classes can override protected virtual members to tailor the service to specific
/// requirements, such as disabling endpoints, customizing authorization, or modifying data loading
/// strategies.</remarks>
/// <typeparam name="TItem">The type representing the full data model returned by read operations. Must implement <see cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TSlimItem">The type representing a lightweight or summary version of the data model, typically used for paginated or list
/// results. Must implement <see cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type representing a paginated collection of slim items. Must inherit from <see cref="PaginatedResultModel{TItem}"/>.</typeparam>
/// <typeparam name="TCreateItem">The type representing the model used to create new entities.</typeparam>
/// <typeparam name="TCreateResult">The type representing the result returned after a successful create operation.</typeparam>
/// <typeparam name="TUpdateItem">The type representing the model used to update existing entities. Must implement <see cref="IKeyedItem{TEntityKey}"/>.</typeparam>
/// <typeparam name="TUpdateResult">The type representing the result returned after a successful update operation.</typeparam>
/// <typeparam name="TRepository">The type of the repository used for data access operations. Must implement <see cref="IGenericDbRepository{TEntity, TRepoOptions, TEntityKey}"/>.</typeparam>
/// <typeparam name="TEntity">The type of the entity managed by the repository. Must implement <see cref="IEntity{TEntityKey}"/>.</typeparam>
/// <typeparam name="TRepositoryOptions">The type representing repository-specific options used to customize data access behavior. Must inherit from
/// RepoOptions and have a parameterless constructor.</typeparam>
/// <typeparam name="TEntityKey">The type of the key used to uniquely identify entities. Must implement <see cref="IEquatable{T}"/>.</typeparam>
public abstract class UmbrellaRepositoryDataService<TItem, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult, TRepository, TEntity, TRepositoryOptions, TEntityKey> :
	UmbrellaRepositoryCoreDataService,
	IUmbrellaRepositoryDataService<TItem, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult, TRepository, TEntity, TRepositoryOptions, TEntityKey>
	where TItem : class, IKeyedItem<TEntityKey>
	where TSlimItem : class, IKeyedItem<TEntityKey>
	where TUpdateItem : class, IKeyedItem<TEntityKey>, IUpdateModel<TEntityKey>
	where TUpdateResult : IUpdateResultModel, new()
	where TPaginatedResultModel : PaginatedResultModel<TSlimItem>, new()
	where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TRepositoryOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
	where TCreateResult : ICreateResultModel<TEntityKey>, new()
{
	/// <summary>
	/// Initializes a new instance of the UmbrellaRepositoryDataService class with the specified dependencies.
	/// </summary>
	/// <param name="logger">The logger used to record diagnostic and operational information for the data service.</param>
	/// <param name="hostingEnvironment">The hosting environment in which the application is running. Used to access environment-specific information.</param>
	/// <param name="options">The configuration options for the data service. Provides settings that control service behavior.</param>
	/// <param name="mapper">The mapper used to convert between data entities and domain models.</param>
	/// <param name="authorizationService">The authorization service used to enforce access control policies for data operations.</param>
	/// <param name="synchronizationManager">The synchronization manager responsible for coordinating data consistency across operations.</param>
	/// <param name="dataAccessUnitOfWork">A lazily-initialized unit of work for managing data access transactions.</param>
	/// <param name="repository">A lazily-initialized repository used to perform data operations on the underlying data store.</param>
	/// <param name="dataExpressionFactory">The factory used to create data expression instances for sorting and filtering.</param>
	protected UmbrellaRepositoryDataService(
		ILogger<UmbrellaRepositoryCoreDataService> logger,
		IHostEnvironment hostingEnvironment,
		UmbrellaRepositoryDataServiceOptions options,
		IUmbrellaMapper mapper,
		IUmbrellaAuthorizationService authorizationService,
		ISynchronizationManager synchronizationManager,
		Lazy<IDataAccessUnitOfWork> dataAccessUnitOfWork,
		Lazy<TRepository> repository,
		IDataExpressionFactory dataExpressionFactory)
		: base(logger, hostingEnvironment, options, mapper, authorizationService, synchronizationManager, dataAccessUnitOfWork)
	{
		Repository = repository;
		DataExpressionFactory = dataExpressionFactory;
	}

	/// <summary>
	/// Gets the repository.
	/// </summary>
	protected Lazy<TRepository> Repository { get; }

	/// <summary>
	/// Gets the factory used to create data expression instances for sorting and filtering.
	/// </summary>
	protected IDataExpressionFactory DataExpressionFactory { get; }

	/// <summary>
	/// Gets a value indicating whether the <c>SearchSlim</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool SlimReadEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Get</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool ReadEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Post</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool CreateEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Put</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool UpdateEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the <c>Delete</c> endpoint is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool DeleteEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the endpoint for checking the existence of an entity by its identifier is enabled.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool ExistsByIdEndpointEnabled { get; } = true;

	/// <summary>
	/// Gets a value indicating whether the total count endpoint is enabled for the resource.
	/// </summary>
	/// <remarks>
	/// This defaults to <see langword="true" /> but should be overridden and set to <see langword="false" /> in derived
	/// services that should not allow the endpoint to be accessed if it is not needed.
	/// </remarks>
	protected virtual bool TotalCountEndpointEnabled { get; } = true;

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
	/// using a synchronization key created by a call to the <see cref="GetCreateSynchronizationRootKey(object)"/> method.
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
	/// Gets a value indicating whether result models created when the <c>Post</c> endpoint is called
	/// should be automatically mapped from the created entity using the <see cref="UmbrellaRepositoryCoreDataService.Mapper"/>.
	/// </summary>
	/// <remarks>
	/// <para>Defaults to <see langword="true"/>.</para>
	/// <para>
	/// This should normally always be set to <see langword="true"/>.
	/// It exists because output mapping was not supported by previous versions of this code.
	/// In future versions, this property will be removed with output mapping always being enabled.
	/// </para>
	/// </remarks>
	protected virtual bool EnablePostOutputMapping { get; } = true;

	/// <summary>
	/// Gets a value indicating whether result models created when the <c>Put</c> endpoint is called
	/// should be automatically mapped from the updated entity using the <see cref="UmbrellaRepositoryCoreDataService.Mapper"/>.
	/// </summary>
	/// <remarks>
	/// <para>Defaults to <see langword="true"/>.</para>
	/// <para>
	/// This should normally always be set to <see langword="true"/>.
	/// It exists because output mapping was not supported by previous versions of this code.
	/// In future versions, this property will be removed with output mapping always being enabled.
	/// </para>
	/// </remarks>
	protected virtual bool EnablePutOutputMapping { get; } = true;

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
	/// Gets the child <see cref="RepoOptions"/> used by the <see cref="FindByIdAsync"/> endpoint when loading entities from the repository.
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

	/// <inheritdoc/>
	public async Task<IOperationResult<TCreateResult?>> CreateAsync(TCreateItem item, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!CreateEndpointEnabled)
			return OperationResult<TCreateResult?>.NotAllowed("The create endpoint is not enabled.");

		try
		{
			return await CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TCreateItem, TCreateResult>(
				item,
				Repository,
				cancellationToken,
				() => BeforeCreateMappingModelToEntityAsync(item, cancellationToken),
				null,
				entity => BeforeCreateEntityAsync(entity, item, cancellationToken),
				null,
				(entity, result) => AfterCreateEntityAsync(entity, item, result, cancellationToken),
				PostRepoOptions,
				PostChildRepoOptions,
				AuthorizationCreateChecksEnabled,
				PostLock,
				GetCreateSynchronizationRootKey,
				EnablePostOutputMapping)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There was a problem creating the entity.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult> DeleteAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!DeleteEndpointEnabled)
			return OperationResult.NotAllowed("The delete endpoint is not enabled.");

		try
		{
			return await DeleteAsync(
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
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataAccessException("There was a problem deleting the entity.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult<bool>> ExistsByIdAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!ExistsByIdEndpointEnabled)
			return OperationResult<bool>.NotAllowed("The exists by ID endpoint is not enabled.");

		try
		{
			var result = await ExistsByIdAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
				id,
				Repository,
				cancellationToken).ConfigureAwait(false);

			return result.Status switch
			{
				OperationResultStatus.NoContent => OperationResult<bool>.Success(true),
				OperationResultStatus.NotFound => OperationResult<bool>.Success(false),
				_ => throw new InvalidOperationException($"Unexpected {nameof(OperationResultStatus)} value '{result.Status}' returned from repository.")
			};
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataAccessException("There was a problem checking for the existence of the entity.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult<TPaginatedResultModel?>> FindAllSlimAsync(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpressionDescriptor>? sorters = null, IEnumerable<FilterExpressionDescriptor>? filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.And, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!SlimReadEndpointEnabled)
			return OperationResult<TPaginatedResultModel?>.NotAllowed("The slim read endpoint is not enabled.");

		try
		{
			ClampPaginationParameters(ref pageNumber, ref pageSize);

			// Convert the sorter and filter descriptors into expressions
			List<SortExpression<TEntity>>? sortExpressions = null;

			if (sorters is not null)
			{
				foreach (SortExpressionDescriptor sorter in sorters)
				{
					var sortExpression = DataExpressionFactory.Create(typeof(SortExpression<TEntity>), sorter);

					if (sortExpression is SortExpression<TEntity> typedExpression)
					{
						sortExpressions ??= [];
						sortExpressions.Add(typedExpression);
					}
				}
			}

			List<FilterExpression<TEntity>>? filterExpressions = null;

			if (filters is not null)
			{
				foreach (FilterExpressionDescriptor filter in filters)
				{
					var filterExpression = DataExpressionFactory.Create(typeof(FilterExpression<TEntity>), filter);

					if (filterExpression is FilterExpression<TEntity> typedExpression)
					{
						filterExpressions ??= [];
						filterExpressions.Add(typedExpression);
					}
				}
			}

			return await base.ReadAllAsync<TEntity, TEntity, TEntityKey, TRepositoryOptions, TSlimItem, TPaginatedResultModel>(
				pageNumber,
				pageSize,
				sortExpressions?.ToArray(),
				filterExpressions?.ToArray(),
				filterCombinator,
				LoadSearchSlimDataAsync,
				cancellationToken,
				null,
				AfterCreateSearchSlimModelAsync,
				AfterReadSlimEntityAsync,
				SearchSlimRepoOptions,
				SearchSlimChildRepoOptions,
				AuthorizationSlimReadChecksEnabled).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { pageNumber, pageSize, sorters, filters, filterCombinator }))
		{
			throw new UmbrellaDataAccessException("There was a problem loading the paginated collection of entities.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult<TItem?>> FindByIdAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!ReadEndpointEnabled)
			return OperationResult<TItem?>.NotAllowed("The read endpoint is not enabled.");

		try
		{
			return await ReadAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TItem>(
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
				GetLock).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataAccessException("There was a problem loading the entity.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult<int>> FindTotalCountAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!TotalCountEndpointEnabled)
			return OperationResult<int>.NotAllowed("The total count endpoint is not enabled.");

		try
		{
			return await TotalCountAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
				Repository,
				cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There was a problem retrieving the total count of entities.", exc);
		}
	}

	/// <inheritdoc/>
	public async Task<IOperationResult<TUpdateResult?>> UpdateAsync(TUpdateItem item, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (!UpdateEndpointEnabled)
			return OperationResult<TUpdateResult?>.NotAllowed("The update endpoint is not enabled.");

		try
		{
			return await UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TUpdateItem, TUpdateResult>(
				item,
				Repository,
				cancellationToken,
				entity => BeforeUpdateMappingModelToEntityAsync(entity, item, cancellationToken),
				null,
				entity => BeforeUpdateEntityAsync(entity, item, cancellationToken),
				null,
				(entity, result) => AfterUpdateEntityAsync(entity, item, result, cancellationToken),
				PutIncludeMap,
				PutRepoOptions,
				PutChildRepoOptions,
				AuthorizationUpdateChecksEnabled,
				PutLock,
				EnablePutOutputMapping).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { item }))
		{
			throw new UmbrellaDataAccessException("There was a problem updating the entity.", exc);
		}
	}

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
	/// This is called by the <c>SearchSlim</c> endpoint immediately after the <typeparamref name="TPaginatedResultModel"/> has been created containing the mapped
	/// entity instances onto <typeparamref name="TSlimItem"/> instances.
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
	protected virtual Task<IOperationResult?> AfterReadSlimEntityAsync(TEntity entity, TSlimItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Get</c> endpoint after the entity has been read, auth checks have been completed, and the entity has been mapped to an <typeparamref name="TItem"/>.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output model.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> AfterReadEntityAsync(TEntity entity, TItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Post</c> endpoint before the <typeparamref name="TItem"/> has been mapped to a new instance of <typeparamref name="TEntity"/>.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeCreateMappingModelToEntityAsync(TCreateItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Post</c> endpoint after the <typeparamref name="TItem"/> has been mapped to a new instance of <typeparamref name="TEntity"/>
	/// but before auth checks are performed.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeCreateEntityAsync(TEntity entity, TCreateItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Put</c> endpoint before the <typeparamref name="TItem"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeUpdateMappingModelToEntityAsync(TEntity entity, TUpdateItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

	/// <summary>
	/// This is called by the <c>Put</c> endpoint after the <typeparamref name="TItem"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>
	/// but before auth checks are performed.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An optional <see cref="IOperationResult"/> that can be used to an error result if there is a problem. By default, this should return <see langword="null"/> if processing is successful.</returns>
	protected virtual Task<IOperationResult?> BeforeUpdateEntityAsync(TEntity entity, TUpdateItem model, CancellationToken cancellationToken) => Task.FromResult<IOperationResult?>(null);

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
	/// This is called by the <c>Post</c> endpoint after the <typeparamref name="TCreateItem"/> has been mapped to a new instance of <typeparamref name="TEntity"/>,
	/// saved to the database and the <typeparamref name="TCreateResult"/> has been created.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterCreateEntityAsync(TEntity entity, TCreateItem model, TCreateResult result, CancellationToken cancellationToken) => Task.CompletedTask;

	/// <summary>
	/// This is called by the <c>Put</c> endpoint after the <typeparamref name="TUpdateItem"/> has been mapped to an existing instance of <typeparamref name="TEntity"/>,
	/// saved to the database and the <typeparamref name="TUpdateResult"/> has been created.
	/// </summary>
	/// <remarks>
	/// By default, this does nothing. Override this method to add custom behaviour and augment the output models.
	/// </remarks>
	/// <param name="entity">The entity.</param>
	/// <param name="model">The model.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An Task that completes when the operation has completed.</returns>
	protected virtual Task AfterUpdateEntityAsync(TEntity entity, TUpdateItem model, TUpdateResult result, CancellationToken cancellationToken) => Task.CompletedTask;

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

#if NET6_0_OR_GREATER
		pageSize = Math.Clamp(pageSize, 1, 50);
#else
		pageSize = Math.Max(1, Math.Min(pageSize, 50));
#endif
	}

	/// <summary>
	/// Gets the synchronization key to be used internally by the <see cref="CreateAsync"/> method. It is important
	/// that this key is scoped appropriately to ensure correct locking behaviour.
	/// </summary>
	/// <param name="model">The incoming model passed into the action method.</param>
	/// <returns>A tuple containing the type and the key used to performing synchronization.</returns>
	protected virtual (Type type, string key)? GetCreateSynchronizationRootKey(object model) => null;
}