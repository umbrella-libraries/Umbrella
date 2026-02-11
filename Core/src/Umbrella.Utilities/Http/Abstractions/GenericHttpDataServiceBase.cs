
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Exceptions;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.DataAnnotations.Enumerations;
using Umbrella.Utilities.Http.Exceptions;
using Umbrella.Utilities.Text;

namespace Umbrella.Utilities.Http.Abstractions;

/// <summary>
/// A generic data service base class used to query and update resources using HTTP.
/// </summary>
public abstract class GenericHttpDataServiceBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GenericHttpDataServiceBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="remoteService">The remote service.</param>
	/// <param name="httpServiceUtility">The HTTP service utility.</param>
	/// <param name="validator">The validator.</param>
	protected GenericHttpDataServiceBase(
		ILogger logger,
		IGenericHttpService remoteService,
		IGenericHttpServiceUtility httpServiceUtility,
		IUmbrellaValidator validator)
	{
		Logger = logger;
		RemoteService = remoteService;
		HttpServiceUtility = httpServiceUtility;
		Validator = validator;
	}

	/// <summary>
	/// Gets the API URL.
	/// </summary>
	/// <remarks>This is relative to the server and should not have a leading or trailing slash, e.g. api/v1/people</remarks>
	protected abstract string ApiUrl { get; }

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the remote service.
	/// </summary>
	protected IGenericHttpService RemoteService { get; }

	/// <summary>
	/// Gets the HTTP service utility.
	/// </summary>
	protected IGenericHttpServiceUtility HttpServiceUtility { get; }

	/// <summary>
	/// Gets the validator.
	/// </summary>
	protected IUmbrellaValidator Validator { get; }

	/// <summary>
	/// Finds all slim results of <typeparamref name="TSlimItem"/> on the server using the specified parameters.
	/// </summary>
	/// <typeparam name="TPaginatedResultModel">The type of the paginated result model.</typeparam>
	/// <typeparam name="TSlimItem">The type of the slim item.</typeparam>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <param name="endpointPath">The relative API endpoint to call.</param>
	/// <param name="pageNumber">The page number.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sorters">The sorters.</param>
	/// <param name="filters">The filters.</param>
	/// <param name="filterCombinator">The filter combinator.</param>
	/// <param name="afterAllItemsLoadedCallback">The optional callback to invoke on the results.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceException" />
	protected virtual async Task<IHttpOperationResult<TPaginatedResultModel?>> GetAllSlimAsync<TPaginatedResultModel, TSlimItem, TIdentifier>(
		string endpointPath,
		int pageNumber = 0,
		int pageSize = 20,
		IEnumerable<SortExpressionDescriptor>? sorters = null,
		IEnumerable<FilterExpressionDescriptor>? filters = null,
		FilterExpressionCombinator filterCombinator = FilterExpressionCombinator.And,
		Func<IEnumerable<TSlimItem>, CancellationToken, Task>? afterAllItemsLoadedCallback = null,
		CancellationToken cancellationToken = default)
		where TIdentifier : IEquatable<TIdentifier>
		where TSlimItem : class, IKeyedItem<TIdentifier>
		where TPaginatedResultModel : PaginatedResultModel<TSlimItem>
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			var parameters = HttpServiceUtility.CreateSearchQueryParameters(pageNumber, pageSize, sorters, filters, filterCombinator);

			IHttpOperationResult<TPaginatedResultModel?> result = await RemoteService.GetAsync<TPaginatedResultModel>(ApiUrl + endpointPath, parameters, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess && result.Result is not null && afterAllItemsLoadedCallback is not null)
				await afterAllItemsLoadedCallback(result.Result.Items, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { endpointPath, pageNumber, pageSize, sorters, filters, filterCombinator }))
		{
			throw new UmbrellaDataServiceException("There was a problem finding the items using the specified parameters.", exc);
		}
	}

	/// <summary>
	/// Finds the specified resource on the remote server.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="afterItemLoadedCallback">An optional callback to be invoked on the result.</param>
	/// <param name="endpointPath">The optional relative endpoint path to call. By default this is empty and is a RESTful API call.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceException" />
	protected async Task<IHttpOperationResult<TItem?>> GetByIdAsync<TItem, TIdentifier>(
		TIdentifier id,
		CancellationToken cancellationToken,
		Func<TItem, CancellationToken, Task>? afterItemLoadedCallback = null,
		string endpointPath = "")
		where TItem : class, IKeyedItem<TIdentifier>
		where TIdentifier : IEquatable<TIdentifier>
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

			IHttpOperationResult<TItem?> result = await RemoteService.GetAsync<TItem>(ApiUrl + endpointPath, parameters, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess && result.Result is not null && afterItemLoadedCallback is not null)
				await afterItemLoadedCallback(result.Result, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id, endpointPath }))
		{
			throw new UmbrellaDataServiceException("There was a problem finding the specified item.", exc);
		}
	}

	/// <summary>
	/// POSTs the specified resource on the remote server.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TPostResult">The type of the post result.</typeparam>
	/// <param name="item">The item.</param>
	/// <param name="afterItemSavedCallback">An optional callback to be invoked after the item has been successfully POSTed.</param>
	/// <param name="endpointPath">The optional relative endpoint path to call. By default this is empty and is a RESTful API call.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceConcurrencyException" />
	/// <exception cref="UmbrellaDataServiceException" />
	protected virtual async Task<IHttpOperationResult<TPostResult?>> PostAsync<TItem, TPostResult>(
		TItem item,
		Func<TItem, TPostResult?, CancellationToken, Task>? afterItemSavedCallback = null,
		string endpointPath = "",
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(item, nameof(item));

		try
		{
			return await SaveCoreAsync(HttpMethod.Post, endpointPath, item, afterItemSavedCallback, cancellationToken).ConfigureAwait(false);
		}
		catch (UmbrellaHttpServiceConcurrencyException exc)
		{
			throw new UmbrellaDataServiceConcurrencyException("There has been a concurrency error whilst posting the specified item.", exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { endpointPath }))
		{
			throw new UmbrellaDataServiceException("There was a problem posting the specified item.", exc);
		}
	}

	/// <summary>
	/// PUTs the specified resource on the remote server.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <typeparam name="TPutResult">The type of the PUT result.</typeparam>
	/// <param name="item">The item.</param>
	/// <param name="afterItemSavedCallback">An optional callback to be invoked after the item has been successfully PUTed.</param>
	/// <param name="endpointPath">The optional relative endpoint path to call. By default this is empty and is a RESTful API call.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceConcurrencyException" />
	/// <exception cref="UmbrellaDataServiceException" />
	protected virtual async Task<IHttpOperationResult<TPutResult?>> PutAsync<TItem, TIdentifier, TPutResult>(
		TItem item,
		Func<TItem, TPutResult?, CancellationToken, Task>? afterItemSavedCallback = null,
		string endpointPath = "",
		CancellationToken cancellationToken = default)
		where TItem : class, IKeyedItem<TIdentifier>
		where TIdentifier : IEquatable<TIdentifier>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(item);

		try
		{
			return item.Id.Equals(default!)
				? throw new InvalidOperationException("The item being patched must have an Id.")
				: await SaveCoreAsync(HttpMethod.Put, endpointPath, item, afterItemSavedCallback, cancellationToken).ConfigureAwait(false);
		}
		catch (UmbrellaHttpServiceConcurrencyException exc)
		{
			throw new UmbrellaDataServiceConcurrencyException("There has been a concurrency error whilst putting the specified item.", exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { item.Id, endpointPath }))
		{
			throw new UmbrellaDataServiceException("There was a problem putting the specified item.", exc);
		}
	}

	/// <summary>
	/// PATCHes the specified resource on the remote server.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <typeparam name="TPatchResult">The type of the PATCH result.</typeparam>
	/// <param name="item">The item.</param>
	/// <param name="afterItemSavedCallback">An optional callback to be invoked after the item has been successfully PATCHed.</param>
	/// <param name="endpointPath">The optional relative endpoint path to call. By default this is empty and is a RESTful API call.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceConcurrencyException" />
	/// <exception cref="UmbrellaDataServiceException" />
	protected virtual async Task<IHttpOperationResult<TPatchResult?>> PatchAsync<TItem, TIdentifier, TPatchResult>(
		TItem item,
		Func<TItem, TPatchResult?, CancellationToken, Task>? afterItemSavedCallback = null,
		string endpointPath = "",
		CancellationToken cancellationToken = default)
		where TItem : class, IKeyedItem<TIdentifier>
		where TIdentifier : IEquatable<TIdentifier>
		where TPatchResult : class
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(item);

		try
		{
			return item.Id.Equals(default!)
				? throw new InvalidOperationException("The item being patched must have an Id.")
				: await SaveCoreAsync(HttpMethodExtras.Patch, endpointPath, item, afterItemSavedCallback, cancellationToken).ConfigureAwait(false);
		}
		catch (UmbrellaHttpServiceConcurrencyException exc)
		{
			throw new UmbrellaDataServiceConcurrencyException("There has been a concurrency error whilst patching the specified item.", exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { item.Id, endpointPath }))
		{
			throw new UmbrellaDataServiceException("There was a problem patching the specified item.", exc);
		}
	}

	/// <summary>
	/// DELETEs the specified resource from the remote server.
	/// </summary>
	/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
	/// <param name="id">The identifier.</param>
	/// <param name="afterItemDeletedCallback">The optional operation to be invoked after the item has been deleted successfully.</param>
	/// <param name="endpointPath">The optional relative endpoint path to call. By default this is empty and is a RESTful API call.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the remote operation.</returns>
	/// <exception cref="UmbrellaDataServiceConcurrencyException" />
	/// <exception cref="UmbrellaDataServiceException" />
	protected virtual async Task<IHttpOperationResult> DeleteAsync<TIdentifier>(
		TIdentifier id,
		Func<TIdentifier, CancellationToken, Task>? afterItemDeletedCallback = null,
		string endpointPath = "",
		CancellationToken cancellationToken = default)
		where TIdentifier : IEquatable<TIdentifier>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(id, nameof(id));

		try
		{
			string? strId = id.ToString();

			if (string.IsNullOrEmpty(strId))
				throw new InvalidOperationException("The id cannot be converted to a string.");

			var parameters = new Dictionary<string, string>
			{
				["id"] = strId
			};

			IHttpOperationResult result = await RemoteService.DeleteAsync(ApiUrl, parameters, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess && afterItemDeletedCallback is not null)
				await afterItemDeletedCallback(id, cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (UmbrellaHttpServiceConcurrencyException exc)
		{
			throw new UmbrellaDataServiceConcurrencyException("There has been a concurrency error whilst deleting the specified item.", exc);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id, endpointPath }))
		{
			throw new UmbrellaDataServiceException("There was a problem deleting the specified item.", exc);
		}
	}

	/// <summary>
	/// Sanitizes the item.
	/// </summary>
	/// <param name="item">The item.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task to await sanitization of the item.</returns>
	protected virtual Task SanitizeItemAsync(object item, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (item is IUmbrellaTrimmable trimmable)
			trimmable.TrimAllStringProperties();

		return Task.CompletedTask;
	}

	/// <summary>
	/// By defaults this calls into <see cref="IUmbrellaValidator.ValidateItemAsync(object, ValidationType)" />.
	/// Overriding this method allows you to perform custom validation on the item.
	/// </summary>
	/// <param name="item">The item.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> used to await completion of this operation.</returns>
	protected async virtual Task<(bool isValid, IReadOnlyCollection<ValidationResult> results)> ValidateItemAsync(object item, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return await Validator.ValidateItemAsync(item, ValidationType.Deep);
	}

	private async Task<IHttpOperationResult<TResult?>> SaveCoreAsync<TItem, TResult>(
		HttpMethod method,
		string endpointPath,
		TItem item,
		Func<TItem, TResult?, CancellationToken, Task>? afterItemSavedCallback,
		CancellationToken cancellationToken)
	{
		if (item is not null)
		{
			await SanitizeItemAsync(item, cancellationToken).ConfigureAwait(false);

			var (isValid, results) = await ValidateItemAsync(item, cancellationToken).ConfigureAwait(false);

			if (!isValid)
				return new HttpOperationResult<TResult>(results);
		}

		string url = ApiUrl + endpointPath;

		IHttpOperationResult<TResult?> result = method switch
		{
			var _ when method == HttpMethod.Post => await RemoteService.PostAsync<TItem, TResult>(url, item, cancellationToken: cancellationToken).ConfigureAwait(false),
			var _ when method == HttpMethod.Put => await RemoteService.PutAsync<TItem, TResult>(url, item, cancellationToken: cancellationToken).ConfigureAwait(false),
			var _ when method == HttpMethodExtras.Patch => await RemoteService.PatchAsync<TItem, TResult>(url, item, cancellationToken: cancellationToken).ConfigureAwait(false),
			_ => throw new NotSupportedException()
		};

		if (result.IsSuccess && item is not null && afterItemSavedCallback is not null)
			await afterItemSavedCallback(item, result.Result, cancellationToken).ConfigureAwait(false);

		return result;
	}
}