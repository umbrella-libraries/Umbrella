


using CommunityToolkit.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq.Expressions;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.EntityFrameworkCore.Extensions;

namespace Umbrella.DataAccess.EntityFrameworkCore.SqlServer.Extensions;

/// <summary>
/// Extension methods for use with <see cref="DbContext"/> instances.
/// </summary>
public static class DbContextExtensions
{
	private const string PeriodEndPropertyName = "PeriodEnd";

	/// <summary>
	/// Gets the next integer value in the named sequence.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="sequenceName">The sequence name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The next integer value in the named sequence.</returns>
	public static async Task<int> GetNextIntegerSequenceValueAsync(this DbContext dbContext, string sequenceName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(dbContext);

		var parameter = new SqlParameter("@result", SqlDbType.Int)
		{
			Direction = ParameterDirection.Output
		};

		var sequenceNameParameter = new SqlParameter("@sequenceName", SqlDbType.NVarChar)
		{
			Value = sequenceName
		};

		string sql = $"SET @result = NEXT VALUE FOR {sequenceName}";

		_ = await dbContext.Database.ExecuteSqlRawAsync(sql, new[] { parameter, sequenceNameParameter }, cancellationToken).ConfigureAwait(false);

		return (int)parameter.Value;
	}

	/// <summary>
	/// Gets the database version based on the last applied migration.
	/// </summary>
	/// <param name="dbContext">The database context.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The current database version.</returns>
	public static async Task<string?> GetDatabaseVersionAsync(this DbContext dbContext, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(dbContext);

		var lstMigration = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);

		string? lastMigration = lstMigration.LastOrDefault();

		if (lastMigration is not null)
		{
			int idxUnderscore = lastMigration.LastIndexOf('_');

			if (idxUnderscore >= 0)
				return lastMigration[(idxUnderscore + 1)..];
		}

		return null;
	}

	/// <summary>
	/// Finds the most recent history entity by id.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <param name="dbContext">The database context.</param>
	/// <param name="id">The identifier.</param>
	/// <param name="trackChanges">if set to <c>true</c>, tracks the entity on the context.</param>
	/// <param name="filter">The filter.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The history entity.</returns>
	public static async Task<TEntity?> FindMostRecentHistoryEntityByIdAsync<TEntity, TEntityKey>(this DbContext dbContext, TEntityKey id, bool trackChanges = false, Expression<Func<TEntity, bool>>? filter = null, CancellationToken cancellationToken = default)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(dbContext);

		var query = dbContext.Set<TEntity>()
			.TemporalAll()
			.TrackChanges(trackChanges)
			.OrderByDescending(x => EF.Property<DateTime>(x, PeriodEndPropertyName))
			.Where(x => x.Id.Equals(id));

		if (filter is not null)
		{
			query = query.Where(filter);
		}
		else
		{
			query = query.Skip(1);
		}

		return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Finds all most recent history entities by a list of ids.
	/// </summary>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	/// <typeparam name="TEntityKey">The type of the entity key.</typeparam>
	/// <param name="dbContext">The database context.</param>
	/// <param name="idList">The identifier list.</param>
	/// <param name="trackChanges">if set to <c>true</c>, tracks the entities on the context.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A collection of pairs of Ids and entities.</returns>
	public static async Task<IReadOnlyCollection<IdEntityPairResult<TEntity, TEntityKey>>> FindAllMostRecentHistoryEntityByIdListAsync<TEntity, TEntityKey>(this DbContext dbContext, IEnumerable<TEntityKey> idList, bool trackChanges = false, CancellationToken cancellationToken = default)
		where TEntity : class, IEntity<TEntityKey>
		where TEntityKey : IEquatable<TEntityKey>
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(dbContext);

		var lstId = idList.Distinct().ToArray();

		var lstEntityHistory = await dbContext.Set<TEntity>()
				.TemporalAll()
				.TrackChanges(trackChanges)
				.Where(x => lstId.Distinct().Contains(x.Id))
				.GroupBy(x => x.Id)
				.Select(x => new IdEntityPairResult<TEntity, TEntityKey> { Id = x.Key, Entity = x.OrderByDescending(x => EF.Property<DateTime>(x, PeriodEndPropertyName)).Skip(1).FirstOrDefault() })
				.ToArrayAsync(cancellationToken)
				.ConfigureAwait(false);

		return lstEntityHistory.UnionBy(lstId.Select(x => new IdEntityPairResult<TEntity, TEntityKey> { Id = x }), x => x.Id).ToArray();
	}
}