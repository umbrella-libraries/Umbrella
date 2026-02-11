using System.Linq.Expressions;
using System.Text.Json;
using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Dating;
using Umbrella.Utilities.Dating.Json;
using Umbrella.Utilities.TypeConverters;

namespace Umbrella.AspNetCore.Blazor.Components.Grid;

/// <summary>
/// Defines a column displayed using the <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
public record UmbrellaColumnDefinition<TItem, TValue> : IUmbrellaColumnDefinition<TItem>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaColumnDefinition{TItem, TValue}"/> class.
	/// </summary>
	/// <param name="heading">The heading.</param>
	/// <param name="shortHeading">The short heading.</param>
	/// <param name="percentageWidth">Width of the percentage.</param>
	/// <param name="sortable">if set to <c>true</c> allows the column to be sortable.</param>
	/// <param name="filterable">if set to <c>true</c> allows the column to be filterable.</param>
	/// <param name="filterOptions">The filter options.</param>
	/// <param name="filterOptionDisplayNameSelector">The filter option display name selector.</param>
	/// <param name="additionalAttributes">The additional attributes.</param>
	/// <param name="filterControlType">Type of the filter control.</param>
	/// <param name="filterMatchType">Type of the filter match.</param>
	/// <param name="filterOptionsType">Type of the filter options.</param>
	/// <param name="property">The property selector.</param>
	/// <param name="filterMemberPathOverride">The filter member override.</param>
	/// <param name="sorterMemberPathOverride">The sorter member override.</param>
	/// <param name="displayMode">The display mode.</param>
	/// <param name="nullableEnumOption">The nullable enum option.</param>
	/// <param name="onAddOnButtonClickedAsync">The delegate invoked when the text addon button is clicked.</param>
	/// <param name="addOnButtonCssClass">The CSS class for the add-on button.</param>
	/// <param name="addOnButtonText">The text for the add-on button.</param>
	/// <param name="addOnButtonIconCssClass">The CSS class for the icon displayed on the add-on button.</param>
	/// <param name="autoCompleteDebounce">The autocomplete debounce in milliseconds.</param>
	/// <param name="autoCompleteMaximumSuggestions">The maximum number of autocomplete suggestions to be shown.</param>
	/// <param name="autoCompleteMinimumLength">The minimum length of search text required before the autocomplete search method is called.</param>
	/// <param name="autoCompleteSearchMethod">The callback method invoked when autocomplete suggestions are required.</param>
	public UmbrellaColumnDefinition(
		string? heading,
		string? shortHeading,
		int? percentageWidth,
		bool sortable,
		bool filterable,
		IReadOnlyCollection<object>? filterOptions,
		Func<object, string>? filterOptionDisplayNameSelector,
		IReadOnlyDictionary<string, object> additionalAttributes,
		UmbrellaColumnFilterType filterControlType,
		FilterType filterMatchType,
		UmbrellaColumnFilterOptionsType? filterOptionsType,
		Expression<Func<TItem, TValue?>>? property,
		string? filterMemberPathOverride,
		string? sorterMemberPathOverride,
		UmbrellaColumnDisplayMode displayMode,
		string? nullableEnumOption,
		Func<string?, ValueTask<string?>>? onAddOnButtonClickedAsync,
		string? addOnButtonCssClass,
		string? addOnButtonText,
		string? addOnButtonIconCssClass,
		int autoCompleteDebounce,
		int autoCompleteMaximumSuggestions,
		int autoCompleteMinimumLength,
		Func<string?, Task<IEnumerable<string>>>? autoCompleteSearchMethod)
	{
		Heading = heading;
		ShortHeading = shortHeading;
		PercentageWidth = percentageWidth;
		Sortable = sortable;
		Filterable = filterable;
		FilterOptions = filterOptions;
		FilterOptionDisplayNameSelector = filterOptionDisplayNameSelector;
		AdditionalAttributes = additionalAttributes;
		FilterControlType = filterControlType;
		FilterMatchType = filterMatchType;
		FilterOptionsType = filterOptionsType;
		Property = property;
		PropertyName = property?.GetMemberName();
		FilterMemberPathOverride = filterMemberPathOverride;
		SorterMemberPathOverride = sorterMemberPathOverride;
		DisplayMode = displayMode;
		NullableEnumOption = nullableEnumOption;
		OnAddOnButtonClickedAsync = onAddOnButtonClickedAsync;
		AddOnButtonCssClass = addOnButtonCssClass;
		AddOnButtonText = addOnButtonText;
		AddOnButtonIconCssClass = addOnButtonIconCssClass;

		if (FilterOptionsType is UmbrellaColumnFilterOptionsType.Enum)
		{
			FilterControlType = UmbrellaColumnFilterType.Options;
			FilterMatchType = FilterType.Equal;
		}
		else if (FilterOptionsType is UmbrellaColumnFilterOptionsType.Boolean)
		{
			FilterControlType = UmbrellaColumnFilterType.Options;
			FilterMatchType = FilterType.Equal;
			FilterOptions = new[] { "Yes", "No" };
			NullableEnumOption = null;
		}
		else if (FilterControlType is UmbrellaColumnFilterType.Date or UmbrellaColumnFilterType.DateRange or UmbrellaColumnFilterType.AutoComplete)
		{
			FilterMatchType = FilterType.Equal;
		}
		else if (FilterControlType is UmbrellaColumnFilterType.Options || FilterOptions is not null)
		{
			FilterControlType = UmbrellaColumnFilterType.Options;
			FilterOptionsType = UmbrellaColumnFilterOptionsType.String;
			FilterMatchType = FilterType.Equal;
			NullableEnumOption = null;
		}

		if (Property is not null)
		{
			if (string.IsNullOrEmpty(Heading))
			{
				Heading = Property.GetDisplayText();
			}

			if (string.IsNullOrEmpty(ShortHeading))
			{
				ShortHeading = Property.GetShortNameDisplayText() ?? Property.GetDisplayText();
			}
		}

		FilterValueType = typeof(TValue);
		AutoCompleteDebounce = autoCompleteDebounce;
		AutoCompleteMaximumSuggestions = autoCompleteMaximumSuggestions;
		AutoCompleteMinimumLength = autoCompleteMinimumLength;
		AutoCompleteSearchMethod = autoCompleteSearchMethod;
	}

	/// <summary>
	/// Gets the property selector.
	/// </summary>
	public Expression<Func<TItem, TValue?>>? Property { get; }

	/// <inheritdoc/>
	public string? PropertyName { get; }

	/// <inheritdoc/>
	public string? FilterMemberPathOverride { get; }

	/// <inheritdoc/>
	public string? SorterMemberPathOverride { get; }

	/// <inheritdoc/>
	public UmbrellaColumnDisplayMode DisplayMode { get; }

	/// <inheritdoc/>
	public string? Heading { get; }

	/// <inheritdoc/>
	public string? ShortHeading { get; }

	/// <inheritdoc/>
	public int? PercentageWidth { get; }

	/// <inheritdoc/>
	public bool Sortable { get; }

	/// <inheritdoc/>
	public bool Filterable { get; }

	/// <inheritdoc/>
	public UmbrellaColumnFilterType FilterControlType { get; }

	/// <inheritdoc/>
	public FilterType FilterMatchType { get; }

	/// <inheritdoc/>
	public IReadOnlyCollection<object>? FilterOptions { get; }

	/// <inheritdoc/>
	public UmbrellaColumnFilterOptionsType? FilterOptionsType { get; }

	/// <inheritdoc/>
	public Func<object, string>? FilterOptionDisplayNameSelector { get; }

	/// <inheritdoc/>
	public IReadOnlyDictionary<string, object> AdditionalAttributes { get; }

	/// <inheritdoc/>
	public string? FilterValue { get; set; }

	/// <inheritdoc/>
	public string? NullableEnumOption { get; set; }

	/// <inheritdoc/>
	public Func<string?, ValueTask<string?>>? OnAddOnButtonClickedAsync { get; set; }

	/// <inheritdoc/>
	public string? AddOnButtonCssClass { get; set; }

	/// <inheritdoc/>
	public string? AddOnButtonText { get; set; }

	/// <inheritdoc/>
	public string? AddOnButtonIconCssClass { get; set; }

	/// <inheritdoc/>
	public SortDirection? Direction { get; set; }

	/// <inheritdoc/>
	public string DirectionCssClass => Direction switch
	{
		SortDirection.Ascending => "fas fa-sort-up",
		SortDirection.Descending => "fas fa-sort-down",
		_ => "fas fa-sort",
	};

	/// <inheritdoc/>
	public string ColumnWidthCssClass => PercentageWidth.HasValue ? $"u-grid__column--{PercentageWidth}" : "u-grid__column--auto";

	/// <inheritdoc/>
	public Type FilterValueType { get; }

	/// <inheritdoc/>
	public TValue? TypedFilterValue
	{
		get
		{
			return FilterValue is not null ? GenericTypeConverterHelper.Convert<TValue>(FilterValue) : default;
		}
		set
		{
			FilterValue = value is not null ? value.ToString() : default;
		}
	}

	/// <inheritdoc/>
	public int AutoCompleteDebounce { get; }

	/// <inheritdoc/>
	public int AutoCompleteMaximumSuggestions { get; }

	/// <inheritdoc/>
	public int AutoCompleteMinimumLength { get; }

	/// <inheritdoc/>
	public Func<string?, Task<IEnumerable<string>>>? AutoCompleteSearchMethod { get; }

	/// <inheritdoc/>
	public string GetFilterOptionDisplayName(object option)
	{
		if (FilterOptionDisplayNameSelector is null)
		{
			if (option is string strOption)
			{
				var lstChar = new List<char>();

				for (int i = 0; i < strOption.Length; i++)
				{
					lstChar.Add(strOption[i]);

					if (i + 1 < strOption.Length && strOption[i + 1] == char.ToUpperInvariant(strOption[i + 1]))
						lstChar.Add(' ');
				}

				return string.Join("", lstChar);
			}
			else if (option is Enum enumOption)
			{
				return enumOption.ToDisplayString();
			}

			return option.ToString() ?? throw new UmbrellaBlazorException("The display name for the option must have a value.");
		}

		return FilterOptionDisplayNameSelector(option);
	}

	/// <inheritdoc/>
	public string? ToDateRangeDisplayValue()
	{
		Guard.IsTrue(FilterControlType is UmbrellaColumnFilterType.DateRange);

		if (!string.IsNullOrEmpty(FilterValue))
		{
			DateTimeRange model = JsonSerializer.Deserialize(FilterValue, DateTimeRangeJsonSerializerContext.Default.DateTimeRange);

			if (model.StartDate != DateTime.MinValue && model.EndDate != DateTime.MinValue)
				return $"{model.StartDate:d} - {model.EndDate:d}";
		}

		return null;
	}
}