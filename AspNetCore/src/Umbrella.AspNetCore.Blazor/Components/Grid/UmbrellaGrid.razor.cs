using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Blazored.Modal;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Grid.Dialogs;
using Umbrella.AspNetCore.Blazor.Components.Grid.Dialogs.Models;
using Umbrella.AspNetCore.Blazor.Components.Grid.Options;
using Umbrella.AspNetCore.Blazor.Components.Pagination;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.AspNetCore.Blazor.Enumerations;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;
using Umbrella.AspNetCore.Shared.Extensions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Dating;
using Umbrella.Utilities.Dating.Json;
using Umbrella.Utilities.Primitives;

namespace Umbrella.AspNetCore.Blazor.Components.Grid;

// TODO: Maybe it's better to create a new CollectionView control here. This Grid adds nothing more than a loop and some basic structure for that scenario.
// Could even have some kind of base component that encapsulates core functionality, e.g. auto-scroll.
// TODO: Create a PaginationMode option to replace ShowPagination: None, Top, Bottom, Both. Do the same for caption. Default to Bottom.

/// <summary>
/// The rendering mode the <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
public enum UmbrellaGridRenderMode
{
	/// <summary>
	/// Full rendering mode where the grid is rendered as a table.
	/// </summary>
	Table,

	/// <summary>
	/// Slim rendering mode the grid and its columns are rendered using divs in order to allow non-tabular layouts to be
	/// used.
	/// </summary>
	CollectionView
}

/// <summary>
/// A grid component used to display bound items as a series of columns in a grid.
/// </summary>
/// <typeparam name="TItem">The type of the item.</typeparam>
/// <seealso cref="ComponentBase" />
/// <seealso cref="IUmbrellaGrid{TItem}" />
[CascadingTypeParameter(nameof(TItem))]
public partial class UmbrellaGrid<TItem> : IUmbrellaGrid<TItem>, IAsyncDisposable
	where TItem : notnull
{
	private sealed class UmbrellaGridSelectableItem
	{
		public UmbrellaGridSelectableItem(bool isSelected, TItem item)
		{
			IsSelected = isSelected;
			Item = item;
		}

		public bool IsSelected { get; set; }
		public TItem Item { get; }
	}

	private enum QueryStringStateUpdateMode { None, Reset, Update }

#pragma warning disable CA2213 // Disposable fields should be disposed
	private readonly CancellationTokenSource _cts = new();
#pragma warning restore CA2213 // Disposable fields should be disposed
	private bool _autoScrollEnabled;
	private string? _initialSortPropertyName;
	private Expression<Func<TItem, object>>? _initialSortPropertyExpression;
	private bool _disposedValue;
	private string? _sessionStorageSearchStateKey;

	private EditContext EditContext { get; } = new(new object());

	[Inject]
	private ILogger<UmbrellaGrid<TItem>> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IUmbrellaBlazorInteropService BlazorInteropUtility { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private IUmbrellaDialogService DialogService { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private NavigationManager Navigation { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the options.
	/// </summary>
	[Inject]
	protected UmbrellaGridOptions Options { get; private set; } = null!;

	[Inject]
	private Lazy<IBrowserEventAggregator> BrowserEventAggregator { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	[Inject]
	private ISessionStorageService SessionStorageService { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the instance of the associated <see cref="UmbrellaPagination"/> component.
	/// </summary>
	private UmbrellaPagination PaginationInstance { get; set; } = null!;

	/// <summary>
	/// Gets or sets a value indicating whether the scanning process that occurs when the grid is first initialized, which
	/// looks for columns, has been completed.
	/// </summary>
	private bool ColumnScanComplete { get; set; }

	/// <summary>
	/// Gets the columns.
	/// </summary>
	private HashSet<IUmbrellaColumnDefinition<TItem>> ColumnDefinitions { get; } = [];

	private List<UmbrellaGridSelectableItem> SelectableItems { get; } = [];

	private TItem SelectedRow { get; set; } = default!;

	/// <summary>
	/// Gets or sets a value indicating whether an item has been selected using a row checkbox.
	/// </summary>
	public bool CheckboxSelectColumnSelected { get; private set; }

	/// <inheritdoc />
	public string? FirstColumnPropertyName { get; private set; }

	/// <summary>
	/// Gets the filterable columns.
	/// </summary>
	private List<IUmbrellaColumnDefinition<TItem>>? FilterableColumns { get; set; }

	/// <summary>
	/// Gets the current <see cref="LayoutState"/> of the component.
	/// </summary>
	private LayoutState CurrentState { get; set; } = LayoutState.Loading;

	/// <summary>
	/// Gets or sets the total count of all items that are available to be loaded into the grid. This will be larger than
	/// the number of items currently displayed where pagination has been applied.
	/// </summary>
	private int TotalCount { get; set; }

	/// <summary>
	/// Gets the current page number. Defaults to <see cref="UmbrellaPaginationDefaults.PageNumber"/>.
	/// </summary>
	public int PageNumber { get; private set; } = UmbrellaPaginationDefaults.PageNumber;

	/// <summary>
	/// Gets the current page size. Defaults to <see cref="UmbrellaPaginationDefaults.PageSize"/>.
	/// </summary>
	public int PageSize { get; private set; } = UmbrellaPaginationDefaults.PageSize;

	/// <summary>
	/// Gets or sets the dialog instance.
	/// </summary>
	[CascadingParameter]
	protected BlazoredModalInstance? ModalInstance { get; set; }

	/// <summary>
	/// Gets or sets the additional content to be displayed between the filter fields and the action buttons that apply the
	/// filters to the grid.
	/// </summary>
	[Parameter]
	public RenderFragment? AdditionalFilterContent { get; set; }

	/// <summary>
	/// Gets or sets the additional content to be displayed inside the <![CDATA[<tfoot> ]]> element of the grid.
	/// </summary>
	[Parameter]
	public RenderFragment? FooterContent { get; set; }

	/// <summary>
	/// Gets or sets the columns to be displayed inside this grid component. This should be a collection of columns
	/// components.
	/// </summary>
	[Parameter]
	public RenderFragment<TItem>? Columns { get; set; }

	/// <summary>
	/// Gets or sets the bulk actions that can be performed on items in this grid.
	/// </summary>
	[Parameter]
	public RenderFragment? BulkActions { get; set; }

	/// <summary>
	/// Gets or sets the items collection displayed in the grid.
	/// </summary>
	public IReadOnlyCollection<TItem> Items { get; private set; } = Array.Empty<TItem>();

	/// <summary>
	/// Gets or sets the message displayed when the grid is loading. This is ususally only shown when the grid is first
	/// initialized.
	/// </summary>
	/// <remarks>Defaults to <c>Loading... Please wait.</c></remarks>
	[Parameter]
	public string LoadingMessage { get; set; } = "Loading... Please wait.";

	/// <summary>
	/// Gets or sets the content displayed when the grid is loading. This is ususally only shown when the grid is first
	/// initialized. This overrides the value of the <see cref="LoadingMessage"/> when specified.
	/// </summary>
	[Parameter]
	public RenderFragment? LoadingMessageContent { get; set; }

	/// <summary>
	/// Gets or sets the message displayed when the grid contains no data.
	/// </summary>
	/// <remarks>Defaults to <c>There is either no data to display or your search options have no results.</c></remarks>
	[Parameter]
	public string EmptyMessage { get; set; } = "There is either no data to display or your search options have no results.";

	/// <summary>
	/// Gets or sets the content displayed when there are no results. This overrides the value of the
	/// <see cref="EmptyMessage"/> when specified.
	/// </summary>
	/// <remarks>Defaults to <c>There has been a problem. Please try again.</c></remarks>
	[Parameter]
	public RenderFragment? EmptyMessageContent { get; set; }

	/// <summary>
	/// Gets or sets the error message displayed when the grid fails to load correctly.
	/// </summary>
	[Parameter]
	public string ErrorMessage { get; set; } = "There has been a problem. Please try again.";

	/// <summary>
	/// Gets or sets the content displayed when the grid fails to load correctly. This overrides the value of the
	/// <see cref="ErrorMessage"/> when specified.
	/// </summary>
	[Parameter]
	public RenderFragment? ErrorMessageContent { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the reload button should be shown when the grid has failed to load
	/// correctly. Defaults to <see langword="true"/>.
	/// </summary>
	[Parameter]
	public bool ShowReloadButton { get; set; } = true;

	/// <summary>
	/// Gets or sets the heading displayed above the filter options.
	/// </summary>
	[Parameter]
	public string FilterOptionsHeading { get; set; } = "Search Options";

	/// <summary>
	/// Gets or sets a value indicating whether the filter options, if there are any and <see cref="ShowFilters"/> is
	/// <see langword="true" />, are expanded when the grid is first loaded.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="true" />
	/// </remarks>
	[Parameter]
	public bool ExpandFiltersOnLoad { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether filter options should be displayed.
	/// </summary>
	[Parameter]
	public bool ShowFilters { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether pagination controls should be displayed.
	/// </summary>
	[Parameter]
	public bool ShowPagination { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the grid caption showing the range of results metadata should be displayed.
	/// </summary>
	[Parameter]
	public bool ShowCaption { get; set; } = true;

	/// <inheritdoc />
	[Parameter]
	public UmbrellaGridRenderMode RenderMode { get; set; } = UmbrellaGridRenderMode.Table;

	/// <summary>
	/// Gets or sets the initial sort property.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Expression<Func<TItem, object>>? InitialSortProperty
	{
		get => _initialSortPropertyExpression;
		set
		{
			_initialSortPropertyExpression = value;
			_initialSortPropertyName = value?.GetMemberName();
		}
	}

	/// <summary>
	/// Gets or sets the initial sort direction. Defaults to <see cref="SortDirection.Descending"/>.
	/// </summary>
	[Parameter]
	public SortDirection InitialSortDirection { get; set; } = SortDirection.Descending;

	/// <summary>
	/// Gets or sets the initial filter expressions. Defaults to an empty array.
	/// </summary>
	[Parameter]
	public IReadOnlyCollection<FilterExpressionDescriptor> InitialFilterExpressions { get; set; } = Array.Empty<FilterExpressionDescriptor>();

	/// <summary>
	/// Gets or sets the callback that is invoked when the grid makes a request for data.
	/// </summary>
	[Parameter]
	[EditorRequired]
	public Func<UmbrellaGridDataRequest, CancellationToken, Task<UmbrellaGridDataResponse<TItem>?>> OnDataRequestedAsync { get; set; } = null!;

	/// <summary>
	/// Gets or sets the page size options. Defaults to <see cref="UmbrellaPaginationDefaults.PageSizeOptions"/>.
	/// </summary>
	[Parameter]
	public IReadOnlyCollection<int> PageSizeOptions { get; set; } = UmbrellaPaginationDefaults.PageSizeOptions;

	/// <summary>
	/// Gets or sets the CSS class applied to the grid.
	/// </summary>
	[Parameter]
	public string? GridCssClass { get; set; } = "table table-hover";

	/// <summary>
	/// Gets or sets the CSS class applied to each item in the grid.
	/// </summary>
	[Parameter]
	public string? ItemCssClass { get; set; }

	/// <summary>
	/// Gets or sets the CSS class applied to selected items within the grid.
	/// </summary>
	[Parameter]
	public string? ItemSelectedCssClass { get; set; } = "table-primary";

	/// <summary>
	/// Gets or sets a value indicating whether the pagination controls should be rendered using smaller styling.
	/// </summary>
	[Parameter]
	public bool SmallPagination { get; set; } = true;

	/// <summary>
	/// Gets or sets whether the grid should auto-scroll to the top when it is updated with new data.
	/// </summary>
	[Parameter]
	public bool AutoScrollTop { get; set; } = true;

	/// <summary>
	/// Gets or sets the scroll offset from the top of the screen when the grid is auto-scrolled to the top if enabled
	/// using the <see cref="AutoScrollTop"/> property.
	/// </summary>
	/// <remarks>
	/// This exists to allow for things liked fixed navigation bars to be taken into account.
	/// </remarks>
	[Parameter]
	public int ScrollTopOffset { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether checkboxes should be shown at the start of each row.
	/// </summary>
	/// <remarks>
	/// Checkboxes on each row are used to select multiple items which can be used in conjunction with items rendered using
	/// the <see cref="BulkActions" /> property.
	/// </remarks>
	[Parameter]
	public bool ShowCheckboxSelectColumn { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether radio buttons should be shown at the start of each row.
	/// </summary>
	/// <remarks>
	/// This is primarily designed to allow a single row to be selected and highlighted by a user when the grid requires
	/// horizontal scrolling. This will help a user to track a single selected row.
	/// </remarks>
	[Parameter]
	public bool ShowRadioSelectColumn { get; set; }

	/// <summary>
	/// Gets or sets the callback that will be invoked when the search filters and sorters are being reset.
	/// </summary>
	[Parameter]
	public EventCallback OnResetFiltersAndSorters { get; set; }

	/// <summary>
	/// Gets or sets the query string state discriminator used to distinguish between querystring state values for sorters
	/// and filters so that are applied to the correct grid component where more than one is in use on a single component
	/// or page.
	/// </summary>
	[Parameter]
	public string? QueryStringStateDiscriminator { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the search options state should be enabled. By default, this is
	/// <see langword="null" /> and the value of <see cref="UmbrellaGridOptions.IsSearchOptionStateEnabled"/> is used. If
	/// this property has a value, it will override the value of
	/// <see cref="UmbrellaGridOptions.IsSearchOptionStateEnabled"/>. If this is set to <see langword="true" />, the grid
	/// will attempt to maintain its state across navigation events.
	/// </summary>
	[Parameter]
	public bool? IsSearchOptionStateEnabledOverride { get; set; }

	private string PageNumberQueryStringParamKey => !string.IsNullOrEmpty(QueryStringStateDiscriminator) ? $"{QueryStringStateDiscriminator}:pageNumber" : "pageNumber";
	private string PageSizeQueryStringParamKey => !string.IsNullOrEmpty(QueryStringStateDiscriminator) ? $"{QueryStringStateDiscriminator}:pageSize" : "pageSize";
	private string SortByQueryStringParamKey => !string.IsNullOrEmpty(QueryStringStateDiscriminator) ? $"{QueryStringStateDiscriminator}:sortBy" : "sortBy";
	private string SortDirectionQueryStringParamKey => !string.IsNullOrEmpty(QueryStringStateDiscriminator) ? $"{QueryStringStateDiscriminator}:sortDirection" : "sortDirection";
	private string FiltersQueryStringParamKey => !string.IsNullOrEmpty(QueryStringStateDiscriminator) ? $"{QueryStringStateDiscriminator}:filters" : "filters";

	private bool IsSearchOptionStateEnabled { get; set; }

	/// <inheritdoc/>
	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		IsSearchOptionStateEnabled = (IsSearchOptionStateEnabledOverride ?? Options.IsSearchOptionStateEnabled) && ModalInstance is null;

		if (IsSearchOptionStateEnabled)
		{
			string url = new Uri(Navigation.Uri).GetComponents(UriComponents.Path, UriFormat.Unescaped).ToLowerInvariant();

			_sessionStorageSearchStateKey = HashCode.Combine(QueryStringStateDiscriminator, typeof(TItem).FullName, url).ToString(CultureInfo.InvariantCulture);

			if (RendererInfo.IsInteractive)
				await BrowserEventAggregator.Value.SubscribeAsync("popstate", async () => await InvokeAsync(async () => await ApplyQueryStringSortersAndFiltersAsync(false)), _cts.Token);
		}
	}

	private void OnCheckboxSelectColumnSelectionChanged(UmbrellaGridSelectableItem selectableItem)
	{
		selectableItem.IsSelected = !selectableItem.IsSelected;
		CheckboxSelectColumnSelected = SelectableItems.Any(x => x.IsSelected);
	}

	private void OnCheckboxSelectColumnSelectedChanged()
	{
		CheckboxSelectColumnSelected = !CheckboxSelectColumnSelected;
		SelectableItems.ForEach(x => x.IsSelected = CheckboxSelectColumnSelected);
	}

	/// <summary>
	/// Gets the caption text.
	/// </summary>
	private string CaptionText
	{
		get
		{
			int startItem = ((PageNumber - 1) * PageSize) + 1;
			int endItem = Math.Min(PageSize, Items.Count) + (PageNumber == 1 ? 0 : startItem - 1);

			return TotalCount == 1 ? "Showing 1 of 1 items" : $"Showing items {startItem} to {endItem} of {TotalCount}";
		}
	}

	/// <inheritdoc />
	public bool AddColumnDefinition(IUmbrellaColumnDefinition<TItem> column)
	{
		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { column });

		return ColumnDefinitions.Add(column);
	}

	/// <inheritdoc />
	public async ValueTask SetColumnScanCompletedAsync()
	{
		if (ColumnScanComplete)
			return;

		ColumnScanComplete = true;

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { ColumnScanComplete });

		var filterableColumns = new List<IUmbrellaColumnDefinition<TItem>>();

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { ColumnDefinitionsCount = ColumnDefinitions.Count });

		for (int i = 0; i < ColumnDefinitions.Count; i++)
		{
			var column = ColumnDefinitions.ElementAt(i);

			if (column.DisplayMode is UmbrellaColumnDisplayMode.None)
				continue;

			if (i is 0)
				FirstColumnPropertyName = column.PropertyName;

			if (column.Filterable)
				filterableColumns.Add(column);

			if (_initialSortPropertyName == column.PropertyName)
				column.Direction = InitialSortDirection;

			if (InitialFilterExpressions.Count > 0)
			{
				string? memberPath = column.FilterMemberPathOverride ?? column.PropertyName;

				if (!string.IsNullOrEmpty(memberPath))
				{
					string? value = InitialFilterExpressions.FindFilterValue<string?>(memberPath);

					if (value is not null)
						column.FilterValue = value;
				}
			}
		}

		FilterableColumns = filterableColumns;

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { FilterableColumnsCount = FilterableColumns.Count });

		await ApplyQueryStringSortersAndFiltersAsync(true);

		StateHasChanged();
	}

	/// <summary>
	/// Refreshes the grid, optionally resetting the state.
	/// </summary>
	/// <param name="resetState">if set to <see langword="true"/>, resets pagination, sorters and filters.</param>
	public async Task RefreshAsync(bool resetState = false)
	{
		if (resetState)
			await ResetFiltersAndSortersAsync();

		await UpdateGridAsync(resetState ? QueryStringStateUpdateMode.Reset : QueryStringStateUpdateMode.None);
	}

	/// <summary>
	/// Changes the <see cref="CurrentState"/> to <see cref="LayoutState.Error"/> and forces re-rendering.
	/// </summary>
	public void SetErrorState()
	{
		CurrentState = LayoutState.Error;
		StateHasChanged();
	}

	/// <summary>
	/// Gets a collection of the items that have been selected determined by the checked status of the checkboxes rendered
	/// on each row of the grid when <see cref="ShowCheckboxSelectColumn"/> is set to <see langword="true"/>.
	/// </summary>
	/// <returns>A collection of the selected items.</returns>
	public IReadOnlyCollection<TItem> GetSelectedItems() => SelectableItems.Where(x => x.IsSelected).Select(x => x.Item).ToArray();

	/// <summary>
	/// Gets the selected item.
	/// </summary>
	public TItem GetSelectedItem() => SelectedRow;
	
	/// <inheritdoc />
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			if (PaginationInstance is not null)
				await PaginationInstance.UpdateAsync(TotalCount, PageNumber, PageSize);
		}
		else
		{
			await SetColumnScanCompletedAsync();
		}
	}

	private async ValueTask InitializeNonInteractiveAsync()
	{
		await SetColumnScanCompletedAsync();

		if (PaginationInstance is not null)
			await PaginationInstance.UpdateAsync(TotalCount, PageNumber, PageSize);
	}

	/// <summary>
	/// The click event handler for the apply filters button.
	/// </summary>
	/// <returns>An awaitable Task that completes when the operation has completed.</returns>
	private async Task ApplyFiltersClickAsync() => await UpdateGridAsync(QueryStringStateUpdateMode.Update);

	/// <summary>
	/// The click event handler for the reset filters button.
	/// </summary>
	/// <returns>An awaitable Task that completes when the operation has completed.</returns>
	private async Task ResetFiltersClickAsync()
	{
		await ResetFiltersAndSortersAsync();
		await UpdateGridAsync(QueryStringStateUpdateMode.Reset);
	}

	/// <summary>
	/// The click event handler for a column heading used to trigger sorting of grid data.
	/// </summary>
	/// <param name="target">The column that has been clicked.</param>
	/// <returns>A <see cref="Task"/> that completes when the grid has been updated.</returns>
	private async Task ColumnHeadingClickAsync(IUmbrellaColumnDefinition<TItem> target)
	{
		foreach (var column in ColumnDefinitions)
		{
			column.Direction = column == target
				? column.Direction switch
				{
					SortDirection.Ascending => SortDirection.Descending,
					SortDirection.Descending => SortDirection.Ascending,
					_ => SortDirection.Ascending
				}
				: null;
		}

		await UpdateGridAsync(QueryStringStateUpdateMode.Update);
	}

	/// <summary>
	/// The click event handler for the reload button.
	/// </summary>
	/// <returns>A <see cref="Task"/> that completes when the grid has been reloaded.</returns>
	private async Task ReloadButtonClickAsync()
	{
		await ResetFiltersAndSortersAsync();
		await UpdateGridAsync(QueryStringStateUpdateMode.None, PageNumber, PageSize);
	}

	private static async Task FilterTextAddonButtonClickAsync(IUmbrellaColumnDefinition<TItem> columnDefinition)
	{
		if (columnDefinition.OnAddOnButtonClickedAsync is null)
			throw new UmbrellaBlazorException($"The {nameof(columnDefinition.OnAddOnButtonClickedAsync)} has not been specified.");

		columnDefinition.FilterValue = await columnDefinition.OnAddOnButtonClickedAsync(columnDefinition.FilterValue);
	}

	/// <summary>
	/// The event handler invoked by the <see cref="UmbrellaPagination"/> component when its state has changed as result of
	/// user interaction.
	/// </summary>
	/// <param name="args">The event arguments containing the updated state of the pagination component.</param>
	/// <returns>An awaitable Task that completed when this operation has completed.</returns>
	private Task OnPaginationOptionsChangedAsync(UmbrellaPaginationEventArgs args) => UpdateGridAsync(QueryStringStateUpdateMode.Update, args.PageNumber, args.PageSize);

	private async Task ResetFiltersAndSortersAsync()
	{
		PageNumber = 1;

		foreach (var column in ColumnDefinitions)
		{
			column.FilterValue = null;
			column.Direction = column.PropertyName == _initialSortPropertyName ? InitialSortDirection : null;

			foreach (var filter in InitialFilterExpressions)
			{
				if (column.PropertyName == filter.MemberPath || column.FilterMemberPathOverride == filter.MemberPath)
					column.FilterValue = filter.Value;
			}
		}

		if (OnResetFiltersAndSorters.HasDelegate)
			await OnResetFiltersAndSorters.InvokeAsync();
	}

	private async Task UpdateGridAsync(QueryStringStateUpdateMode queryStringStateUpdateMode, int? pageNumber = null, int? pageSize = null)
	{
		PageNumber = pageNumber ?? 1;

		if (pageSize.HasValue)
			PageSize = pageSize.Value;

		if (OnDataRequestedAsync is not null)
		{
			List<SortExpressionDescriptor>? lstSorters = null;
			List<FilterExpressionDescriptor>? lstFilters = null;

			foreach (var column in ColumnDefinitions)
			{
				if (string.IsNullOrEmpty(column.PropertyName) || column.DisplayMode == UmbrellaColumnDisplayMode.None)
				{
					continue;
				}

				if (column.Sortable && column.Direction.HasValue)
				{
					lstSorters ??= [];
					lstSorters.Add(new SortExpressionDescriptor(column.SorterMemberPathOverride ?? column.PropertyName, column.Direction.Value));
				}

				if (column.Filterable && !string.IsNullOrEmpty(column.FilterValue))
				{
					lstFilters ??= [];

					string? filterValue = column.FilterOptionsType switch
					{
						UmbrellaColumnFilterOptionsType.String when column.FilterControlType is UmbrellaColumnFilterType.Options && column.FilterValue.Equals("any", StringComparison.OrdinalIgnoreCase) => null,
						UmbrellaColumnFilterOptionsType.String => column.FilterValue,
						UmbrellaColumnFilterOptionsType.Boolean when column.FilterValue.Equals("yes", StringComparison.OrdinalIgnoreCase) => "true",
						UmbrellaColumnFilterOptionsType.Boolean when column.FilterValue.Equals("no", StringComparison.OrdinalIgnoreCase) => "false",
						UmbrellaColumnFilterOptionsType.Boolean when column.FilterValue.Equals("any", StringComparison.OrdinalIgnoreCase) => null,
						UmbrellaColumnFilterOptionsType.Enum when column.FilterValue.Equals("any", StringComparison.OrdinalIgnoreCase) => null,
						_ => column.FilterValue
					};

					if (!string.IsNullOrWhiteSpace(filterValue))
					{
						if (column.FilterControlType is UmbrellaColumnFilterType.DateRange)
						{
							DateTimeRange dateRange = JsonSerializer.Deserialize(column.FilterValue, DateTimeRangeJsonSerializerContext.Default.DateTimeRange);

							if (dateRange.StartDate != DateTime.MinValue && dateRange.EndDate != DateTime.MaxValue)
							{
								// Ensure we filter on the start and end of the day range.
								string dtStartValue = new DateTime(dateRange.StartDate.Year, dateRange.StartDate.Month, dateRange.StartDate.Day, 0, 0, 0, dateRange.StartDate.Kind).ToString("O");
								string dtEndValue = new DateTime(dateRange.EndDate.Year, dateRange.EndDate.Month, dateRange.EndDate.Day, 23, 59, 59, dateRange.EndDate.Kind).ToString("O");

								lstFilters.Add(new FilterExpressionDescriptor(column.FilterMemberPathOverride ?? column.PropertyName, dtStartValue.EndsWith("Z", StringComparison.InvariantCulture) ? dtStartValue : dtStartValue + "Z", FilterType.GreaterThanOrEqual));
								lstFilters.Add(new FilterExpressionDescriptor(column.FilterMemberPathOverride ?? column.PropertyName, dtEndValue.EndsWith("Z", StringComparison.InvariantCulture) ? dtEndValue : dtEndValue + "Z", FilterType.LessThanOrEqual));
							}
						}
						else
						{
							// Treating explicit values as special cases here where we want to intentionally pass a null value, e.g. for a nullable enum
							// where we want to filter only on null.
							if (filterValue is "null")
							{
								filterValue = null;
							}
							else if (DateTime.TryParse(filterValue, out DateTime dtFilter))
							{
								// Ensure we filter on the start and end of the day.
								string dtValue = new DateTime(dtFilter.Year, dtFilter.Month, dtFilter.Day, 23, 59, 59, dtFilter.Kind).ToString("O");

								filterValue = dtValue.EndsWith("Z", StringComparison.InvariantCulture) ? dtValue : dtValue + "Z";
							}

							lstFilters.Add(new FilterExpressionDescriptor(column.FilterMemberPathOverride ?? column.PropertyName, filterValue, column.FilterMatchType));
						}
					}
				}
			}

			static IReadOnlyCollection<T> EnsureCollection<T>(List<T>? coll)
			{
				IReadOnlyCollection<T>? target = coll;

				target ??= Array.Empty<T>();

				return target;
			}

			if (IsSearchOptionStateEnabled && queryStringStateUpdateMode is QueryStringStateUpdateMode.Reset or QueryStringStateUpdateMode.Update)
			{
				string url = Navigation.Uri;

				if (queryStringStateUpdateMode is QueryStringStateUpdateMode.Update)
				{
					// We only support a single sorter at the moment so just grab the first one.
					SortExpressionDescriptor? sortExpression = lstSorters?.FirstOrDefault();

					if (sortExpression is not null)
					{
						if (sortExpression.MemberPath == _initialSortPropertyName && sortExpression.Direction == InitialSortDirection)
						{
							url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
							{
								[SortByQueryStringParamKey] = null,
								[SortDirectionQueryStringParamKey] = null
							});
						}
						else
						{
							url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
							{
								[SortByQueryStringParamKey] = sortExpression.MemberPath,
								[SortDirectionQueryStringParamKey] = (int)sortExpression.Direction
							});
						}
					}

					if (lstFilters is { Count: > 0 })
					{
						var dicFilters = lstFilters.Where(x => !string.IsNullOrEmpty(x.MemberPath) && !string.IsNullOrEmpty(x.Value)).Select(x => new UmbrellaKeyValuePair<string, string>(x.MemberPath!, x.Value!));

						url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
						{
							[FiltersQueryStringParamKey] = JsonSerializer.Serialize(dicFilters, UmbrellaKeyValuePairJsonSerializerContext.Default.IEnumerableUmbrellaKeyValuePairStringString)
						});
					}
					else
					{
						url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
						{
							[FiltersQueryStringParamKey] = null
						});
					}

					if (Logger.IsEnabled(LogLevel.Debug))
						Logger.WriteDebug(new { PageNumber, PageSize });

					url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
					{
						[PageNumberQueryStringParamKey] = PageNumber > 1 ? PageNumber : null
					});

					url = Navigation.GetUriWithQueryParameters(url, new Dictionary<string, object?>
					{
						[PageSizeQueryStringParamKey] = PageSize != UmbrellaPaginationDefaults.PageSize ? PageSize : null
					});
				}
				else if (queryStringStateUpdateMode is QueryStringStateUpdateMode.Reset)
				{
					//All Grid query string values such as filter are removed, we need to ensure any changes to the page size are preserved.
					string[] queryStringKeysToRemove = [SortByQueryStringParamKey, SortDirectionQueryStringParamKey, FiltersQueryStringParamKey, PageNumberQueryStringParamKey];

					var uri = new Uri(url);
					uri = uri.GetUriWithoutQueryStringParameters(queryStringKeysToRemove);
					url = uri.AbsoluteUri;

					Logger.WriteDebug(message: "Url after reset.");
					if (Logger.IsEnabled(LogLevel.Debug))
						Logger.WriteDebug(new { url });
				}

				if (url != Navigation.Uri)
				{
					// Before navigating, store the url in SessionStorage, the intention being that if the user has navigated
					// away from the screen containing the grid, we can restore state by loading it from there.
					if (!string.IsNullOrEmpty(_sessionStorageSearchStateKey))
						await SessionStorageService.SetItemAsStringAsync(_sessionStorageSearchStateKey, url, _cts.Token);

					Navigation.NavigateTo(url);
				}
			}

			UmbrellaGridDataResponse<TItem>? response = await OnDataRequestedAsync(new UmbrellaGridDataRequest(PageNumber, PageSize, EnsureCollection(lstSorters), EnsureCollection(lstFilters)), _cts.Token);

			if (response is null)
			{
				await DialogService.ShowDangerMessageAsync("There has been a problem loading the data. Please try again.");
				return;
			}

			Items = response.Value.Items;
			TotalCount = response.Value.TotalCount ?? TotalCount;
			PageSize = response.Value.PageSize ?? PageSize;
			PageNumber = response.Value.PageNumber ?? PageNumber;

			SelectableItems.Clear();
			SelectableItems.AddRange(Items.Select(x => new UmbrellaGridSelectableItem(false, x)));
			SelectedRow = default!;
			CheckboxSelectColumnSelected = false;

			if (AutoScrollTop && _autoScrollEnabled)
				await BlazorInteropUtility.ScrollToAsync(".u-grid", ScrollTopOffset);

			// Only enable auto-scrolling after the initial page load.
			_autoScrollEnabled = true;

			CurrentState = Items.Count > 0 ? LayoutState.Success : LayoutState.Empty;

			if (response.Value.CallStateHasChanged)
				StateHasChanged();
		}
	}

	private async Task DateRangeSelectionButtonClickAsync(IUmbrellaColumnDefinition<TItem> column)
	{
		try
		{
			var result = await DialogService.ShowDialogAsync<DateRangeDialog>("Select Dates", "");

			if (result.Cancelled)
				return;

			if (result.Data is not DateRangeDialogModel resultModel)
				throw new InvalidOperationException("There has been a problem determining the selected date range.");

			var updatedDateRange = new DateTimeRange
			{
				StartDate = resultModel.StartDate ?? DateTime.MinValue,
				EndDate = resultModel.EndDate ?? DateTime.MinValue
			};

			column.FilterValue = JsonSerializer.Serialize(updatedDateRange, typeof(DateTimeRange), DateTimeRangeJsonSerializerContext.Default);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			await DialogService.ShowDangerMessageAsync();
		}
	}

	private async ValueTask ApplyQueryStringSortersAndFiltersAsync(bool tryRestoreFromSessionStorage)
	{
		await ResetFiltersAndSortersAsync();

		if (IsSearchOptionStateEnabled)
		{
			var sortByResult = Navigation.TryGetQueryStringValue<string>(SortByQueryStringParamKey);
			var sortDirectionResult = Navigation.TryGetQueryStringEnumValue<SortDirection>(SortDirectionQueryStringParamKey);
			var filtersResult = Navigation.TryGetQueryStringValue<string>(FiltersQueryStringParamKey);
			var pageNumberResult = Navigation.TryGetQueryStringValue<int>(PageNumberQueryStringParamKey);
			var pageSizeResult = Navigation.TryGetQueryStringValue<int>(PageSizeQueryStringParamKey);

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { sortByResult, sortDirectionResult, filtersResult, pageNumberResult, pageSizeResult }, "Reading QueryString");

			// If we have nothing on the querystring, try and restore the state from Session Storage
			if (RendererInfo.IsInteractive
				&& tryRestoreFromSessionStorage
				&& !sortByResult.success
				&& !sortDirectionResult.success
				&& !filtersResult.success
				&& !pageNumberResult.success
				&& !pageSizeResult.success
				&& !string.IsNullOrEmpty(_sessionStorageSearchStateKey))
			{
				string url = await SessionStorageService.GetItemAsStringAsync(_sessionStorageSearchStateKey, _cts.Token);

				if (!string.IsNullOrEmpty(url) && url != Navigation.Uri)
				{
					Navigation.NavigateTo(url, replace: true);

					var uri = Navigation.ToAbsoluteUri(url);

					// Re-read the values from the updated querystring
					sortByResult = uri.TryGetQueryStringValue<string>(SortByQueryStringParamKey);
					sortDirectionResult = uri.TryGetQueryStringEnumValue<SortDirection>(SortDirectionQueryStringParamKey);
					filtersResult = uri.TryGetQueryStringValue<string>(FiltersQueryStringParamKey);
					pageNumberResult = uri.TryGetQueryStringValue<int>(PageNumberQueryStringParamKey);
					pageSizeResult = uri.TryGetQueryStringValue<int>(PageSizeQueryStringParamKey);

					if (Logger.IsEnabled(LogLevel.Debug))
						Logger.WriteDebug(new { sortByResult, sortDirectionResult, filtersResult, pageNumberResult, pageSizeResult }, "Re-reading QueryString");
				}
			}

			if (sortByResult.success)
			{
				IUmbrellaColumnDefinition<TItem>? sortColumn = ColumnDefinitions.FirstOrDefault(x => x.Sortable && (x.PropertyName?.Equals(sortByResult.value, StringComparison.OrdinalIgnoreCase) is true || x.SorterMemberPathOverride?.Equals(sortByResult.value, StringComparison.OrdinalIgnoreCase) is true));

				if (sortColumn is not null)
				{
					// We need to clear any current sort columns before applying the one from the querystring
					foreach (var column in ColumnDefinitions)
					{
						column.Direction = null;
					}

					sortColumn.Direction = sortDirectionResult.success ? sortDirectionResult.value : SortDirection.Ascending;

					if (Logger.IsEnabled(LogLevel.Debug))
						Logger.WriteDebug(new { sortColumn.PropertyName, sortColumn.Direction }, "Applied Sorter");
				}
			}

			if (filtersResult.success)
			{
				List<UmbrellaKeyValuePair<string, string>>? dicFilters = JsonSerializer.Deserialize(filtersResult.value, UmbrellaKeyValuePairJsonSerializerContext.Default.ListUmbrellaKeyValuePairStringString);

				if (dicFilters is { Count: > 0 })
				{
					for (int i = 0; i < dicFilters.Count; i++)
					{
						UmbrellaKeyValuePair<string, string> kvp = dicFilters[i];
						IUmbrellaColumnDefinition<TItem>? filterableColumn = FilterableColumns?.FirstOrDefault(x => x.Filterable && (x.PropertyName?.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) is true || x.FilterMemberPathOverride?.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase) is true));

						if (filterableColumn is not null)
						{
							if (filterableColumn.FilterOptionsType is UmbrellaColumnFilterOptionsType.Boolean && bool.TryParse(kvp.Value, out bool result))
							{
								filterableColumn.FilterValue = result ? "Yes" : "No";
							}
							else if (filterableColumn.FilterControlType is UmbrellaColumnFilterType.DateRange)
							{
								// There should be another filter with the same name so we need to find that before we can assign the filter value correctly.
								UmbrellaKeyValuePair<string, string> otherFilter = dicFilters.LastOrDefault(x => x.Key == kvp.Key);

								// If the other value can't be found, just skip it.
								if (otherFilter is { Key: null, Value: null })
									continue;

								_ = dicFilters.Remove(otherFilter);
								i--;

								if (DateTime.TryParseExact(kvp.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date1) && DateTime.TryParseExact(otherFilter.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date2))
								{
									DateTime startDate = date1 <= date2 ? date1 : date2;
									DateTime endDate = date1 <= date2 ? date2 : date1;

									var dateRange = new DateTimeRange
									{
										StartDate = startDate,
										EndDate = endDate
									};

									filterableColumn.FilterValue = JsonSerializer.Serialize(dateRange, typeof(DateTimeRange), DateTimeRangeJsonSerializerContext.Default);

									if (Logger.IsEnabled(LogLevel.Debug))
										Logger.WriteDebug(new { filterableColumn.PropertyName, filterableColumn.FilterValue }, "QueryString Date Range Filter");
								}
							}
							else if (filterableColumn.FilterControlType is UmbrellaColumnFilterType.Date && DateTime.TryParseExact(kvp.Value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date))
							{
								if (filterableColumn is UmbrellaColumnDefinition<TItem, DateTime> dtColumn)
								{
									dtColumn.TypedFilterValue = date;
								}
								else if (filterableColumn is UmbrellaColumnDefinition<TItem, DateTime?> ndtColumn)
								{
									ndtColumn.TypedFilterValue = date;
								}
							}
							else
							{
								filterableColumn.FilterValue = kvp.Value;
							}

							if (Logger.IsEnabled(LogLevel.Debug))
								Logger.WriteDebug(new { filterableColumn.PropertyName, filterableColumn.FilterValue }, "Applied Filter");
						}
					}
				}
			}

			if (pageNumberResult.success)
			{
				PageNumber = pageNumberResult.value;
			}

			if (pageSizeResult.success)
			{
				PageSize = pageSizeResult.value;
			}
		}

		await UpdateGridAsync(QueryStringStateUpdateMode.None, PageNumber, PageSize);
	}

	/// <summary>
	/// Disposes this object.
	/// </summary>
	/// <param name="disposing">if set to <see langword="true"/>, disposes managed state if not already disposed.</param>
	/// <returns>A <see cref="ValueTask"/> that represents the asynchronous invocation operation.</returns>
	protected virtual async ValueTask DisposeAsync(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				await _cts.CancelAsync();
				_cts.Dispose();

				if (BrowserEventAggregator.IsValueCreated)
					await BrowserEventAggregator.Value.DisposeAsync();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		// Do not change this code. Put cleanup code in 'DisposeAsync(bool disposing)' method
		await DisposeAsync(disposing: true);
		GC.SuppressFinalize(this);
	}
}