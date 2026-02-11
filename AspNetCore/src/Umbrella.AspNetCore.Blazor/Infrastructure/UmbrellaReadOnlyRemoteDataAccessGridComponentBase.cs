
using System.Diagnostics.CodeAnalysis;
using Umbrella.AspNetCore.Blazor.Components.Grid;
using Umbrella.AspNetCore.Blazor.Constants;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Infrastructure;

/// <summary>
/// A base component to be used with Blazor components that contain a single <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
/// <typeparam name="TItemModel">The type of the item model.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
/// <typeparam name="TRepository">The type of the repository.</typeparam>
/// <seealso cref="UmbrellaGridComponentBase{TItemModel, TPaginatedResultModel}" />
public abstract class UmbrellaReadOnlyRemoteDataAccessGridComponentBase<TItemModel, TPaginatedResultModel, TRepository> : UmbrellaGridComponentBase<TItemModel, TPaginatedResultModel>
	where TItemModel : class
	where TPaginatedResultModel : PaginatedResultModel<TItemModel>
	where TRepository : class, IReadOnlyPaginatedSlimItemGenericDataRepository<TItemModel, TPaginatedResultModel>
{
	/// <summary>
	/// Gets or sets the repository.
	/// </summary>
	[Inject]
	protected TRepository Repository { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <inheritdoc/>
	protected override Task<IOperationResult<TPaginatedResultModel?>> LoadPaginatedResultModelAsync(int pageNumber, int pageSize, IEnumerable<SortExpressionDescriptor>? sorters = null, IEnumerable<FilterExpressionDescriptor>? filters = null, CancellationToken cancellationToken = default) => Repository.FindAllSlimAsync(pageNumber, pageSize, sorters: sorters, filters: filters, cancellationToken: cancellationToken);
}