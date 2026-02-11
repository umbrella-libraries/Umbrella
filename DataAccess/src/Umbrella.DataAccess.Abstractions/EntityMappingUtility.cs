


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions.Exceptions;
using Umbrella.Utilities.Mapping.Abstractions;

namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A utility class used to update a collection of existing items using an incoming items collection. This implementation leverages object mapping internally using
/// a constructor injected instance of <see cref="IUmbrellaMapper"/>.
/// </summary>
/// <seealso cref="IEntityMappingUtility" />
public class EntityMappingUtility : IEntityMappingUtility
{
	private readonly ILogger _log;
	private readonly Lazy<IUmbrellaMapper> _mapper;

	/// <summary>
	/// Initializes a new instance of the <see cref="EntityMappingUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="mapper">The mapper.</param>
	public EntityMappingUtility(
		ILogger<EntityMappingUtility> logger,
		Lazy<IUmbrellaMapper> mapper)
	{
		_log = logger;
		_mapper = mapper;
	}

	/// <inheritdoc />
	public async ValueTask<List<TEntity>> UpdateItemsListAsync<TModel, TEntity, TEntityKey>(
		IEnumerable<TModel> modelItems,
		IEnumerable<TEntity> existingItems,
		Func<TModel, TEntity, bool> matchSelector,
		Func<TEntity, ValueTask>? newEntityAction = null,
		Func<TEntity, List<TEntity>, ValueTask>? deletedEntityAction = null,
		Func<TEntity, bool>? autoInclusionSelector = null,
		CancellationToken cancellationToken = default,
		params Func<TModel, TEntity, ValueTask>[] innerActions)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(existingItems);

		try
		{
			// If there is nothing to process, just return an empty list for the target entity type
			if (modelItems is null)
				return [];

			var updatedList = new List<TEntity>();

			// Auto include entity items that match the autoIncludeSelector if it isn't null
			if (autoInclusionSelector is not null)
				updatedList.AddRange(existingItems.Where(autoInclusionSelector));

			foreach (var item in modelItems)
			{
				if (item is null)
					throw new InvalidOperationException("The item is null.");

				TEntity? entity = existingItems.SingleOrDefault(x => matchSelector(item, x));

				// No existing item with this id, so add a new one
				if (entity is null)
				{
					entity = await _mapper.Value.MapAsync<TEntity>(item, cancellationToken).ConfigureAwait(false);

					// Make sure the mapped entity has an default id value to avoid errors with having mapped an existing item that belongs
					// to something like a different foreign key relationship
					entity.Id = default!;

					if (newEntityAction is not null)
						await newEntityAction(entity).ConfigureAwait(false);

					updatedList.Add(entity);
				}
				else // Existing item found, so map to existing instance
				{
					_ = await _mapper.Value.MapAsync(item, entity, cancellationToken).ConfigureAwait(false);
					updatedList.Add(entity);
				}

				if (innerActions?.Length > 0)
				{
					foreach (var action in innerActions)
					{
						await action(item, entity).ConfigureAwait(false);
					}
				}
			}

			// Process the deleted items
			if (deletedEntityAction is not null)
			{
				foreach (var item in existingItems.Except(updatedList))
				{
					await deletedEntityAction(item, updatedList).ConfigureAwait(false);
				}
			}

			return updatedList;
		}
		catch (Exception exc) when (_log.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There has been a problem updating the target items list.", exc);
		}
	}
}