
namespace Umbrella.Utilities.Data.Concurrency;

/// <summary>
/// Adds support for reading a concurrency token on an item.
/// </summary>
/// <remarks>
/// <para>
/// This is the read-side contract and is the interface that result models and read models should implement.
/// Because it declares no setter, implementations are free to use <see langword="init"/> accessors, which
/// permits the house-standard <c>public required string ConcurrencyStamp { get; init; }</c> declaration.
/// </para>
/// <para>
/// Entities, and request models that need to be mutated after construction, should implement
/// <see cref="IConcurrencyStamp"/> instead. That interface extends this one and re-declares the property
/// with a setter.
/// </para>
/// </remarks>
public interface IReadOnlyConcurrencyStamp
{
	/// <summary>
	/// Gets the concurrency stamp.
	/// </summary>
	string ConcurrencyStamp { get; }
}
