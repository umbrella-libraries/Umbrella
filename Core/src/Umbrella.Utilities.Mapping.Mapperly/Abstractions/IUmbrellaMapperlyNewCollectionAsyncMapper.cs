


namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Specifies the contract for a Mapperly mapping class that can asynchronously map a collection of <typeparamref name="TSource"/> instances
/// onto a new collection of <typeparamref name="TDestination"/> instances.
/// </summary>
/// <typeparam name="TSource">The type of the source.</typeparam>
/// <typeparam name="TDestination">The type of the destination.</typeparam>
public interface IUmbrellaMapperlyNewCollectionAsyncMapper<TSource, TDestination>
{
	/// <summary>
	/// Maps all of the specified <paramref name="source"/> items to a collection of <typeparamref name="TDestination"/> items.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The mapped <typeparamref name="TDestination"/> item collection.</returns>
	ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync(IEnumerable<TSource> source, CancellationToken cancellationToken);
}