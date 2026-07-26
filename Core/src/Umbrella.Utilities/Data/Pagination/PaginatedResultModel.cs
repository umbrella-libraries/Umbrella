namespace Umbrella.Utilities.Data.Pagination;

/// <summary>
/// Represents the result of a paginated query.
/// </summary>
/// <remarks>
/// The pagination state exposed by this record must be initialized during construction, by using an object initializer, or by using a <c>with</c> expression.
/// Types that derive from this type must also be records.
/// </remarks>
/// <typeparam name="TItem">The type of the item.</typeparam>
public record PaginatedResultModel<TItem>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="PaginatedResultModel{TItem}"/> class.
	/// </summary>
	public PaginatedResultModel()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PaginatedResultModel{TItem}"/> class.
	/// </summary>
	/// <param name="items">The items.</param>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="totalCount">The total count.</param>
	public PaginatedResultModel(IReadOnlyCollection<TItem> items, int pageNumber, int pageSize, int totalCount)
	{
		Items = items;
		PageNumber = pageNumber;
		PageSize = pageSize;
		TotalCount = totalCount;
	}

	/// <summary>
	/// Gets the items.
	/// </summary>
	public IReadOnlyCollection<TItem> Items { get; init; } = Array.Empty<TItem>();

	/// <summary>
	/// Gets the page number.
	/// </summary>
	public int PageNumber { get; init; }

	/// <summary>
	/// Gets the size of the page.
	/// </summary>
	public int PageSize { get; init; }

	/// <summary>
	/// Gets the total count.
	/// </summary>
	public int TotalCount { get; init; }

	/// <summary>
	/// Gets a value indicating whether there are more items that can be retrieved on subsequent pages.
	/// </summary>
	public bool MoreItems => PageNumber * PageSize < TotalCount;
}
