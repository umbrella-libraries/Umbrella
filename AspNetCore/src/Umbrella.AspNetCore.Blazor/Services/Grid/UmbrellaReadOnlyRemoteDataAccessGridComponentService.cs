using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Grid;
using Umbrella.AspNetCore.Blazor.Services.Grid.Abstractions;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Services.Grid;

/// <summary>
/// A service that can be used with Blazor components that contain a <see cref="UmbrellaGrid{TItem}"/> component.
/// Multiple instances of this service can be used to power multiple grids contained within a single Blazor component.
/// </summary>
/// <typeparam name="TItemModel">The type of the item model.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
/// <typeparam name="TRepository">The type of the repository.</typeparam>
/// <seealso cref="UmbrellaGridComponentService{TItemModel, TPaginatedResultModel}" />
/// <seealso cref="IUmbrellaReadOnlyRemoteDataAccessGridComponentService{TItemModel, TPaginatedResultModel, TRepository}" />
public class UmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository> : UmbrellaGridComponentService<TItemModel, TPaginatedResultModel>, IUmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository>
	where TItemModel : class
	where TPaginatedResultModel : PaginatedResultModel<TItemModel>
	where TRepository : class, IReadOnlyPaginatedSlimItemGenericDataRepository<TItemModel, TPaginatedResultModel>
{
	internal UmbrellaReadOnlyRemoteDataAccessGridComponentService(
		ILogger<UmbrellaGridComponentService<TItemModel, TPaginatedResultModel>> logger,
		IUmbrellaDialogService dialogUtility,
		TRepository repository)
		: base(logger, dialogUtility)
	{
		Repository = repository;
		LoadPaginatedResultModelDelegate = (pageNumber, pageSize, sorters, filters, cancellationToken) => Repository.FindAllSlimAsync(pageNumber, pageSize, sorters: sorters, filters: filters, cancellationToken: cancellationToken);
	}

	/// <inheritdoc />
	public TRepository Repository { get; }
}