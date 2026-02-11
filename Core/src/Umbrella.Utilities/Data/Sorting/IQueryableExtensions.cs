
using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Data.Sorting;

/// <summary>
/// Extensions for <see cref="IQueryable{T}"/> collections for sorting.
/// </summary>
public static class IQueryableExtensions
{
	/// <summary>
	/// Applies the sort expressions to the specified query.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="items">The items.</param>
	/// <param name="sortExpressions">The sort expressions.</param>
	/// <param name="defaultSortOrderExpression">The default sort order expression when <paramref name="sortExpressions"/> is null or empty.</param>
	/// <returns>The query with the sort expressions applied to it.</returns>
	public static IQueryable<TItem> ApplySortExpressions<TItem>(this IQueryable<TItem> items, IEnumerable<SortExpression<TItem>>? sortExpressions, in SortExpression<TItem> defaultSortOrderExpression)
	{
		IOrderedQueryable<TItem>? orderedQuery = null;

		if (sortExpressions?.Count() > 0)
		{
			int i = 0;

			foreach (SortExpression<TItem> sortExpression in sortExpressions)
			{
				var expression = sortExpression.Expression ?? throw new InvalidOperationException("The expression cannot be found.");

				orderedQuery = i++ == 0
					? sortExpression.Direction == SortDirection.Ascending
						? items.OrderBy(expression)
						: items.OrderByDescending(expression)
					: sortExpression.Direction == SortDirection.Ascending
						? orderedQuery!.ThenBy(expression)
						: orderedQuery!.ThenByDescending(expression);
			}
		}
		else
		{
			var expression = defaultSortOrderExpression.Expression ?? throw new InvalidOperationException("The expression for the defaultSortOrderExpression cannot be found.");

			orderedQuery = defaultSortOrderExpression.Direction == SortDirection.Ascending
						? items.OrderBy(expression)
						: items.OrderByDescending(expression);
		}

		return orderedQuery ?? items;
	}

	/// <summary>
	/// Orders the query by sort direction.
	/// </summary>
	/// <typeparam name="TSource">The type of the source.</typeparam>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <param name="source">The source.</param>
	/// <param name="keySelector">The key selector.</param>
	/// <param name="direction">The direction.</param>
	/// <param name="comparer">The comparer.</param>
	/// <returns>The ordered query.</returns>
	public static IOrderedQueryable<TSource> OrderBySortDirection<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, SortDirection direction, IComparer<TKey>? comparer = null)
	{
		Guard.IsNotNull(source, nameof(source));
		Guard.IsNotNull(keySelector, nameof(keySelector));

		return direction switch
		{
			SortDirection.Descending => comparer is null ? source.OrderByDescending(keySelector) : source.OrderByDescending(keySelector, comparer),
			_ => comparer is null ? source.OrderBy(keySelector) : source.OrderBy(keySelector, comparer),
		};
	}
}