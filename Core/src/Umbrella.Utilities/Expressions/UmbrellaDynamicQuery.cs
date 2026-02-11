


using System.Linq.Expressions;
using CommunityToolkit.Diagnostics;

namespace Umbrella.Utilities.Expressions;

/// <summary>
/// Helps building dynamic queries.
/// </summary>
public static class UmbrellaDynamicQuery
{
	/// <summary>
	/// Create a dynamic predicate for a given property selector, comparison method and reference value.
	/// </summary>
	/// <typeparam name="T">The type of the query data.</typeparam>
	/// <param name="selector">The property selector to parse.</param>
	/// <param name="comparer">The comparison method to use.</param>
	/// <param name="value">The reference value to compare with.</param>
	/// <param name="provider">The culture-specific formatting information.</param>
	/// <returns>The dynamic predicate.</returns>
	public static Expression<Func<T, bool>> CreatePredicate<T>(string selector, UmbrellaDynamicCompare comparer, string value, IFormatProvider? provider = null)
	{
		Guard.IsNotNullOrWhiteSpace(selector, nameof(selector));

		if (!Enum.IsDefined(typeof(UmbrellaDynamicCompare), comparer))
			throw new ArgumentOutOfRangeException(nameof(comparer));

		var target = Expression.Parameter(typeof(T));

		return Expression.Lambda<Func<T, bool>>(UmbrellaDynamicExpression.CreateComparison(target, selector, comparer, value, provider), target);
	}

	/// <summary>
	/// Create a dynamic predicate for a given property selector, comparison method and reference value.
	/// </summary>
	/// <typeparam name="T">The type of the query data.</typeparam>
	/// <param name="selector">The property selector to parse.</param>
	/// <param name="comparer">The comparison method to use.</param>
	/// <param name="value">The reference value to compare with.</param>
	/// <param name="provider">The culture-specific formatting information.</param>
	/// <returns>The dynamic predicate.</returns>
	public static Expression<Func<T, bool>> CreatePredicate<T>(string selector, string comparer, string value, IFormatProvider? provider = null)
	{
		Guard.IsNotNullOrWhiteSpace(selector, nameof(selector));
		Guard.IsNotNullOrWhiteSpace(comparer, nameof(comparer));

		var target = Expression.Parameter(typeof(T));

		return Expression.Lambda<Func<T, bool>>(UmbrellaDynamicExpression.CreateComparison(target, selector, comparer, value, provider), target);
	}
}