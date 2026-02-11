
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.Utilities.Data.Services.Abstractions;

/// <summary>
/// A generic service used to query a resource.
/// </summary>
/// <typeparam name="TItem">The type of the item.</typeparam>
/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
public interface IReadOnlyGenericDataService<TItem, TIdentifier>
	where TItem : class, IKeyedItem<TIdentifier>
	where TIdentifier : IEquatable<TIdentifier>
{
	/// <summary>
	/// Determines if the specified resource exists.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	Task<IOperationResult<bool>> ExistsByIdAsync(TIdentifier id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds the specified resource.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	Task<IOperationResult<TItem?>> FindByIdAsync(TIdentifier id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds the total count of <typeparamref name="TItem"/> that exist.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	Task<IOperationResult<int>> FindTotalCountAsync(CancellationToken cancellationToken = default);
}