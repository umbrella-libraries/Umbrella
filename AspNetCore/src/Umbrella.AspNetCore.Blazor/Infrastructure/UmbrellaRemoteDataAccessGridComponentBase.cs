


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Grid;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.AspNetCore.Blazor.Infrastructure;

/// <summary>
/// A base component to be used with Blazor components that contain a single <see cref="UmbrellaGrid{TItem}"/> component.
/// </summary>
/// <typeparam name="TItemModel">The type of the item model.</typeparam>
/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
/// <typeparam name="TRepository">The type of the repository.</typeparam>
/// <seealso cref="UmbrellaGridComponentBase{TItemModel, TPaginatedResultModel}" />
public abstract class UmbrellaRemoteDataAccessGridComponentBase<TItemModel, TIdentifier, TPaginatedResultModel, TRepository> : UmbrellaReadOnlyRemoteDataAccessGridComponentBase<TItemModel, TPaginatedResultModel, TRepository>
	where TItemModel : class, IKeyedItem<TIdentifier>
	where TIdentifier : IEquatable<TIdentifier>
	where TPaginatedResultModel : PaginatedResultModel<TItemModel>
	where TRepository : class, IReadOnlyPaginatedSlimItemGenericDataRepository<TItemModel, TPaginatedResultModel>, IDeleteItemGenericDataService<TIdentifier>
{
	/// <summary>
	/// The event handler invoked when an item in the grid is to be deleted.
	/// </summary>
	/// <param name="item">The item.</param>
	public virtual async Task DeleteItemClickAsync(TItemModel item)
	{
		Guard.IsNotNull(item);

		try
		{
			string typeDisplayName = typeof(TItemModel).GetDisplayText();

			bool confirmed = await DialogUtility.ShowConfirmDangerMessageAsync($"Are you sure you want to delete this {typeDisplayName}? ", $"Delete {typeDisplayName}");

			if (confirmed)
			{
				bool success = await BeforeDeletingAsync(item);

				if (!success)
					return;

				var result = await Repository.DeleteAsync(item.Id);

				if (result.IsSuccess)
				{
					await DialogUtility.ShowSuccessMessageAsync($"The {typeDisplayName} has been successfully deleted.", $"{typeDisplayName} Deleted");
					await GridInstance.RefreshAsync();
				}
				else
				{
					await ShowOperationResultErrorMessageAsync(result, $"Delete {typeDisplayName}");
				}
			}
		}
		catch (UmbrellaConcurrencyException)
		{
			await DialogUtility.ShowConcurrencyDangerMessageAsync();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { item.Id }))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <summary>
	/// Called after showing the confirmation dialog asking the user to confirm deletion, but before the calling the <c>DeleteAsync</c> method on the repository.
	/// </summary>
	/// <param name="item">The item being deleted.</param>
	/// <returns>An awaitable task.</returns>
	protected virtual ValueTask<bool> BeforeDeletingAsync(TItemModel item) => new(true);
}