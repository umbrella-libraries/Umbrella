using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Services.Grid.Abstractions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Services.Abstractions;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Services.Grid;

/// <summary>
/// A factory for creating instances of <see cref="UmbrellaGridComponentService{TItemModel, TPaginatedResultModel}"/>
/// and <see cref="UmbrellaRemoteDataAccessGridComponentService{TItemModel, TIdentifier, TPaginatedResultModel, TRepository}"/>.
/// </summary>
public class UmbrellaGridComponentServiceFactory : IUmbrellaGridComponentServiceFactory
{
	private readonly ILogger _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IServiceProvider _serviceProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaGridComponentServiceFactory"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="serviceProvider">The service provider.</param>
	public UmbrellaGridComponentServiceFactory(
		ILoggerFactory loggerFactory,
		IServiceProvider serviceProvider)
	{
		_logger = loggerFactory.CreateLogger<UmbrellaGridComponentServiceFactory>();
		_loggerFactory = loggerFactory;
		_serviceProvider = serviceProvider;
	}

	/// <inheritdoc/>
	public IUmbrellaGridComponentService<TItemModel, TPaginatedResultModel> CreateUmbrellaGridComponentService<TItemModel, TPaginatedResultModel>(
		Func<int, int, IEnumerable<SortExpressionDescriptor>?, IEnumerable<FilterExpressionDescriptor>?, CancellationToken, Task<IOperationResult<TPaginatedResultModel?>>> loadPaginatedResultModelDelegate,
		Action stateHasChangedDelegate,
		bool callGridStateHasChangedOnRefresh = true,
		IReadOnlyCollection<FilterExpressionDescriptor>? initialFilterExpressions = null)
		where TItemModel : notnull
		where TPaginatedResultModel : PaginatedResultModel<TItemModel>
	{
		try
		{
			var logger = _loggerFactory.CreateLogger<UmbrellaGridComponentService<TItemModel, TPaginatedResultModel>>();
			var dialogUtility = _serviceProvider.GetRequiredService<IUmbrellaDialogService>();

			var service = new UmbrellaGridComponentService<TItemModel, TPaginatedResultModel>(logger, dialogUtility)
			{
				CallGridStateHasChangedOnRefresh = callGridStateHasChangedOnRefresh,
				LoadPaginatedResultModelDelegate = loadPaginatedResultModelDelegate,
				StateHasChangedDelegate = stateHasChangedDelegate
			};

			if (initialFilterExpressions is not null)
				service.InitialFilterExpressions = initialFilterExpressions;

			return service;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { callGridStateHasChangedOnRefresh }))
		{
			throw new UmbrellaBlazorException("There has been a problem creating the service.", exc);
		}
	}

	/// <inheritdoc/>
	public IUmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository> CreateUmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository>(
		Action stateHasChangedDelegate,
		Func<int, int, IEnumerable<SortExpressionDescriptor>?, IEnumerable<FilterExpressionDescriptor>?, CancellationToken, Task<IOperationResult<TPaginatedResultModel?>>>? loadPaginatedResultModelDelegate = null,
		bool callGridStateHasChangedOnRefresh = true,
		IReadOnlyCollection<FilterExpressionDescriptor>? initialFilterExpressions = null)
		where TItemModel : class
		where TPaginatedResultModel : PaginatedResultModel<TItemModel>
		where TRepository : class, IReadOnlyPaginatedSlimItemGenericDataRepository<TItemModel, TPaginatedResultModel>
	{
		try
		{
			var logger = _loggerFactory.CreateLogger<UmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository>>();
			var dialogUtility = _serviceProvider.GetRequiredService<IUmbrellaDialogService>();
			var repository = _serviceProvider.GetRequiredService<TRepository>();

			var service = new UmbrellaReadOnlyRemoteDataAccessGridComponentService<TItemModel, TPaginatedResultModel, TRepository>(logger, dialogUtility, repository)
			{
				CallGridStateHasChangedOnRefresh = callGridStateHasChangedOnRefresh,
				StateHasChangedDelegate = stateHasChangedDelegate
			};

			if (initialFilterExpressions is not null)
				service.InitialFilterExpressions = initialFilterExpressions;

			if (loadPaginatedResultModelDelegate is not null)
				service.LoadPaginatedResultModelDelegate = loadPaginatedResultModelDelegate;

			return service;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { callGridStateHasChangedOnRefresh }))
		{
			throw new UmbrellaBlazorException("There has been a problem creating the service.", exc);
		}
	}

	/// <inheritdoc/>
	public IUmbrellaRemoteDataAccessGridComponentService<TItemModel, TIdentifier, TPaginatedResultModel, TRepository> CreateUmbrellaRemoteDataAccessGridComponentService<TItemModel, TIdentifier, TPaginatedResultModel, TRepository>(
		Action stateHasChangedDelegate,
		Func<int, int, IEnumerable<SortExpressionDescriptor>?, IEnumerable<FilterExpressionDescriptor>?, CancellationToken, Task<IOperationResult<TPaginatedResultModel?>>>? loadPaginatedResultModelDelegate = null,
		bool callGridStateHasChangedOnRefresh = true,
		IReadOnlyCollection<FilterExpressionDescriptor>? initialFilterExpressions = null,
		Func<TItemModel, ValueTask<bool>>? beforeDeletingDelegate = null)
		where TItemModel : class, IKeyedItem<TIdentifier>
		where TIdentifier : IEquatable<TIdentifier>
		where TPaginatedResultModel : PaginatedResultModel<TItemModel>
		where TRepository : class, IReadOnlyPaginatedSlimItemGenericDataRepository<TItemModel, TPaginatedResultModel>, IDeleteItemGenericDataService<TIdentifier>
	{
		try
		{
			var logger = _loggerFactory.CreateLogger<UmbrellaRemoteDataAccessGridComponentService<TItemModel, TIdentifier, TPaginatedResultModel, TRepository>>();
			var dialogUtility = _serviceProvider.GetRequiredService<IUmbrellaDialogService>();
			var repository = _serviceProvider.GetRequiredService<TRepository>();

			var service = new UmbrellaRemoteDataAccessGridComponentService<TItemModel, TIdentifier, TPaginatedResultModel, TRepository>(logger, dialogUtility, repository)
			{
				CallGridStateHasChangedOnRefresh = callGridStateHasChangedOnRefresh,
				StateHasChangedDelegate = stateHasChangedDelegate,
				BeforeDeletingDelegate = beforeDeletingDelegate
			};

			if (initialFilterExpressions is not null)
				service.InitialFilterExpressions = initialFilterExpressions;

			if (loadPaginatedResultModelDelegate is not null)
				service.LoadPaginatedResultModelDelegate = loadPaginatedResultModelDelegate;

			return service;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { callGridStateHasChangedOnRefresh }))
		{
			throw new UmbrellaBlazorException("There has been a problem creating the service.", exc);
		}
	}
}