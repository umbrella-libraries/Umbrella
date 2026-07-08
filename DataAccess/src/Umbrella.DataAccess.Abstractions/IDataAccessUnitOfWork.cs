namespace Umbrella.DataAccess.Abstractions;

/// <summary>
/// Represents a unit of work for data access operations.
/// </summary>
public interface IDataAccessUnitOfWork
{
	/// <summary>
	/// Commits the changes to the underlying data store.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable task.</returns>
	/// <remarks>
	/// Implementations should throw an <see cref="Umbrella.Utilities.Exceptions.UmbrellaConcurrencyException"/> when the commit fails
	/// because of an optimistic concurrency violation so that callers can surface the failure as a concurrency conflict,
	/// and an <see cref="Exceptions.UmbrellaDataAccessException"/> for all other failures.
	/// </remarks>
	Task CommitAsync(CancellationToken cancellationToken = default);
}