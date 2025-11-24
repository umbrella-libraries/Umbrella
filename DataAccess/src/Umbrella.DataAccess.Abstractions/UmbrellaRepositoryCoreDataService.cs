// Copyright (c) Zinofi Digital Ltd. All Rights Reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Concurrency;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A generic service that can be used to perform CRUD operations on entities that interact with types that implement <see cref="IGenericDbRepository{TEntity, TRepoOptions, TEntityKey}"/>.
/// </summary>
public class UmbrellaRepositoryCoreDataService : IUmbrellaRepositoryCoreDataService
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the hosting environment.
	/// </summary>
	protected IHostEnvironment HostingEnvironment { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	protected UmbrellaRepositoryDataServiceOptions Options { get; }

	/// <summary>
	/// Gets the mapper.
	/// </summary>
	protected IUmbrellaMapper Mapper { get; }

	/// <summary>
	/// Gets the authorization service.
	/// </summary>
	protected IUmbrellaAuthorizationService AuthorizationService { get; }

	/// <summary>
	/// Gets the synchronization manager.
	/// </summary>
	protected ISynchronizationManager SynchronizationManager { get; }

	/// <summary>
	/// Gets the data access unit of work.
	/// </summary>
	protected Lazy<IDataAccessUnitOfWork> DataAccessUnitOfWork { get; }

	/// <summary>
	/// Gets the current user.
	/// </summary>
	protected static ClaimsPrincipal User => ClaimsPrincipal.Current ?? throw new InvalidOperationException("No ClaimsPrincipal found.");

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaRepositoryCoreDataService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="hostingEnvironment">The hosting environment.</param>
	/// <param name="options">The options.</param>
	/// <param name="mapper">The mapper.</param>
	/// <param name="authorizationService">The authorization service.</param>
	/// <param name="synchronizationManager">The synchronization manager.</param>
	/// <param name="dataAccessUnitOfWork">The data access unit of work.</param>
	public UmbrellaRepositoryCoreDataService(
		ILogger<UmbrellaRepositoryCoreDataService> logger,
		IHostEnvironment hostingEnvironment,
		UmbrellaRepositoryDataServiceOptions options,
		IUmbrellaMapper mapper,
		IUmbrellaAuthorizationService authorizationService,
		ISynchronizationManager synchronizationManager,
		Lazy<IDataAccessUnitOfWork> dataAccessUnitOfWork)
	{
		Logger = logger;
		HostingEnvironment = hostingEnvironment;
		Options = options;
		Mapper = mapper;
		AuthorizationService = authorizationService;
		SynchronizationManager = synchronizationManager;
		DataAccessUnitOfWork = dataAccessUnitOfWork;
	}

	/// <inheritdoc />
	public virtual async Task<IOperationResult<TPaginatedResultModel?>> ReadAllAsync<TEntityResult, TEntity, TEntityKey, TRepositoryOptions, TItemModel, TPaginatedResultModel>(
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
			PaginatedResultModel<TEntityResult> result = await loadReadAllDataAsyncDelegate(pageNumber, pageSize, sorters, filters, filterCombinator, options, childOptions, cancellationToken).ConfigureAwait(false);

			// Authorization Checks
			if (enableAuthorizationChecks)
			{
				bool authorized = await AuthorizationService.AuthorizeAllAsync(User, result.Items, Options.ReadPolicyName, cancellationToken).ConfigureAwait(false);

				if (!authorized)
					return OperationResult<TPaginatedResultModel?>.Forbidden("You do not have permission to access one or more of the items in the results.");
			}

			var model = new TPaginatedResultModel
			{
				Items = mapReadAllEntitiesDelegate is null ? await Mapper.MapAllAsync<TItemModel>(result.Items, cancellationToken).ConfigureAwait(false) : mapReadAllEntitiesDelegate(result.Items),
				PageNumber = pageNumber,
				PageSize = pageSize,
				TotalCount = result.TotalCount,
				MoreItems = pageNumber * pageSize < result.TotalCount
			};

			if (afterCreateSearchSlimPaginatedModelAsyncDelegate is not null)
				await afterCreateSearchSlimPaginatedModelAsyncDelegate(result, model, sorters, filters, filterCombinator, cancellationToken).ConfigureAwait(false);

			if (afterCreateSlimModelAsyncDelegate is not null)
			{
				for (int i = 0; i < model.Items.Count; i++)
				{
					var actionResult = await afterCreateSlimModelAsyncDelegate(result.Items.ElementAt(i), model.Items.ElementAt(i), cancellationToken).ConfigureAwait(false);

					if (actionResult is not null)
						return actionResult.ToTypedOperationResult<TPaginatedResultModel>();
				}
			}

			return OperationResult<TPaginatedResultModel>.Success(model);
		}
		catch (Exception exc) when (Options.ReadAllExceptionFilter(exc))
		{
			var result = await Options.HandleReadAllExceptionAsync(exc).ConfigureAwait(false);

			if (result is not null)
				return result.ToTypedOperationResult<TPaginatedResultModel>();

			throw;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { pageNumber, pageSize, sorters = sorters?.ToSortExpressionDescriptors(), filters = filters?.ToFilterExpressionDescriptors() }, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult<TPaginatedResultModel>.GenericFailure("An error has occurred whilst trying to get the list of items.");
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult<TModel?>> ReadAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel>(
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

		ISynchronizationRoot? syncRoot = null;

		try
		{
			if (synchronizeAccess && id.ToString() is string syncKey)
				syncRoot = await SynchronizationManager.GetSynchronizationRootAndWaitAsync<TEntity>(syncKey, cancellationToken).ConfigureAwait(false);

			TEntity? item = loadReadEntityAsyncDelegate is not null
				? await loadReadEntityAsyncDelegate(id, trackChanges, map, options, childOptions, cancellationToken).ConfigureAwait(false)
				: await repository.Value.FindByIdAsync(id, trackChanges, map, options, childOptions, cancellationToken).ConfigureAwait(false);

			if (item is null)
				return OperationResult<TModel>.NotFound("The item could not be found. Please try again.");

			// Ensure the current user has Read permissions.
			if (enableAuthorizationChecks)
			{
				bool success = await AuthorizationService.AuthorizeAsync(User, item, Options.ReadPolicyName).ConfigureAwait(false);

				if (!success)
					return OperationResult<TModel>.Forbidden("You do not have permission to access the specified item.");
			}

			TModel model = mapperCallback is null
				? await Mapper.MapAsync<TModel>(item, cancellationToken).ConfigureAwait(false)
				: mapperCallback(item);

			if (afterReadEntityCallback is not null)
			{
				IOperationResult? result = await afterReadEntityCallback(item, model).ConfigureAwait(false);

				if (result is not null)
					return result.ToTypedOperationResult<TModel>();
			}

			return OperationResult<TModel>.Success(model);
		}
		catch (Exception exc) when (Options.ReadExceptionFilter(exc))
		{
			var result = await Options.HandleReadExceptionAsync(exc).ConfigureAwait(false);

			if (result is not null)
				return result.ToTypedOperationResult<TModel>();

			throw;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult<TModel>.GenericFailure("An error has occurred whilst trying to get the specified item.");
		}
		finally
		{
			if (syncRoot is not null)
				await syncRoot.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult<TResultModel?>> CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
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
		bool synchronizeAccess = false,
		Func<object, (Type type, string key)?>? synchronizationRootKeyCreator = null,
		bool enableOutputMapping = true)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TResultModel : ICreateResultModel<TEntityKey>, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		ISynchronizationRoot? syncRoot = null;

		try
		{
			if (model is null)
				return OperationResult<TResultModel>.InvalidOperation("The request body has not been provided.");

			if (synchronizeAccess && synchronizationRootKeyCreator is not null)
			{
				(Type type, string key)? syncKey = synchronizationRootKeyCreator(model);

				if (syncKey.HasValue)
					syncRoot = await SynchronizationManager.GetSynchronizationRootAndWaitAsync(syncKey.Value.type, syncKey.Value.key, cancellationToken).ConfigureAwait(false);
			}

			if (beforeMappingCallback is not null)
			{
				var result = await beforeMappingCallback().ConfigureAwait(false);

				if (result is not null)
					return result.ToTypedOperationResult<TResultModel>();
			}

			var entity = mapperInputCallback is null
				? await Mapper.MapAsync<TEntity>(model, cancellationToken).ConfigureAwait(false)
				: mapperInputCallback(model);

			if (beforeCreateEntityCallback is not null)
			{
				IOperationResult? beforeResult = await beforeCreateEntityCallback(entity).ConfigureAwait(false);

				if (beforeResult is not null)
					return beforeResult.ToTypedOperationResult<TResultModel>();
			}

			// Ensure the current user has Create permissions.
			if (enableAuthorizationChecks)
			{
				bool success = await AuthorizationService.AuthorizeAsync(User, entity, Options.CreatePolicyName).ConfigureAwait(false);

				if (!success)
					return OperationResult<TResultModel>.Forbidden("You do not have permission to create the specified item.");
			}

			IOperationResult<TEntity> saveResult = await repository.Value.SaveEntityAsync(entity, repoOptions: options, childOptions: childOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (Options.DataAccessUnitOfWorkExclusionFilter?.Invoke(entity) is not true)
				await DataAccessUnitOfWork.Value.CommitAsync(cancellationToken).ConfigureAwait(false);

			if (saveResult.Status is OperationResultStatus.GenericSuccess)
			{
				TResultModel? result = default;

				if (enableOutputMapping)
				{
					result = mapperOutputCallback is null
						? await Mapper.MapAsync<TResultModel>(entity, cancellationToken).ConfigureAwait(false)
						: mapperOutputCallback(entity);
				}
				else
				{
					result = new TResultModel { Id = entity.Id };

					if (result is IConcurrencyStamp concurrencyStampResult && entity is IConcurrencyStamp concurrencyStampEntity)
						concurrencyStampResult.ConcurrencyStamp = concurrencyStampEntity.ConcurrencyStamp;
				}

				if (afterCreateEntityCallback is not null)
					await afterCreateEntityCallback(entity, result).ConfigureAwait(false);

				return OperationResult<TResultModel>.Created(result);
			}

			return saveResult.ValidationResults?.Count > 0
				? new OperationResult<TResultModel>
				{
					Status = OperationResultStatus.InvalidOperation,
					ValidationResults = saveResult.ValidationResults
				}
				: OperationResult<TResultModel>.InvalidOperation("An error has occurred whilst trying to create the item. Please check the model and try again.");
		}
		catch (Exception exc) when (Options.CreateExceptionFilter(exc))
		{
			OperationResult? result = await Options.HandleCreateExceptionAsync(exc).ConfigureAwait(false);

			if (result is not null)
				return result.ToTypedOperationResult<TResultModel>();

			throw;
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult<TResultModel>.GenericFailure("An error has occurred whilst trying to create the item.");
		}
		finally
		{
			if (syncRoot is not null)
				await syncRoot.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult<TResultModel?>> UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(
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
		bool synchronizeAccess = false,
		bool enableOutputMapping = true)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TModel : IUpdateModel<TEntityKey>
		where TResultModel : IUpdateResultModel, new()
	{
		cancellationToken.ThrowIfCancellationRequested();

		ISynchronizationRoot? syncRoot = null;
		Guard.IsNotNull(repository);

		try
		{
			if (synchronizeAccess && model.Id.ToString() is string syncKey)
				syncRoot = await SynchronizationManager.GetSynchronizationRootAndWaitAsync<TEntity>(syncKey, cancellationToken).ConfigureAwait(false);

			var entity = await repository.Value.FindByIdAsync(model.Id, true, map, options, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (entity is null)
				return OperationResult<TResultModel>.NotFound("The specified item could not be found.");

			// Check the concurrency stamp. This is done in the repos and again when the database query is executed but good to fail on
			// this as early as possible.
			if (entity is IConcurrencyStamp concurrencyStamp && concurrencyStamp.ConcurrencyStamp != model.ConcurrencyStamp)
				return OperationResult<TResultModel>.Conflict(Options.ConcurrencyErrorMessage);

			if (beforeMappingCallback is not null)
			{
				var result = await beforeMappingCallback(entity).ConfigureAwait(false);

				if (result is not null)
					return result.ToTypedOperationResult<TResultModel>();
			}

			entity = mapperInputCallback is null
					? await Mapper.MapAsync(model, entity, cancellationToken).ConfigureAwait(false)
					: mapperInputCallback(model, entity);

			if (beforeUpdateEntityCallback is not null)
			{
				var beforeResult = await beforeUpdateEntityCallback(entity).ConfigureAwait(false);

				if (beforeResult is not null)
					return beforeResult.ToTypedOperationResult<TResultModel>();
			}

			// Ensure the current user has Update permissions.
			if (enableAuthorizationChecks)
			{
				bool success = await AuthorizationService.AuthorizeAsync(User, entity, Options.UpdatePolicyName).ConfigureAwait(false);

				if (!success)
					return OperationResult<TResultModel>.Forbidden("You do not have permission to update the specified item.");
			}

			IOperationResult<TEntity> saveResult = await repository.Value.SaveEntityAsync(entity, repoOptions: options, childOptions: childOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (Options.DataAccessUnitOfWorkExclusionFilter?.Invoke(entity) is not true)
				await DataAccessUnitOfWork.Value.CommitAsync(cancellationToken).ConfigureAwait(false);

			if (saveResult.Status is OperationResultStatus.GenericSuccess)
			{
				TResultModel? result = default;

				if (enableOutputMapping)
				{
					result = mapperOutputCallback is null
						? await Mapper.MapAsync<TResultModel>(entity, cancellationToken).ConfigureAwait(false)
						: mapperOutputCallback(entity);
				}
				else
				{
					result = new TResultModel();

					if (entity is IConcurrencyStamp concurrencyStampEntity)
						result.ConcurrencyStamp = concurrencyStampEntity.ConcurrencyStamp;
				}

				if (afterUpdateEntityCallback is not null)
					await afterUpdateEntityCallback(entity, result).ConfigureAwait(false);

				return OperationResult<TResultModel>.Success(result);
			}

			return saveResult.ValidationResults?.Count > 0
				? new OperationResult<TResultModel>
				{
					Status = OperationResultStatus.InvalidOperation,
					ValidationResults = saveResult.ValidationResults
				}
				: OperationResult<TResultModel>.InvalidOperation("There was a problem updating the item. Please check the model and try again.");
		}
		catch (UmbrellaConcurrencyException)
		{
			return OperationResult<TResultModel>.Conflict(Options.ConcurrencyErrorMessage);
		}
		catch (Exception exc) when (Options.UpdateExceptionFilter(exc))
		{
			var result = await Options.HandleUpdateExceptionAsync(exc).ConfigureAwait(false);

			if (result is not null)
				return result.ToTypedOperationResult<TResultModel>();

			throw;
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult<TResultModel>.GenericFailure("There has been a problem updating the specified item.");
		}
		finally
		{
			if (syncRoot is not null)
				await syncRoot.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult> DeleteAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
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

		ISynchronizationRoot? syncRoot = null;

		try
		{
			if (synchronizeAccess && id.ToString() is string syncKey)
				syncRoot = await SynchronizationManager.GetSynchronizationRootAndWaitAsync<TEntity>(syncKey, cancellationToken).ConfigureAwait(false);

			var entity = await repository.Value.FindByIdAsync(id, true, map, options, childOptions, cancellationToken).ConfigureAwait(false);

			if (entity is null)
				return OperationResult.NotFound("The specified item could not be found.");

			if (enableAuthorizationChecks)
			{
				bool success = await AuthorizationService.AuthorizeAsync(User, entity, Options.DeletePolicyName).ConfigureAwait(false);

				if (!success)
					return OperationResult.Forbidden("You do not have permission to delete the specified item.");
			}

			IOperationResult? result = await beforeDeleteEntityAsyncCallback(entity, cancellationToken).ConfigureAwait(false);

			if (result is not null)
				return result;

			await repository.Value.DeleteEntityAsync(entity, repoOptions: options, childOptions: childOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

			if (Options.DataAccessUnitOfWorkExclusionFilter?.Invoke(entity) is not true)
				await DataAccessUnitOfWork.Value.CommitAsync(cancellationToken).ConfigureAwait(false);

			await afterDeleteEntityAsyncCallback(entity, cancellationToken).ConfigureAwait(false);

			return OperationResult.NoContent();
		}
		catch (Exception exc) when (Options.DeleteExceptionFilter(exc))
		{
			OperationResult? result = await Options.HandleDeleteExceptionAsync(exc).ConfigureAwait(false);

			if (result is not null)
				return result;

			throw;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult.GenericFailure("There has been a problem deleting the specified item.");
		}
		finally
		{
			if (syncRoot is not null)
				await syncRoot.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult> ExistsByIdAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
		TEntityKey id,
		Lazy<TRepository> repository,
		CancellationToken cancellationToken)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		try
		{
			bool exists = await repository.Value.ExistsByIdAsync(id, cancellationToken).ConfigureAwait(false);

			return exists
				? OperationResult.NoContent()
				: OperationResult.NotFound("The specified item could not be found.");
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult.GenericFailure("An error has occurred whilst trying to get the specified item.");
		}
	}

	/// <inheritdoc />
	public async Task<IOperationResult<int>> TotalCountAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(
		Lazy<TRepository> repository,
		CancellationToken cancellationToken)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(repository);

		try
		{
			int count = await repository.Value.FindTotalCountAsync(cancellationToken).ConfigureAwait(false);

			return OperationResult<int>.Success(count);
		}
		catch (Exception exc) when (Logger.WriteError(exc, returnValue: !HostingEnvironment.IsDevelopment()))
		{
			return OperationResult<int>.GenericFailure("An error has occurred whilst trying to get the specified item.");
		}
	}
}