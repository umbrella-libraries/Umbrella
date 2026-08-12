namespace Umbrella.Utilities.Data.Models;

/// <summary>
/// A result model of the operation to create an item.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface ICreateResultModel<TKey>
	where TKey : IEquatable<TKey>
{
	/// <summary>
	/// Gets the identifier.
	/// </summary>
	/// <remarks>
	/// This is read-only because result models are always populated by a mapper. Implementations should declare
	/// the property as <c>public required TKey Id { get; init; }</c>.
	/// </remarks>
	TKey Id { get; }
}