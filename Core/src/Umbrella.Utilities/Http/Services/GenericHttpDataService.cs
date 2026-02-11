


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Data.Services.Exceptions;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.Utilities.Http.Services;

// TODO: The base class class should be flattened into this one. Although that would cause issues.
// I need better names. Maybe GenericHttpCrudDataService or something? No, something more elegant.
// Maybe 

/// <summary>
/// A generic repository used to query and update a remote resource over HTTP.
/// </summary>
/// <typeparam name="TItem">The type of the item.</typeparam>
/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
/// <typeparam name="TSlimItem">The type of the slim item.</typeparam>
/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
/// <typeparam name="TCreateItem">The type of the create item.</typeparam>
/// <typeparam name="TCreateResult">The type of the create result.</typeparam>
/// <typeparam name="TUpdateItem">The type of the update item.</typeparam>
/// <typeparam name="TUpdateResult">The type of the update result.</typeparam>
public abstract class GenericHttpDataService<TItem, TIdentifier, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult> : GenericHttpDataServiceBase, IGenericDataService<TItem, TIdentifier, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult>
	where TItem : class, IKeyedItem<TIdentifier>
	where TSlimItem : class, IKeyedItem<TIdentifier>
	where TUpdateItem : class, IKeyedItem<TIdentifier>
	where TIdentifier : IEquatable<TIdentifier>
	where TPaginatedResultModel : PaginatedResultModel<TSlimItem>
{
	/// <summary>
	/// The endpoint called by the <see cref="FindAllSlimAsync(int, int, IEnumerable{SortExpressionDescriptor}?, IEnumerable{FilterExpressionDescriptor}?, FilterExpressionCombinator, CancellationToken)"/>
	/// method.
	/// </summary>
	/// <remarks>
	/// Defaults to /SearchSlim. This path must start with a leading /.
	/// </remarks>
	protected virtual string FindAllSlimEndpoint { get; } = "/SearchSlim";

	/// <summary>
	/// Initializes a new instance of the <see cref="GenericHttpDataService{TItem, TIdentifier, TSlimItem, TPaginatedResultModel, TCreateItem, TCreateResult, TUpdateItem, TUpdateResult}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="httpService">The HTTP service.</param>
	/// <param name="httpServiceUtility">The HTTP service utility.</param>
	/// <param name="validator">The validator.</param>
	protected GenericHttpDataService(
		ILogger logger,
		IGenericHttpService httpService,
		IGenericHttpServiceUtility httpServiceUtility,
		IUmbrellaValidator validator) : base(logger, httpService, httpServiceUtility, validator)
	{
	}

	/// <inheritdoc />
	public virtual async Task<IOperationResult<TItem?>> FindByIdAsync(TIdentifier id, CancellationToken cancellationToken = default)
		=> await GetByIdAsync<TItem, TIdentifier>(id, cancellationToken, AfterItemLoadedAsync);

	/// <inheritdoc />
	public virtual async Task<IOperationResult<TPaginatedResultModel?>> FindAllSlimAsync(int pageNumber = 0, int pageSize = 20, IEnumerable<SortExpressionDescriptor>? sorters = null, IEnumerable<FilterExpressionDescriptor>? filters = null, FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.And, CancellationToken cancellationToken = default)
		=> await GetAllSlimAsync<TPaginatedResultModel, TSlimItem, TIdentifier>(FindAllSlimEndpoint, pageNumber, pageSize, sorters, filters, filterCombinator, AfterAllItemsLoadedAsync, cancellationToken);

	/// <inheritdoc />
	public virtual async Task<IOperationResult<int>> FindTotalCountAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await RemoteService.GetAsync<int>(ApiUrl + "/TotalCount", cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataServiceException("There was a problem finding the total count.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IOperationResult<bool>> ExistsByIdAsync(TIdentifier id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(id);

		try
		{
			string? strId = id.ToString();

			if (string.IsNullOrEmpty(strId))
				throw new InvalidOperationException("The id cannot be converted to a string.");

			var parameters = new Dictionary<string, string>
			{
				["id"] = strId
			};

			return await RemoteService.GetAsync<bool>(ApiUrl + "/ExistsById", parameters, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataServiceException("There was a problem determining if the specified item exists.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IOperationResult<TCreateResult?>> CreateAsync(TCreateItem item, CancellationToken cancellationToken = default)
		=> await PostAsync<TCreateItem, TCreateResult>(item, AfterItemCreatedAsync, cancellationToken: cancellationToken);

	/// <inheritdoc />
	public virtual async Task<IOperationResult<TUpdateResult?>> UpdateAsync(TUpdateItem item, CancellationToken cancellationToken = default)
		=> await PutAsync<TUpdateItem, TIdentifier, TUpdateResult>(item, AfterItemUpdatedAsync, cancellationToken: cancellationToken);

	/// <inheritdoc />
	public virtual async Task<IOperationResult> DeleteAsync(TIdentifier id, CancellationToken cancellationToken = default)
		=> await DeleteAsync(id, AfterItemDeletedAsync, cancellationToken: cancellationToken);

	/// <summary>
	/// Override this in a derived type to perform an operation on the item after it has been loaded.
	/// </summary>
	/// <param name="item">The item.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected virtual Task AfterItemLoadedAsync(TItem item, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	///  Override this in a derived type to perform an operation on the items after they have been loaded.
	/// </summary>
	/// <param name="items">The items.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	protected virtual Task AfterAllItemsLoadedAsync(IEnumerable<TSlimItem> items, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform any work after the item has been created.
	/// </summary>
	/// <param name="item">The item.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected virtual Task AfterItemCreatedAsync(TCreateItem item, TCreateResult? result, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform any work after the item has been updated.
	/// </summary>
	/// <param name="item">The item.</param>
	/// <param name="result">The result.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected virtual Task AfterItemUpdatedAsync(TUpdateItem item, TUpdateResult? result, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Overriding this method allows you to perform any work after the item has been deleted.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected virtual Task AfterItemDeletedAsync(TIdentifier id, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}
}