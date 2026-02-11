


using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.Utilities.Data.Services.Abstractions;

/// <summary>
/// A generic service used to delete resources.
/// </summary>
/// <typeparam name="TIdentifier">The type of the identifier.</typeparam>
public interface IDeleteItemGenericDataService<TIdentifier>
	where TIdentifier : IEquatable<TIdentifier>
{
	/// <summary>
	/// Deletes the specified resource.
	/// </summary>
	/// <param name="id">The identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The result of the operation.</returns>
	Task<IOperationResult> DeleteAsync(TIdentifier id, CancellationToken cancellationToken = default);
}