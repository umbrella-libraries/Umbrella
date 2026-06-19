using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Grid;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.Utilities.Data.Pagination;

namespace Umbrella.AspNetCore.Blazor.Components.Pagination;

/// <summary>
/// A component used to display pagination options. Used primarily with the <see cref="UmbrellaGrid{TItem}"/> component but can be used on its own.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaPagination
{
	/// <summary>
	/// Gets or sets the logger.
	/// </summary>
	[Inject]
	public ILogger<UmbrellaPagination> Logger { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the total count.
	/// </summary>
	protected int TotalCount { get; set; }

	/// <summary>
	/// Gets or sets the size of the page. Defaults to <see cref="UmbrellaPaginationDefaults.PageSize"/>.
	/// </summary>
	protected int PageSize { get; set; } = UmbrellaPaginationDefaults.PageSize;

	/// <summary>
	/// Gets or sets the page number. Defaults to <see cref="UmbrellaPaginationDefaults.PageNumber"/>.
	/// </summary>
	protected int PageNumber { get; set; } = UmbrellaPaginationDefaults.PageNumber;

	/// <summary>
	/// Gets or sets the page size options. Defaults to <see cref="UmbrellaPaginationDefaults.PageSizeOptions"/>.
	/// </summary>
	[Parameter]
	public IReadOnlyCollection<int> PageSizeOptions { get; set; } = UmbrellaPaginationDefaults.PageSizeOptions;

	/// <summary>
	/// Gets or sets the maximum pages to show. Defaults to <see cref="UmbrellaPaginationDefaults.MaxPagesToShow"/>.
	/// </summary>
	[Parameter]
	public int MaxPagesToShow { get; set; } = UmbrellaPaginationDefaults.MaxPagesToShow;

	/// <summary>
	/// Gets or sets a value indicating whether to use small pagination styling.
	/// </summary>
	[Parameter]
	public bool SmallPagination { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the page size selection dropdown is shown. Defaults to <see langword="true"/>.
	/// </summary>
	[Parameter]
	public bool ShowPageSizeOptions { get; set; } = true;

	/// <summary>
	/// Gets or sets the querystring key used for the page number in the generated pagination hyperlink <c>href</c> values.
	/// Defaults to <c>"pageNumber"</c>.
	/// </summary>
	[Parameter]
	public string PageNumberQueryStringKey { get; set; } = "pageNumber";

	/// <summary>
	/// Gets or sets the event handler that is invoked when the pagination options have been changed as a result of user interaction.
	/// </summary>
	[Parameter]
	public EventCallback<UmbrellaPaginationEventArgs> OnOptionsChanged { get; set; }

	/// <summary>
	/// Gets or sets the model.
	/// </summary>
	protected PaginationModel Model { get; set; }

	/// <summary>
	/// Gets the unique element id used to associate the page size dropdown toggle with its menu. Computed once per instance.
	/// </summary>
	private string PaginationSizeButtonId { get; } = "a" + Guid.NewGuid().ToString("N")[1..];

	/// <summary>
	/// Updates the pagination state using the specified parameters.
	/// </summary>
	/// <param name="totalCount">The total count.</param>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	public async Task UpdateAsync(int? totalCount = null, int? pageNumber = null, int? pageSize = null)
	{
		TotalCount = totalCount ?? TotalCount;
		PageSize = pageSize ?? PageSize;
		PageNumber = pageNumber ?? PageNumber;

		await RefreshAsync(false);

		StateHasChanged();
	}

	/// <inheritdoc />
	protected override async Task OnInitializedAsync() => await RefreshAsync(false);

	/// <summary>
	/// The event handler invoked when a pagination size has been clicked.
	/// </summary>
	/// <param name="pageSize">Size of the page.</param>
	protected async Task PageSizeClickAsync(int pageSize)
	{
		PageSize = pageSize;
		PageNumber = 1;
		await RefreshAsync();
	}

	/// <summary>
	/// The event handler invoked when a pagination number has been clicked.
	/// </summary>
	/// <param name="pageNumber">The page number.</param>
	protected async Task ItemClickAsync(int pageNumber)
	{
		PageNumber = pageNumber;
		await RefreshAsync();
	}

	/// <summary>
	/// The event handler invoked when the "previous" pagination button has been clicked.
	/// </summary>
	protected async Task PreviousClickAsync()
	{
		if (Model.PreviousPageNumber.HasValue)
		{
			PageNumber = Model.PreviousPageNumber.Value;
			await RefreshAsync();
		}
	}

	/// <summary>
	/// The event handler invoked when the "next" pagination button has been clicked.
	/// </summary>
	protected async Task NextClickAsync()
	{
		if (Model.NextPageNumber.HasValue)
		{
			PageNumber = Model.NextPageNumber.Value;
			await RefreshAsync();
		}
	}

	/// <summary>
	/// The event handler invoked when the "first" pagination button has been clicked.
	/// </summary>
	protected async Task FirstClickAsync()
	{
		if (Model.FirstPageNumber.HasValue)
		{
			PageNumber = Model.FirstPageNumber.Value;
			await RefreshAsync();
		}
	}

	/// <summary>
	/// The event handler invoked when the "last" pagination button has been clicked.
	/// </summary>
	protected async Task LastClickAsync()
	{
		if (Model.LastPageNumber.HasValue)
		{
			PageNumber = Model.LastPageNumber.Value;
			await RefreshAsync();
		}
	}

	private async Task RefreshAsync(bool raiseEvent = true)
	{
		Model = new PaginationModel(TotalCount, PageNumber, PageSize, ShowPageSizeOptions, MaxPagesToShow);

		if (raiseEvent && OnOptionsChanged.HasDelegate)
			await OnOptionsChanged.InvokeAsync(new UmbrellaPaginationEventArgs(PageSize, PageNumber));
	}

	private string BuildPageHref(int pageNumber) => $"?{PageNumberQueryStringKey}={pageNumber}";
}