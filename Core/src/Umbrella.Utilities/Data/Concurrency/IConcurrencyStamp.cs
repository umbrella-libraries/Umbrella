
namespace Umbrella.Utilities.Data.Concurrency;

/// <summary>
/// Adds supports for storing a concurrency token on an item.
/// </summary>
/// <remarks>
/// This is the mutable contract and is intended for entities, and for request models that need the stamp to be
/// assigned after construction. Result models and read models should implement
/// <see cref="IReadOnlyConcurrencyStamp"/> instead so they can declare the property using an
/// <see langword="init"/> accessor.
/// </remarks>
/// <seealso cref="IReadOnlyConcurrencyStamp" />
public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp
{
	/// <summary>
	/// Gets or sets the concurrency stamp.
	/// </summary>
	new string ConcurrencyStamp { get; set; }
}
