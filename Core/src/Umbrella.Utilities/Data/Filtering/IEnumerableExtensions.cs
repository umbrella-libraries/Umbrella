
using System.Globalization;
using System.Linq.Expressions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.TypeConverters;

namespace Umbrella.Utilities.Data.Filtering;

/// <summary>
/// Extensions for <see cref="IEnumerable{T}"/> collections for filtering.
/// </summary>
public static class IEnumerableExtensions
{
	/// <summary>
	/// Applies the filter expressions to the collection.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="items">The items.</param>
	/// <param name="filterExpressions">The filter expressions.</param>
	/// <param name="combinator">The filter combinator.</param>
	/// <param name="useCurrentCulture">if set to <c>true</c>, uses the current culture rules for any culture based comparisons, e.g. strings.</param>
	/// <returns>The filtered collection.</returns>
	public static IEnumerable<TItem> ApplyFilterExpressions<TItem>(this IEnumerable<TItem> items, IEnumerable<FilterExpression<TItem>>? filterExpressions, FilterExpressionCombinator combinator, bool useCurrentCulture = true)
	{
		IEnumerable<TItem>? filteredItems = null;

		if (filterExpressions?.Count() > 0)
		{
			var lstFilterPredicate = new List<Func<TItem, bool>>();
			var lstPrimaryFilterPredicate = new List<Func<TItem, bool>>();

			foreach (var filterExpression in filterExpressions)
			{
				bool Predicate(TItem x)
				{
					object? filterValue = filterExpression.Value;
					object? propertyValue = filterExpression.GetDelegate()?.Invoke(x);

					if (propertyValue is string strPropertyValue && filterValue is string strFilterValue)
					{
						StringComparison comparison = useCurrentCulture ? StringComparison.CurrentCultureIgnoreCase : StringComparison.InvariantCultureIgnoreCase;

						return filterExpression.Type switch
						{
							FilterType.Equal => strPropertyValue.Equals(strFilterValue, comparison),
							FilterType.NotEqual => !strPropertyValue.Equals(strFilterValue, comparison),
							FilterType.StartsWith => strPropertyValue.StartsWith(strFilterValue, comparison),
							FilterType.EndsWith => strPropertyValue.EndsWith(strFilterValue, comparison),
							FilterType.Contains => strPropertyValue.IndexOf(strFilterValue, comparison) > -1,
							_ => false
						};
					}

					return propertyValue?.Equals(filterValue) ?? false;
				}

				if (filterExpression.IsPrimary)
					lstPrimaryFilterPredicate.Add(Predicate);
				else
					lstFilterPredicate.Add(Predicate);
			}

			filteredItems = items;

			lstPrimaryFilterPredicate.ForEach(x => filteredItems = filteredItems.Where(y => x(y)));

			if (combinator == FilterExpressionCombinator.And)
			{
				lstFilterPredicate.ForEach(x => filteredItems = filteredItems.Where(y => x(y)));
			}
			else if (combinator == FilterExpressionCombinator.Or)
			{
				var lstItem = new HashSet<TItem>();
				lstFilterPredicate.ForEach(x => lstItem.UnionWith(filteredItems.Where(y => x(y))));

				filteredItems = lstItem;
			}
		}

		return filteredItems ?? items;
	}

	/// <summary>
	/// Converts a collection of <see cref="FilterExpression{TItem}"/> to a collection of <see cref="FilterExpressionDescriptor"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="filterExpressions">The filter expressions.</param>
	/// <returns>A <see cref="IEnumerable{FilterExpressionDescriptor}"/> collection.</returns>
	public static IEnumerable<FilterExpressionDescriptor> ToFilterExpressionDescriptors<TItem>(this IEnumerable<FilterExpression<TItem>> filterExpressions)
		=> filterExpressions.Select(x => (FilterExpressionDescriptor?)x).OfType<FilterExpressionDescriptor>();

	/// <summary>
	/// Finds a filter with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <returns>The filter, if it exists.</returns>
	public static FilterExpression<TItem>? FindByMemberPath<TItem>(this IEnumerable<FilterExpression<TItem>>? filters, string memberPath)
	{
		FilterExpression<TItem>? result = filters?.SingleOrDefault(x => x.MemberPath?.Equals(memberPath, StringComparison.OrdinalIgnoreCase) ?? false);

		return result != default(FilterExpression<TItem>) ? result : null;
	}

	/// <summary>
	/// Pops the filter with the specified <paramref name="filterSelector"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TFilterValue">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="filterSelector">The filter selector.</param>
	/// <returns>A tuple containing details of whether the filter exists, its value, and the updated filters without the popped filter.</returns>
	public static (bool found, TFilterValue filterValue, IEnumerable<FilterExpression<TItem>>? updatedFilters) PopFilter<TItem, TFilterValue>(this IEnumerable<FilterExpression<TItem>>? filters, Expression<Func<TItem, TFilterValue>> filterSelector)
		=> PopFilter<TItem, TFilterValue>(filters, filterSelector.GetMemberPath());

	/// <summary>
	/// Pops the filter with the specified <paramref name="filterPath"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TFilterValue">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="filterPath">The filter path.</param>
	/// <param name="cultureInfo">The optional culture.</param>
	/// <returns>A tuple containing details of whether the filter exists, its value, and the updated filters without the popped filter.</returns>
	/// <remarks>If the <paramref name="cultureInfo"/> is not specified, <see cref="CultureInfo.InvariantCulture"/> is used.</remarks>
	public static (bool found, TFilterValue filterValue, IEnumerable<FilterExpression<TItem>>? updatedFilters) PopFilter<TItem, TFilterValue>(this IEnumerable<FilterExpression<TItem>>? filters, string filterPath, CultureInfo? cultureInfo = null)
	{
		if (filters is null)
			return (false, default!, filters);

		FilterExpression<TItem>? filter = filters.FindByMemberPath(filterPath);

		if (filter is null)
			return (false, default!, filters);

		var updatedFilters = filters.Where(x => x != filter);

		if (filter.Value.Value is null)
			return (true, default!, updatedFilters);

		object? value = Convert.ChangeType(filter.Value.Value, Nullable.GetUnderlyingType(typeof(TFilterValue)) ?? typeof(TFilterValue), cultureInfo ?? CultureInfo.InvariantCulture);

		if (value is not null)
			return (true, (TFilterValue)value, updatedFilters);

		return (false, default!, updatedFilters);
	}

	/// <summary>
	/// Peeks at the filter with the specified <paramref name="filterSelector"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TFilterValue">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="filterSelector">The filter selector.</param>
	/// <returns>A tuple containing details of whether the filter exists and its value.</returns>
	public static (bool found, TFilterValue filterValue) PeekFilter<TItem, TFilterValue>(this IEnumerable<FilterExpression<TItem>>? filters, Expression<Func<TItem, TFilterValue>> filterSelector)
		=> PeekFilter<TItem, TFilterValue>(filters, filterSelector.GetMemberPath());

	/// <summary>
	/// Peeks the filter with the specified <paramref name="filterPath"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TFilterValue">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="filterPath">The filter path.</param>
	/// <param name="cultureInfo">The optional culture.</param>
	/// <returns>A tuple containing details of whether the filter exists and its value.</returns>
	/// <remarks>If the <paramref name="cultureInfo"/> is not specified, <see cref="CultureInfo.InvariantCulture"/> is used.</remarks>
	public static (bool found, TFilterValue filterValue) PeekFilter<TItem, TFilterValue>(this IEnumerable<FilterExpression<TItem>>? filters, string filterPath, CultureInfo? cultureInfo = null)
	{
		FilterExpression<TItem>? filter = filters.FindByMemberPath(filterPath);

		if (filter is null || filter.Value.Value is null)
			return (false, default!);

		// TODO: Will this work for enums?? Should do but can't remember.
		// It'll only work if the filter value has already been converted to a enum before this method has been called into. Check the model binders.
		object? value = Convert.ChangeType(filter.Value.Value, Nullable.GetUnderlyingType(typeof(TFilterValue)) ?? typeof(TFilterValue), cultureInfo ?? CultureInfo.InvariantCulture);

		if (value is not null)
			return (true, (TFilterValue)value);

		return (false, default!);
	}

	/// <summary>
	/// Finds a filter with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <returns>The filter, if it exists.</returns>
	public static FilterExpressionDescriptor? FindFilterExpressionDescriptor(this IEnumerable<IDataExpressionDescriptor>? filters, string memberPath)
		=> filters?.OfType<FilterExpressionDescriptor>().SingleOrDefault(x => x.MemberPath?.Equals(memberPath, StringComparison.OrdinalIgnoreCase) ?? false);

	/// <summary>
	/// Finds a filter value with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <typeparam name="T">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <param name="fallbackCreator">The fallback value creator.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The filter value, or the fallback if it does not exist.</returns>
	public static T FindFilterValue<T>(this IEnumerable<IDataExpressionDescriptor>? filters, string memberPath, Func<T> fallbackCreator, Func<string?, T>? customValueConverter = null)
	{
		FilterExpressionDescriptor? descriptor = FindFilterExpressionDescriptor(filters, memberPath);

		string? value = descriptor?.Value;

		return GenericTypeConverterHelper.Convert(value, fallbackCreator, customValueConverter);
	}

	/// <summary>
	/// Finds a filter value with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <typeparam name="T">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <param name="fallback">The fallback value.</param>
	/// <param name="customValueConverter">The custom value converter.</param>
	/// <returns>The filter value, or the fallback if it does not exist.</returns>
	public static T FindFilterValue<T>(this IEnumerable<IDataExpressionDescriptor>? filters, string memberPath, T fallback = default!, Func<string?, T>? customValueConverter = null)
		=> FindFilterValue(filters, memberPath, () => fallback, customValueConverter);

	/// <summary>
	/// Finds a filter enum value with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <typeparam name="T">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <param name="fallback">The fallback value.</param>
	/// <returns>The filter value, or the fallback if it does not exist.</returns>
	public static T FindFilterEnumValue<T>(this IEnumerable<IDataExpressionDescriptor>? filters, string memberPath, T fallback = default)
		where T : struct, Enum
	{
		FilterExpressionDescriptor? descriptor = FindFilterExpressionDescriptor(filters, memberPath);

		string? value = descriptor?.Value;

		return GenericTypeConverterHelper.ConvertToEnum(value, fallback);
	}

	/// <summary>
	/// Finds a nullable filter enum value with the specified <paramref name="memberPath"/>.
	/// </summary>
	/// <typeparam name="T">The type of the filter value.</typeparam>
	/// <param name="filters">The filters.</param>
	/// <param name="memberPath">The member path.</param>
	/// <param name="fallback">The fallback value.</param>
	/// <returns>The filter value, or the fallback if it does not exist.</returns>
	public static T? FindNullableFilterEnumValue<T>(this IEnumerable<IDataExpressionDescriptor>? filters, string memberPath, T? fallback = null)
		where T : struct, Enum
	{
		FilterExpressionDescriptor? descriptor = FindFilterExpressionDescriptor(filters, memberPath);

		string? value = descriptor?.Value;

		return GenericTypeConverterHelper.ConvertToNullableEnum(value, fallback);
	}
}