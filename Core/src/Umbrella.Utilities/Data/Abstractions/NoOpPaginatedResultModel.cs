using Umbrella.Utilities.Data.Pagination;

namespace Umbrella.Utilities.Data.Abstractions;

/// <summary>
/// A <see cref="PaginatedResultModel{TItem}"/> implementation that doesn't do anything and whose items are of
/// type <see cref="NoOpKeyedItem{TIdentifier}" />.
/// </summary>
/// <typeparam name="TIdentifier">The type of the item identifier.</typeparam>
/// <seealso cref="PaginatedResultModel{TItem}" />
public record NoOpPaginatedResultModel<TIdentifier> : PaginatedResultModel<NoOpKeyedItem<TIdentifier>>
	where TIdentifier : IEquatable<TIdentifier>
{
}
