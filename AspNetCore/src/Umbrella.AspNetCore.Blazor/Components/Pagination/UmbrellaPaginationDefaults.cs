namespace Umbrella.AspNetCore.Blazor.Components.Pagination;

/// <summary>
/// Specifies the pagination defaults.
/// </summary>
public static class UmbrellaPaginationDefaults
{
	/// <summary>
	/// The default page number.
	/// </summary>
	public const int PageNumber = 1;

	/// <summary>
	/// The default page size.
	/// </summary>
	public const int PageSize = 50;

	/// <summary>
	/// The default maximum number of page links to show.
	/// </summary>
	public const int MaxPagesToShow = 5;

	/// <summary>
	/// The default page size options of 10, 25, and 50.
	/// </summary>
	public static readonly IReadOnlyCollection<int> PageSizeOptions = new[] { 10, 25, 50 };
}