namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// An equality comparer used to compare entities for equality based on their Id.
/// </summary>
/// <typeparam name="TEntityKey">The type of the entity Id.</typeparam>
public class IEntityKeyComparer<TEntityKey> : IEqualityComparer<IEntity<TEntityKey>>
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Gets a singleton instance of this comparer.
	/// </summary>
	public static IEntityKeyComparer<TEntityKey> Instance { get; } = new IEntityKeyComparer<TEntityKey>();

	/// <inheritdoc />
	public bool Equals(IEntity<TEntityKey>? x, IEntity<TEntityKey>? y) => x is null && y is null || (x is not null and not 0 && y is not null and not 0 && x.Id.Equals(y.Id));

	/// <inheritdoc />
	public int GetHashCode(IEntity<TEntityKey> obj)
	{
		if (obj is null)
			throw new ArgumentNullException(nameof(obj));

		return obj.Id.GetHashCode();
	}
}
