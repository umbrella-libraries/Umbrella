using Umbrella.Utilities.Data.Concurrency;

namespace Umbrella.Utilities.Data.Models;

/// <summary>
/// A result model of the operation to update an item.
/// </summary>
/// <remarks>
/// The concurrency stamp is exposed using the read-only <see cref="IReadOnlyConcurrencyStamp"/> contract because
/// result models are always populated by a mapper. Implementations should declare the property as
/// <c>public required string ConcurrencyStamp { get; init; }</c>.
/// </remarks>
/// <seealso cref="IReadOnlyConcurrencyStamp" />
public interface IUpdateResultModel : IReadOnlyConcurrencyStamp
{
	// TODO: Consider adding an Id property for convenience.
}
