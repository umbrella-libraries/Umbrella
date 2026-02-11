
namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Specifies the contract for a Mapperly mapping class that can asynchronously map a <typeparamref name="TSource"/> instance onto an existing <typeparamref name="TDestination"/> instance.
/// </summary>
/// <typeparam name="TSource">The type of the source.</typeparam>
/// <typeparam name="TDestination">The type of the destination.</typeparam>
public interface IUmbrellaMapperlyExistingInstanceAsyncMapper<TSource, TDestination>
{
	/// <summary>
	/// Maps the specified <paramref name="source"/> to an existing instance of <typeparamref name="TDestination"/>.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="destination">The destination.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
	ValueTask MapAsync(TSource source, TDestination destination, CancellationToken cancellationToken);
}