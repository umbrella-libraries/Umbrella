using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;

namespace Umbrella.AspNetCore.Blazor.Components.Grid;

/// <summary>
/// Represents a definition of an <see cref="UmbrellaColumn{TItem, TValue}"/> .
/// </summary>
/// <typeparam name="TItem">The type of the item.</typeparam>
public interface IUmbrellaColumnDefinition<TItem>
{
	/// <summary>
	/// Gets a collection of the unmatched parameter values specified on the column component that this instance is
	/// associated with.
	/// </summary>
	IReadOnlyDictionary<string, object> AdditionalAttributes { get; }

	/// <summary>
	/// Gets or sets the add on button CSS class.
	/// </summary>
	string? AddOnButtonCssClass { get; set; }

	/// <summary>
	/// Gets or sets the add on button icon CSS class.
	/// </summary>
	string? AddOnButtonIconCssClass { get; set; }

	/// <summary>
	/// Gets or sets the add on button text.
	/// </summary>
	string? AddOnButtonText { get; set; }

	/// <summary>
	/// Gets the CSS class for the width of the column based on the value of the <see cref="PercentageWidth"/> property.
	/// </summary>
	string ColumnWidthCssClass { get; }

	/// <summary>
	/// Gets or sets the sort direction.
	/// </summary>
	SortDirection? Direction { get; set; }

	/// <summary>
	/// Gets the sort direction CSS class based on the current value of the <see cref="Direction"/> property.
	/// </summary>
	string DirectionCssClass { get; }

	/// <summary>
	/// Gets the display mode.
	/// </summary>
	UmbrellaColumnDisplayMode DisplayMode { get; }

	UmbrellaColumnDisplayMode GetDisplayMode();

	/// <summary>
	/// Gets the function that determines the display mode for the umbrella column.
	/// </summary>
	/// <remarks>
	/// This property allows customization of how the umbrella column is displayed based on the provided display mode
	/// function. The function should return an instance of UmbrellaColumnDisplayMode that dictates the visual
	/// representation.
	/// </remarks>
	Func<UmbrellaColumnDisplayMode>? DisplayModelSelector { get; }

	/// <summary>
	/// Gets a value specifying whether or not the column is filterable.
	/// </summary>
	bool Filterable { get; }

	/// <summary>
	/// Gets a value specifying the type of control that is rendered to allow filtering, if enabled using the
	/// <see cref="Filterable"/> property.
	/// </summary>
	UmbrellaColumnFilterType FilterControlType { get; }

	/// <summary>
	/// Gets a value specifying how the <see cref="FilterValue"/> will be matched against the data in the column.
	/// </summary>
	FilterType FilterMatchType { get; }

	/// <summary>
	/// Gets or sets the property path override used as the <see cref="FilterExpressionDescriptor.MemberPath"/> property
	/// value when creating filters.
	/// </summary>
	/// <remarks>
	/// If this value is <see langword="null"/> , the <see cref="PropertyName"/> will be used.
	/// </remarks>
	string? FilterMemberPathOverride { get; }

	/// <summary>
	/// Gets a delegate used to convert a filter option to a friendly string for displaying to the user.
	/// </summary>
	Func<object, string>? FilterOptionDisplayNameSelector { get; }

	/// <summary>
	/// Gets the filter options displayed to the user.
	/// </summary>
	IReadOnlyCollection<object>? FilterOptions { get; }

	/// <summary>
	/// Specifies the type of the <see cref="FilterOptions"/> displayed to the user.
	/// </summary>
	UmbrellaColumnFilterOptionsType? FilterOptionsType { get; }

	/// <summary>
	/// Gets or sets the value used to filter the column.
	/// </summary>
	string? FilterValue { get; set; }

	/// <summary>
	/// Gets the column heading display text.
	/// </summary>
	string? Heading { get; }

	/// <summary>
	/// Gets or sets the nullable enum option.
	/// </summary>
	/// <remarks>
	/// If a value is provided, an option will be shown when the <see cref="FilterOptionsType"/> is set to
	/// <see cref="UmbrellaColumnFilterOptionsType.Enum"/> which will show a new option after the <c> Any</c> option with
	/// an explcit value of <see langword="null" /> with the value specified for this property value displayed as the text
	/// in the dropdown for the option.
	/// </remarks>
	string? NullableEnumOption { get; set; }

	/// <summary>
	/// Gets or sets the addon button delegate which will be invoked when the add-on button is clicked when the
	/// <see cref="FilterControlType"/> is set to <see cref="UmbrellaColumnFilterType.TextAddOnButton"/> .
	/// </summary>
	/// <remarks>
	/// The delegate must accept a string parameter which will be the value of the current filter and return a string,
	/// which is the new filter value to set the text box's content to, wrapped in a <see cref="ValueTask"/> .
	/// </remarks>
	Func<string?, ValueTask<string?>>? OnAddOnButtonClickedAsync { get; set; }

	/// <summary>
	/// Gets the percentage width of the column.
	/// </summary>
	int? PercentageWidth { get; }

	/// <summary>
	/// Gets or sets the name of the property.
	/// </summary>
	string? PropertyName { get; }

	/// <summary>
	/// Gets the column short heading display text.
	/// </summary>
	string? ShortHeading { get; }

	/// <summary>
	/// Gets a value specifying whether or not the column is sortable.
	/// </summary>
	bool Sortable { get; }

	/// <summary>
	/// Gets or sets the property path override used as the value <see cref="SortExpressionDescriptor.MemberPath"/>
	/// property value when creating sorters.
	/// </summary>
	/// <remarks>
	/// If this value is <see langword="null"/> , the <see cref="PropertyName"/> will be used.
	/// </remarks>
	string? SorterMemberPathOverride { get; }

	/// <summary>
	/// Gets a friendly display name for a specified filter <paramref name="option"/> .
	/// </summary>
	/// <param name="option">The filter option.</param>
	/// <returns>The friendly display name.</returns>
	string GetFilterOptionDisplayName(object option);

	/// <summary>
	/// Gets the type of the filter value.
	/// </summary>
	/// <remarks>
	/// This is the <see cref="Type"/> of the <see cref="FilterValue"/> .
	/// </remarks>
	Type FilterValueType { get; }

	/// <summary>
	/// Converts the <see cref="FilterValue"/> to a string displayed as a date range, e.g. 01/01/2023 - 31/12/2023, if the
	/// <see cref="FilterControlType"/> is <see cref="UmbrellaColumnFilterType.DateRange"/> .
	/// </summary>
	/// <returns>The date range as a string.</returns>
	string? ToDateRangeDisplayValue();

	/// <summary>
	/// Gets or sets the AutoComplete search method.
	/// </summary>
	Func<string?, Task<IEnumerable<string>>>? AutoCompleteSearchMethod { get; }

	/// <summary>
	/// Gets or sets the AutoComplete debounce in milliseconds.
	/// </summary>
	int AutoCompleteDebounce { get; }

	/// <summary>
	/// Gets or sets the AutoComplete maximum suggestions that will be displayed to the user.
	/// </summary>
	int AutoCompleteMaximumSuggestions { get; }

	/// <summary>
	/// Gets or sets the minimum characters that need to be provided before the <see cref="AutoCompleteSearchMethod"/>
	/// delegate is invoked.
	/// </summary>
	int AutoCompleteMinimumLength { get; }
}
