


using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Models;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A service that provides data access operations for entities in a repository.
/// </summary>
public interface IUmbrellaRepositoryCoreDataService
{
	/// <summary>
	/// Used to create a new <typeparamref name="TEntity"/> in the repository based on the provided <typeparamref name="TModel"/> which returns
	/// a <typeparamref name="TResultModel"/> if successful.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Synchronize execution of this method if enabled using <paramref name="synchronizeAccess"/>.</item>
	/// <item>Invokes the <paramref name="beforeMappingCallback"/> to allow execution of custom code before performing mapping.</item>
	/// <item>Maps the <typeparamref name="TModel"/> to a new instance of <typeparamref name="TEntity"/> using the <paramref name="mapperInputCallback"/> falling back to use the <see cref="IUmbrellaMapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="beforeCreateEntityCallback"/> to augment the newly created <typeparamref name="TEntity"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Saves the new entity to the <typeparamref name="TRepository"/>.</item>
	/// <item>
	/// Creates the <typeparamref name="TResultModel"/> by mapping the entity, if <paramref name="enableOutputMapping"/> is <see langword="true"/>, using the <paramref name="mapperOutputCallback"/> if specified falling back to using the <see cref="IUmbrellaMapper"/>.
	/// If <paramref name="enableOutputMapping"/> is <see langword="true"/>, a new instance of <typeparamref name="TResultModel"/> and only assigning the <c>Id</c> and <c>ConcurrencyStamp</c> properties.
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
	/// <param name="beforeMappingCallback">The optional callback invoked before mapping.</param>
	/// <param name="mapperInputCallback">The mapper input callback.</param>
	/// <param name="beforeCreateEntityCallback">The before create entity callback.</param>
	/// <param name="mapperOutputCallback">The mapper output callback.</param>
	/// <param name="afterCreateEntityCallback">The after create entity callback.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities persisted to the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using a key generated using the <paramref name="synchronizationRootKeyCreator"/> parameter.</param>
	/// <param name="synchronizationRootKeyCreator">The synchronization root key creator used to create a key for the <see cref="ISynchronizationManager"/> to synchronize access to this method.</param>
	/// <param name="enableOutputMapping">
	/// Specifies whether the newly created <typeparamref name="TEntity"/> is mapped to an instance of <typeparamref name="TResultModel"/> using an instance of <see cref="IUmbrellaMapper"/>,
	/// or if this is done by this method internally by creating a new instance of <typeparamref name="TResultModel"/> and only assigning the <c>Id</c> and <c>ConcurrencyStamp</c> properties.
	/// Please leave this set to <see langword="true"/> use a mapping implementation for a richer experience.
	/// </param>
	/// <returns>The operation result</returns>
	Task<IOperationResult<TResultModel?>> CreateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(TModel model, Lazy<TRepository> repository, CancellationToken cancellationToken, Func<Task<IOperationResult?>>? beforeMappingCallback = null, Func<TModel, TEntity>? mapperInputCallback = null, Func<TEntity, Task<IOperationResult?>>? beforeCreateEntityCallback = null, Func<TEntity, TResultModel>? mapperOutputCallback = null, Func<TEntity, TResultModel, Task>? afterCreateEntityCallback = null, TRepositoryOptions? options = null, IEnumerable<RepoOptions>? childOptions = null, bool enableAuthorizationChecks = true, bool synchronizeAccess = false, Func<object, (Type type, string key)?>? synchronizationRootKeyCreator = null, bool enableOutputMapping = true)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TResultModel : ICreateResultModel<TEntityKey>, new();

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
	/// <returns>The operation result</returns>
	Task<IOperationResult> DeleteAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(TEntityKey id, Lazy<TRepository> repository, Func<TEntity, CancellationToken, Task<IOperationResult?>> beforeDeleteEntityAsyncCallback, Func<TEntity, CancellationToken, Task> afterDeleteEntityAsyncCallback, CancellationToken cancellationToken, IncludeMap<TEntity>? map = null, TRepositoryOptions? options = null, IEnumerable<RepoOptions>? childOptions = null, bool enableAuthorizationChecks = true, bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new();

	/// <summary>
	/// Asynchronously determines whether an entity with the specified identifier exists in the repository.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity to check for existence.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity's unique identifier.</typeparam>
	/// <typeparam name="TRepository">The type of the repository used to access entities.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of options used to configure the repository.</typeparam>
	/// <param name="id">The unique identifier of the entity to check for existence.</param>
	/// <param name="repository">A lazily initialized repository instance used to perform the existence check. Cannot be null.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IOperationResult" /> indicating whether
	/// the entity exists.</returns>
	Task<IOperationResult> ExistsByIdAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(TEntityKey id, Lazy<TRepository> repository, CancellationToken cancellationToken)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new();

	/// <summary>
	/// Used to load paginated entities in bulk from the repository based on the specified <paramref name="sorters"/> and <paramref name="filters"/>
	/// with each result mapped to a collection of <typeparamref name="TItemModel"/> wrapped in a <typeparamref name="TPaginatedResultModel"/>.
	/// </summary>
	/// <remarks>
	/// The lifecycle of this method internally is as follows:
	/// <list type="number">
	/// <item>Invokes the <paramref name="loadReadAllDataAsyncDelegate"/> to load all <typeparamref name="TEntityResult"/> instances from the repository.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on all loaded entities.</item>
	/// <item>Creates the <typeparamref name="TPaginatedResultModel"/> and maps the <typeparamref name="TEntityResult"/> instances to <typeparamref name="TItemModel"/> instances using the the <paramref name="mapReadAllEntitiesDelegate"/>, falling back to using the <see cref="IUmbrellaMapper"/> if not specified.</item>
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
	/// <param name="mapReadAllEntitiesDelegate">The delegate used to map the results loaded from the repository. If not specified, the <see cref="IUmbrellaMapper"/> is used.</param>
	/// <param name="afterCreateSearchSlimPaginatedModelAsyncDelegate">The delegate that is invoked after the paginated result model has been created.</param>
	/// <param name="afterCreateSlimModelAsyncDelegate">The delegate that is invoked when result models have been created.</param>
	/// <param name="options">The repository options.</param>
	/// <param name="childOptions">The child repository options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities loaded from the repository.</param>
	/// <returns>The operation result</returns>
	Task<IOperationResult<TPaginatedResultModel?>> ReadAllAsync<TEntityResult, TEntity, TEntityKey, TRepositoryOptions, TItemModel, TPaginatedResultModel>(int pageNumber, int pageSize, SortExpression<TEntityResult>[]? sorters, FilterExpression<TEntity>[]? filters, FilterExpressionCombinator? filterCombinator, Func<int, int, SortExpression<TEntityResult>[]?, FilterExpression<TEntity>[]?, FilterExpressionCombinator?, TRepositoryOptions?, IEnumerable<RepoOptions>?, CancellationToken, Task<PaginatedResultModel<TEntityResult>>> loadReadAllDataAsyncDelegate, CancellationToken cancellationToken, Func<IReadOnlyCollection<TEntityResult>, TItemModel[]>? mapReadAllEntitiesDelegate = null, Func<PaginatedResultModel<TEntityResult>, TPaginatedResultModel, SortExpression<TEntityResult>[]?, FilterExpression<TEntity>[]?, FilterExpressionCombinator?, CancellationToken, Task>? afterCreateSearchSlimPaginatedModelAsyncDelegate = null, Func<TEntityResult, TItemModel, CancellationToken, Task<IOperationResult?>>? afterCreateSlimModelAsyncDelegate = null, TRepositoryOptions? options = null, IEnumerable<RepoOptions>? childOptions = null, bool enableAuthorizationChecks = true)
		where TEntityResult : class, IEntity<TEntityKey>
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TPaginatedResultModel : PaginatedResultModel<TItemModel>, new();

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
	/// <item>Maps the <typeparamref name="TEntity"/> to the <typeparamref name="TModel"/> using the <paramref name="mapperCallback"/> falling back to use the <see cref="IUmbrellaMapper"/> if not specified.</item>
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
	/// <param name="mapperCallback">The mapper callback. If not specified, <see cref="IUmbrellaMapper"/> is used to perform the mapping.</param>
	/// <param name="afterReadEntityCallback">The after read entity callback.</param>
	/// <param name="trackChanges">if set to <see langword="true"/> enable change tracking on the database context.</param>
	/// <param name="map">The map.</param>
	/// <param name="options">The options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="enableAuthorizationChecks">Specifies whether imperative authorization checks are performed on entities loaded from the repository.</param>
	/// <param name="synchronizeAccess">Specifies whether exclusive access should be enabled using code that synchronizes using the <paramref name="id"/> and type name of the entity.</param>
	/// <returns>The operation result</returns>
	Task<IOperationResult<TModel?>> ReadAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel>(TEntityKey id, Lazy<TRepository> repository, CancellationToken cancellationToken, Func<TEntityKey, bool, IncludeMap<TEntity>?, TRepositoryOptions?, IEnumerable<RepoOptions>?, CancellationToken, Task<TEntity?>>? loadReadEntityAsyncDelegate = null, Func<TEntity, TModel>? mapperCallback = null, Func<TEntity, TModel, Task<IOperationResult?>>? afterReadEntityCallback = null, bool trackChanges = false, IncludeMap<TEntity>? map = null, TRepositoryOptions? options = null, IEnumerable<RepoOptions>? childOptions = null, bool enableAuthorizationChecks = true, bool synchronizeAccess = false)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new();

	/// <summary>
	/// Asynchronously retrieves the total number of entities in the repository.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity to count.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity's key.</typeparam>
	/// <typeparam name="TRepository">The type of the repository used to access entities.</typeparam>
	/// <typeparam name="TRepositoryOptions">The type of options used to configure the repository.</typeparam>
	/// <param name="repository">A lazily initialized repository instance used to access the entities.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains an <see cref="IOperationResult" /> with the total
	/// count of entities.
	/// </returns>
	Task<IOperationResult<int>> TotalCountAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions>(Lazy<TRepository> repository, CancellationToken cancellationToken)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new();

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
	/// <item>Maps the <typeparamref name="TModel"/> to an existing instance of <typeparamref name="TEntity"/> using the <paramref name="mapperInputCallback"/> falling back to use the <see cref="IUmbrellaMapper"/> if not specified.</item>
	/// <item>Invokes the <paramref name="beforeUpdateEntityCallback"/> to augment the updated <typeparamref name="TEntity"/>.</item>
	/// <item>Perform authorization, if enabled via the <paramref name="enableAuthorizationChecks"/> property, on the entity.</item>
	/// <item>Saves the updated entity to the <typeparamref name="TRepository"/>.</item>
	/// <item>
	/// Creates the <typeparamref name="TResultModel"/> by mapping the entity, if <paramref name="enableOutputMapping"/> is <see langword="true"/>, using the <paramref name="mapperOutputCallback"/> if specified falling back to using the <see cref="IUmbrellaMapper"/>.
	/// If <paramref name="enableOutputMapping"/> is <see langword="true"/>, a new instance of <typeparamref name="TResultModel"/> and only assigning the <c>Id</c> and <c>ConcurrencyStamp</c> properties.
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
	/// <param name="enableOutputMapping">
	/// Specifies whether the newly created <typeparamref name="TEntity"/> is mapped to an instance of <typeparamref name="TResultModel"/> using the <see cref="IUmbrellaMapper"/>,
	/// or if this is done by this method by creating a new instance of <typeparamref name="TResultModel"/> and only assigning the <c>ConcurrencyStamp</c> property.
	/// Please leave this set to <see langword="true"/> use a mapping implementation for a richer experience.
	/// </param>
	/// <returns>The operation result</returns>
	Task<IOperationResult<TResultModel?>> UpdateAsync<TEntity, TEntityKey, TRepository, TRepositoryOptions, TModel, TResultModel>(TModel model, Lazy<TRepository> repository, CancellationToken cancellationToken, Func<TEntity, Task<IOperationResult?>>? beforeMappingCallback = null, Func<TModel, TEntity, TEntity>? mapperInputCallback = null, Func<TEntity, Task<IOperationResult?>>? beforeUpdateEntityCallback = null, Func<TEntity, TResultModel>? mapperOutputCallback = null, Func<TEntity, TResultModel, Task>? afterUpdateEntityCallback = null, IncludeMap<TEntity>? map = null, TRepositoryOptions? options = null, IEnumerable<RepoOptions>? childOptions = null, bool enableAuthorizationChecks = true, bool synchronizeAccess = false, bool enableOutputMapping = true)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
		where TRepository : class, IGenericDbRepository<TEntity, TRepositoryOptions, TEntityKey>
		where TRepositoryOptions : RepoOptions, new()
		where TModel : IUpdateModel<TEntityKey>
		where TResultModel : IUpdateResultModel, new();
}