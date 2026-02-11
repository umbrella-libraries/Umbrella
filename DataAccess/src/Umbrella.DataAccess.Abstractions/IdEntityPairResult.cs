
namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// A record that encapsulates an Id / Entity pair.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
public record IdEntityPairResult<TEntity, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Gets the identifier.
	/// </summary>
	public required TEntityKey Id { get; init; }

	/// <summary>
	/// Gets the entity.
	/// </summary>
	public TEntity? Entity { get; init; }
}