using System.Linq.Expressions;
using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Exceptions;
using Umbrella.DataAccess.EntityFrameworkCore.Extensions;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Filtering;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Data.Sorting;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Security.Extensions;

namespace Umbrella.DataAccess.EntityFrameworkCore;

// TODO: SRP - Remove security checks??

/// <summary>
/// Serves as the base class for repositories which provide read-only access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <seealso cref="ReadOnlyGenericDbRepository{TEntity, TDbContext, RepoOptions, Int32}" />
public abstract class ReadOnlyGenericDbRepository<TEntity, TDbContext> : ReadOnlyGenericDbRepository<TEntity, TDbContext, RepoOptions, int>
	where TEntity : class, IEntity<int>
	where TDbContext : DbContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyGenericDbRepository{TEntity, TDbContext}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	protected ReadOnlyGenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer)
		: base(dbContext, logger, lookupNormalizer)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide read-only access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <seealso cref="T:Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository{TEntity, TDbContext, Umbrella.DataAccess.Abstractions.RepoOptions, System.Int32}" />
public abstract class ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions> : ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions, int>
	where TEntity : class, IEntity<int>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyGenericDbRepository{TEntity, TDbContext, TRepoOptions}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	protected ReadOnlyGenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer)
		: base(dbContext, logger, lookupNormalizer)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide read-only access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
/// <seealso cref="T:Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository{TEntity, TDbContext, Umbrella.DataAccess.Abstractions.RepoOptions, System.Int32}" />
public abstract class ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey> : ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, int>
	where TEntity : class, IEntity<TEntityKey>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyGenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	protected ReadOnlyGenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer)
		: base(dbContext, logger, lookupNormalizer)
	{
	}
}

/// <summary>
/// Serves as the base class for repositories which provide read-only access to entities stored in a database accessed using Entity Framework Core.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
/// <typeparam name="TRepoOptions">The type of the repo options.</typeparam>
/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
/// <typeparam name="TUserAuditKey">The type of the user audit key.</typeparam>
/// <seealso cref="T:Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository{TEntity, TDbContext, Umbrella.DataAccess.Abstractions.RepoOptions, System.Int32}" />
/// <seealso cref="T:Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository{TEntity, TDbContext, Umbrella.DataAccess.Abstractions.RepoOptions, System.Int32}" />
public abstract class ReadOnlyGenericDbRepository<TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey> : IReadOnlyGenericDbRepository<TEntity, TRepoOptions, TEntityKey>
	where TEntity : class, IEntity<TEntityKey>
	where TDbContext : DbContext
	where TRepoOptions : RepoOptions, new()
	where TEntityKey : IEquatable<TEntityKey>
{
	#region Private Static Members
	private static IReadOnlyCollection<string>? _validFilterPaths;
	#endregion

	#region Protected Static Properties
	/// <summary>
	/// Gets the default repo options.
	/// </summary>
	protected static TRepoOptions DefaultRepoOptions { get; } = new TRepoOptions();
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the database context.
	/// </summary>
	protected Lazy<TDbContext> Context { get; }

	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the lookup normalizer.
	/// </summary>
	protected IDataLookupNormalizer LookupNormalizer { get; }

	/// <summary>
	/// Gets the <see cref="IQueryable{TEntity}"/> from the database context for the current <typeparamref name="TEntity"/> type
	/// with any query filter logic applied by the <see cref="ApplyQueryFilter(IQueryable{TEntity})"/> method which by default does
	/// nothing unless overridden in a derived type.
	/// </summary>
	protected IQueryable<TEntity> Items => ApplyQueryFilter(Context.Value.Set<TEntity>());

	/// <summary>
	/// Gets the current user identifier.
	/// </summary>
	protected TUserAuditKey? CurrentUserId => ClaimsPrincipal.Current is not null ? ClaimsPrincipal.Current.GetId<TUserAuditKey>() : default;
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="ReadOnlyGenericDbRepository{TEntity, TDbContext, TRepoOptions, TEntityKey, TUserAuditKey}"/> class.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	protected ReadOnlyGenericDbRepository(
		Lazy<TDbContext> dbContext,
		ILogger logger,
		IDataLookupNormalizer lookupNormalizer)
	{
		Context = dbContext;
		Logger = logger;
		LookupNormalizer = lookupNormalizer;
	}
	#endregion

	#region IReadOnlyGenericRepository Members
	/// <inheritdoc />
	public virtual async Task<PaginatedResultModel<TEntity>> FindAllAsync(
		int pageNumber = 0,
		int pageSize = 20,
		bool trackChanges = false,
		IncludeMap<TEntity>? map = null!,
		IEnumerable<SortExpression<TEntity>>? sortExpressions = null,
		IEnumerable<FilterExpression<TEntity>>? filterExpressions = null,
		FilterExpressionCombinator filterExpressionCombinator = FilterExpressionCombinator.And,
		TRepoOptions? repoOptions = null,
		IEnumerable<RepoOptions>? childOptions = null,
		Expression<Func<TEntity, bool>>? coreFilterExpression = null,
		IEnumerable<Expression<Func<TEntity, bool>>>? additionalFilterExpressions = null,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await FindAllCoreAsync(pageNumber, pageSize, trackChanges, map, sortExpressions, filterExpressions, filterExpressionCombinator, repoOptions, childOptions, coreFilterExpression, additionalFilterExpressions, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { pageNumber, pageSize, trackChanges, map, sortExpressions = sortExpressions?.ToSortExpressionDescriptors(), filterExpressions = filterExpressions?.ToFilterExpressionDescriptors(), filterExpressionCombinator, repoOptions, childOptions }))
		{
			throw new UmbrellaDataAccessException("There has been a problem retrieving all items using the specified parameters.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IReadOnlyCollection<TEntity>> FindAllByIdListAsync(IEnumerable<TEntityKey> entityKeys, bool trackChanges = false, IncludeMap<TEntity>? map = null, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await Items
				.Where(x => entityKeys.Contains(x.Id))
				.IncludeMap(map)
				.TrackChanges(trackChanges)
				.ToArrayAsync(cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { entityKeys, trackChanges, map, repoOptions, childOptions }))
		{
			throw new UmbrellaDataAccessException("There has been a problem retrieving all items using the specified parameters.", exc);
		}
	}

	/// <summary>
	/// Finds all entities using the specified parameters.
	/// </summary>
	/// <param name="pageNumber">The page number. Defaults to zero. Pagination will only be applied when this is greater than zero.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="trackChanges">if set to <c>true</c>, tracking entries are created in the database context (where supported).</param>
	/// <param name="map">The include map to specify related entities to load at the same time.</param>
	/// <param name="sortExpressions">The sort expressions.</param>
	/// <param name="filterExpressions">The filter expressions.</param>
	/// <param name="filterExpressionCombinator">The filter expression combinator.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child repo options.</param>
	/// <param name="coreFilterExpression">An additional filter expression to be applied to the query before the <paramref name="filterExpressions"/> and any additional <paramref name="additionalFilterExpressions"/> are applied.</param>
	/// <param name="additionalFilterExpressions">Optional additional filter expressions that are too complex to model using the <see cref="FilterExpression{TItem}"/> type.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The paginated results.</returns>
	protected virtual async Task<PaginatedResultModel<TEntity>> FindAllCoreAsync(
		int pageNumber = 0,
		int pageSize = 20,
		bool trackChanges = false,
		IncludeMap<TEntity>? map = null!,
		IEnumerable<SortExpression<TEntity>>? sortExpressions = null,
		IEnumerable<FilterExpression<TEntity>>? filterExpressions = null,
		FilterExpressionCombinator filterExpressionCombinator = FilterExpressionCombinator.And,
		TRepoOptions? repoOptions = null,
		IEnumerable<RepoOptions>? childOptions = null,
		Expression<Func<TEntity, bool>>? coreFilterExpression = null,
		IEnumerable<Expression<Func<TEntity, bool>>>? additionalFilterExpressions = null,
		CancellationToken cancellationToken = default)
	{
		repoOptions ??= DefaultRepoOptions;
		ThrowIfFiltersInvalid(filterExpressions);

		var filteredQuery = Items;

		if (coreFilterExpression is not null)
			filteredQuery = filteredQuery.Where(coreFilterExpression);

		filteredQuery = filteredQuery.ApplyFilterExpressions(filterExpressions, filterExpressionCombinator, additionalFilterExpressions);

		var results = await filteredQuery
			.ApplySortExpressions(sortExpressions, new SortExpression<TEntity>(x => x.Id, SortDirection.Ascending))
			.IncludeMap(map)
			.Select(x => new { Entity = x, TotalCount = filteredQuery.Count() })
			.ApplyPagination(pageNumber, pageSize)
			.TrackChanges(trackChanges)
			.ToArrayAsync(cancellationToken)
			.ConfigureAwait(false);

		int totalCount = results.FirstOrDefault()?.TotalCount ?? await filteredQuery.CountAsync(cancellationToken).ConfigureAwait(false);
		var entities = results.Select(x => x.Entity).ToList();

		await AfterAllItemsLoadedAsync(entities, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

		return new PaginatedResultModel<TEntity>(entities, pageNumber, pageSize, totalCount);
	}

	/// <summary>
	/// Finds all shaped entities using the specified parameters.
	/// </summary>
	/// <typeparam name="TShapedEntity">The type of the shaped entity.</typeparam>
	/// <param name="shapedEntitySelector">The shaped entity selector.</param>
	/// <param name="pageNumber">The page number. Defaults to zero. Pagination will only be applied when this is greater than zero.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sortExpressions">The sort expressions.</param>
	/// <param name="entityFilterExpressions">The entity filter expressions.</param>
	/// <param name="shapedFilterExpressions">The shaped filter expressions.</param>
	/// <param name="filterExpressionCombinator">The filter expression combinator.</param>
	/// <param name="coreEntityFilterExpression">An additional entity filter expression to be applied to the query before the <paramref name="entityFilterExpressions"/> and any additional <paramref name="additionalEntityFilterExpressions"/> are applied.</param>
	/// <param name="coreShapedFilterExpression">An additional shaped filter expression to be applied to the query before the <paramref name="entityFilterExpressions"/> and any additional <paramref name="additionalShapedFilterExpressions"/> are applied.</param>
	/// <param name="additionalEntityFilterExpressions">Optional additional entity filter expressions that are too complex to model using the <see cref="FilterExpression{TItem}"/> type.</param>
	/// <param name="additionalShapedFilterExpressions">Optional additional shaped filter expressions that are too complex to model using the <see cref="FilterExpression{TItem}"/> type.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The paginated results.</returns>
	protected virtual Task<PaginatedResultModel<TShapedEntity>> FindAllShapedAsync<TShapedEntity>(
		Expression<Func<TEntity, TShapedEntity>> shapedEntitySelector,
		int pageNumber = 0,
		int pageSize = 20,
		IEnumerable<SortExpression<TShapedEntity>>? sortExpressions = null,
		IEnumerable<FilterExpression<TEntity>>? entityFilterExpressions = null,
		IEnumerable<FilterExpression<TShapedEntity>>? shapedFilterExpressions = null,
		FilterExpressionCombinator filterExpressionCombinator = FilterExpressionCombinator.And,
		Expression<Func<TEntity, bool>>? coreEntityFilterExpression = null,
		Expression<Func<TShapedEntity, bool>>? coreShapedFilterExpression = null,
		IEnumerable<Expression<Func<TEntity, bool>>>? additionalEntityFilterExpressions = null,
		IEnumerable<Expression<Func<TShapedEntity, bool>>>? additionalShapedFilterExpressions = null,
		CancellationToken cancellationToken = default)
		where TShapedEntity : IEntity<TEntityKey>
	{
		return FindAllShapedAsync(
			shapedEntitySelector,
			new SortExpression<TShapedEntity>(x => x.Id, SortDirection.Ascending),
			pageNumber,
			pageSize,
			sortExpressions,
			entityFilterExpressions,
			shapedFilterExpressions,
			filterExpressionCombinator,
			coreEntityFilterExpression,
			coreShapedFilterExpression,
			additionalEntityFilterExpressions,
			additionalShapedFilterExpressions,
			cancellationToken);
	}

	/// <summary>
	/// Finds all shaped entities using the specified parameters.
	/// </summary>
	/// <typeparam name="TShapedEntity">The type of the shaped entity.</typeparam>
	/// <param name="shapedEntitySelector">The shaped entity selector.</param>
	/// <param name="initialSortExpression">The initial sort expression.</param>
	/// <param name="pageNumber">The page number. Defaults to zero. Pagination will only be applied when this is greater than zero.</param>
	/// <param name="pageSize">Size of the page.</param>
	/// <param name="sortExpressions">The sort expressions.</param>
	/// <param name="entityFilterExpressions">The entity filter expressions.</param>
	/// <param name="shapedFilterExpressions">The shaped filter expressions.</param>
	/// <param name="filterExpressionCombinator">The filter expression combinator.</param>
	/// <param name="coreEntityFilterExpression">An additional entity filter expression to be applied to the query before the <paramref name="entityFilterExpressions"/> and any additional <paramref name="additionalEntityFilterExpressions"/> are applied.</param>
	/// <param name="coreShapedFilterExpression">An additional shaped filter expression to be applied to the query before the <paramref name="entityFilterExpressions"/> and any additional <paramref name="additionalShapedFilterExpressions"/> are applied.</param>
	/// <param name="additionalEntityFilterExpressions">Optional additional entity filter expressions that are too complex to model using the <see cref="FilterExpression{TItem}"/> type.</param>
	/// <param name="additionalShapedFilterExpressions">Optional additional shaped filter expressions that are too complex to model using the <see cref="FilterExpression{TItem}"/> type.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The paginated results.</returns>
	protected virtual async Task<PaginatedResultModel<TShapedEntity>> FindAllShapedAsync<TShapedEntity>(
		Expression<Func<TEntity, TShapedEntity>> shapedEntitySelector,
		SortExpression<TShapedEntity> initialSortExpression,
		int pageNumber = 0,
		int pageSize = 20,
		IEnumerable<SortExpression<TShapedEntity>>? sortExpressions = null,
		IEnumerable<FilterExpression<TEntity>>? entityFilterExpressions = null,
		IEnumerable<FilterExpression<TShapedEntity>>? shapedFilterExpressions = null,
		FilterExpressionCombinator filterExpressionCombinator = FilterExpressionCombinator.And,
		Expression<Func<TEntity, bool>>? coreEntityFilterExpression = null,
		Expression<Func<TShapedEntity, bool>>? coreShapedFilterExpression = null,
		IEnumerable<Expression<Func<TEntity, bool>>>? additionalEntityFilterExpressions = null,
		IEnumerable<Expression<Func<TShapedEntity, bool>>>? additionalShapedFilterExpressions = null,
		CancellationToken cancellationToken = default)
	{
		var filteredQuery = Items;

		if (coreEntityFilterExpression is not null)
			filteredQuery = filteredQuery.Where(coreEntityFilterExpression);

		filteredQuery = filteredQuery.ApplyFilterExpressions(entityFilterExpressions, filterExpressionCombinator, additionalEntityFilterExpressions);

		var shapedQuery = filteredQuery.Select(shapedEntitySelector);

		if (coreShapedFilterExpression is not null)
			shapedQuery = shapedQuery.Where(coreShapedFilterExpression);

		shapedQuery = shapedQuery.ApplyFilterExpressions(shapedFilterExpressions, filterExpressionCombinator, additionalShapedFilterExpressions);

		var results = await shapedQuery
			.ApplySortExpressions(sortExpressions, initialSortExpression)
			.Select(x => new { Entity = x, TotalCount = shapedQuery.Count() })
			.ApplyPagination(pageNumber, pageSize)
			.ToArrayAsync(cancellationToken)
			.ConfigureAwait(false);

		int totalCount = results.FirstOrDefault()?.TotalCount ?? await shapedQuery.CountAsync(cancellationToken).ConfigureAwait(false);
		var entities = results.Select(x => x.Entity).ToArray();

		return new PaginatedResultModel<TShapedEntity>(entities, pageNumber, pageSize, totalCount);
	}

	/// <inheritdoc />
	public virtual async Task<TEntity?> FindByIdAsync(TEntityKey id, bool trackChanges = false, IncludeMap<TEntity>? map = null, TRepoOptions? repoOptions = null, IEnumerable<RepoOptions>? childOptions = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		repoOptions ??= DefaultRepoOptions;

		try
		{
			// TODO: Benchmark accessing using FirstOrDefault, SingleOrDefault and FindAsync
			var entity = await Items.TrackChanges(trackChanges).IncludeMap(map).SingleOrDefaultAsync(x => x.Id.Equals(id), cancellationToken).ConfigureAwait(false);

			if (entity is null)
				return null;

			await AfterItemLoadedAsync(entity, repoOptions, childOptions, cancellationToken).ConfigureAwait(false);

			return entity;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id, trackChanges, map, repoOptions, childOptions }))
		{
			throw new UmbrellaDataAccessException("There has been a problem retrieving the item with the specified id.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<int> FindTotalCountAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await Items.CountAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaDataAccessException("There has been a problem retrieving the count of all items.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> ExistsByIdAsync(TEntityKey id, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await Items.AnyAsync(x => x.Id.Equals(id), cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { id }))
		{
			throw new UmbrellaDataAccessException("There has been a problem determining if the item exists.", exc);
		}
	}
	#endregion

	#region Protected Methods
	/// <summary>
	/// Initializes the valid filters.
	/// </summary>
	/// <param name="filters">The filters.</param>
	protected static void InitializeValidFilters(params Expression<Func<TEntity, object>>[] filters)
	{
		Guard.IsNotNull(filters);

		var lstValidFilter = new HashSet<string>();

		foreach (var filter in filters)
		{
			if (filter is null)
				continue;

			_ = lstValidFilter.AddNotNull(filter.GetMemberPath());
		}

		_validFilterPaths = lstValidFilter;
	}

	/// <summary>
	/// Validates that the specified filters are permitted.
	/// </summary>
	/// <param name="filters">The filters.</param>
	/// <returns><see langword="true"/> if they are all valid; otherwise <see langword="false"/>.</returns>
	private protected virtual bool ValidateFilters(IEnumerable<FilterExpression<TEntity>>? filters)
	{
		if (filters is null || _validFilterPaths is null)
			return true;

		return filters.All(x => _validFilterPaths.Contains(x.MemberPath));
	}

	/// <summary>
	/// Throws an exception if one or more of the specified <paramref name="filters"/> is invalid.
	/// </summary>
	/// <param name="filters">The filters.</param>
	protected void ThrowIfFiltersInvalid(IEnumerable<FilterExpression<TEntity>>? filters)
	{
		if (!ValidateFilters(filters))
			throw new UmbrellaDataAccessException("One or more of the specified filters is invalid.");
	}

	/// <summary>
	/// Applies the query filter. By default, this does nothing unless overridden by derived types.
	/// </summary>
	/// <param name="query">The query.</param>
	/// <returns>An updated query with the filter applied.</returns>
	protected virtual IQueryable<TEntity> ApplyQueryFilter(IQueryable<TEntity> query) => query;

	/// <summary>
	/// Override this in a derived type to perform an operation on the entity after it has been loaded from the database.
	/// </summary>
	/// <param name="entity">The entity.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected virtual Task AfterItemLoadedAsync(TEntity entity, TRepoOptions? repoOptions, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return Task.CompletedTask;
	}

	/// <summary>
	/// Override this in a derived type to perform an operation on the entities after they have been loaded from the database.
	/// </summary>
	/// <param name="entities">The entities.</param>
	/// <param name="repoOptions">The repo options.</param>
	/// <param name="childOptions">The child options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> that completes when the operation has completed.</returns>
	protected async Task AfterAllItemsLoadedAsync(IEnumerable<TEntity> entities, TRepoOptions? repoOptions, IEnumerable<RepoOptions>? childOptions, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		IEnumerable<Task> tasks = entities.Select(x => AfterItemLoadedAsync(x, repoOptions, childOptions, cancellationToken));

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}
	#endregion
}