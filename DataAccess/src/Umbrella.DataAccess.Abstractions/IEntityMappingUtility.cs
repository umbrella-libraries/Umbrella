


using Umbrella.Utilities.Mapping.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A utility class used to update a collection of existing items using an incoming items collection.
/// </summary>
public interface IEntityMappingUtility
{
	/// <summary>
	/// Used to map a collection of models onto a collection of existing entities. This leverages object mapping internally which means
	/// that the appropriate mappings must be registered with the singleton instance of <see cref="IUmbrellaMapper" /> registered with DI.
	/// </summary>
	/// <typeparam name="TModel">The type of the model items.</typeparam>
	/// <typeparam name="TEntity">The type of the entity items.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <param name="modelItems">The model items.</param>
	/// <param name="existingItems">The existing items.</param>
	/// <param name="matchSelector">The selector used to match items, e.g. the id.</param>
	/// <param name="newEntityAction">The action to be applied to new entity items that do not exist in the existing items.</param>
	/// <param name="deletedEntityAction">The action to be applied to deleted items that do not exist in the model items.</param>
	/// <param name="autoInclusionSelector">The automatic inclusion selector.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="innerActions">The inner actions to be applied to nested objects. Nested objects need to be explicitly handled.</param>
	/// <returns>A collection of <typeparamref name="TEntity"/> instances.</returns>
	ValueTask<List<TEntity>> UpdateItemsListAsync<TModel, TEntity, TEntityKey>(IEnumerable<TModel> modelItems, IEnumerable<TEntity> existingItems, Func<TModel, TEntity, bool> matchSelector, Func<TEntity, ValueTask>? newEntityAction = null, Func<TEntity, List<TEntity>, ValueTask>? deletedEntityAction = null, Func<TEntity, bool>? autoInclusionSelector = null, CancellationToken cancellationToken = default, params Func<TModel, TEntity, ValueTask>[] innerActions)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>;
}