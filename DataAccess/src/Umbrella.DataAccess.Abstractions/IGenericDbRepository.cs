


using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// Encapsulates CRUD operations for a database repository.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <seealso cref="F:Umbrella.DataAccess.Abstractions.IGenericDbRepository{TEntity, Umbrella.DataAccess.Abstractions.RepoOptions}" />
public interface IGenericDbRepository<TEntity> : IGenericDbRepository<TEntity, RepoOptions>
	where TEntity : class, IEntity<int>
{
}

/// <summary>
/// Encapsulates CRUD operations for a database repository.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <seealso cref="F:Umbrella.DataAccess.Abstractions.IGenericDbRepository{TEntity, Umbrella.DataAccess.Abstractions.RepoOptions}" />
public interface IGenericDbRepository<TEntity, in TRepoOptions> : IGenericDbRepository<TEntity, TRepoOptions, int>
	where TEntity : class, IEntity<int>
	where TRepoOptions : RepoOptions, new()
{
}

/// <summary>
/// Encapsulates CRUD operations for a database repository.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
/// <seealso cref="F:Umbrella.DataAccess.Abstractions.IGenericDbRepository{TEntity, Umbrella.DataAccess.Abstractions.RepoOptions}" />
public interface IGenericDbRepository<TEntity, in TRepoOptions, TEntityKey> : IReadOnlyGenericDbRepository<TEntity, TRepoOptions, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TRepoOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Removes the empty entities from the <paramref name="entities"/> collection. This collection will be mutated by this method.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	Task RemoveEmptyEntitiesAsync(ICollection<TEntity> entities, CancellationToken cancellationToken, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null);

	/// <summary>
	/// Saves the entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="pushChangesToDb">if set to <c>true</c> pushes the changes to the database.</param>
	/// <param name="addToContext">if set to <c>true</c> adds the entity to the database context.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="forceAdd">if set to <c>true</c> forces the entity to be added to the database. This is useful when the repository cannot determine if the item is new or not.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the save operation.</returns>
	[Obsolete($"Use the {nameof(SaveEntityAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	Task<IOperationResult<TEntity>> SaveAsync(TEntity entity, bool pushChangesToDb = true, bool addToContext = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, bool forceAdd = false, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves the entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="addToContext">if set to <c>true</c> adds the entity to the database context.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="forceAdd">if set to <c>true</c> forces the entity to be added to the database. This is useful when the repository cannot determine if the item is new or not.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the save operation.</returns>
	/// <remarks>
	/// If changes need to be pushed to the database, the <see cref="IDataAccessUnitOfWork.CommitAsync"/> method should be called afterwards.
	/// </remarks>
	Task<IOperationResult<TEntity>> SaveEntityAsync(TEntity entity, bool addToContext = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, bool forceAdd = false, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves all entities in the specified collection.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="pushChangesToDb">if set to <c>true</c> pushes the changes to the database.</param>
	/// <param name="bypassSaveLogic">if set to <c>true</c> bypasses the default save logic.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the save operation.</returns>
	[Obsolete($"Use the {nameof(SaveAllEntitiesAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	Task<IReadOnlyCollection<IOperationResult<TEntity>>> SaveAllAsync(IEnumerable<TEntity> entities, bool pushChangesToDb = true, bool bypassSaveLogic = false, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Saves all entities in the specified collection.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="bypassSaveLogic">if set to <c>true</c> bypasses the default save logic.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the save operation.</returns>
	/// <remarks>
	/// If changes need to be pushed to the database, the <see cref="IDataAccessUnitOfWork.CommitAsync"/> method should be called afterwards.
	/// </remarks>
	Task<IReadOnlyCollection<IOperationResult<TEntity>>> SaveAllEntitiesAsync(IEnumerable<TEntity> entities, bool bypassSaveLogic = false, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="pushChangesToDb">if set to <c>true</c> pushes the changes to the database.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	[Obsolete($"Use the {nameof(DeleteEntityAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	Task DeleteAsync(TEntity entity, bool pushChangesToDb = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes the entity.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	/// <remarks>
	/// If changes need to be pushed to the database, the <see cref="IDataAccessUnitOfWork.CommitAsync"/> method should be called afterwards.
	/// </remarks>
	Task DeleteEntityAsync(TEntity entity, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all of the entities in the specified collection.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="pushChangesToDb">if set to <c>true</c> pushes the changes to the database.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	[Obsolete($"Use the {nameof(DeleteAllEntitiesAsync)} method instead together with the ${nameof(IDataAccessUnitOfWork)} type. This method will be removed in a future version.")]
	Task DeleteAllAsync(IEnumerable<TEntity> entities, bool pushChangesToDb = true, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all of the entities in the specified collection.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	/// <remarks>
	/// If changes need to be pushed to the database, the <see cref="IDataAccessUnitOfWork.CommitAsync"/> method should be called afterwards.
	/// </remarks>
	Task DeleteAllEntitiesAsync(IEnumerable<TEntity> entities, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default);

#if NET9_0_OR_GREATER
	/// <summary>
	/// Deletes the entity with the specified <paramref name="id"/>
	/// </summary>
	/// <param name="id">The id</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	/// <remarks>The entity will be removed from the database immediately without <see cref="IDataAccessUnitOfWork.CommitAsync"/> needing to be called.</remarks>
	Task DeleteEntityByIdAsync(TEntityKey id, CancellationToken cancellationToken = default);
#endif
}