
namespace Umbrella.Utilities.Mapping.Abstractions;

/// <summary>
/// A very basic object mapper to perform property mapping between objects.
/// </summary>
/// <remarks>
/// The primary motivation for creating this is to allow flexibility
/// in choosing the mapping implementation used by package consumers. All methods are asynchronous which allows us
/// to take advantage of asynchronous mapping implementation support in the future.
/// </remarks>
public interface IUmbrellaMapper
{
	/// <summary>
	/// Execute a mapping from the source object to a new destination object.
	/// </summary>
	/// <typeparam name="TSource">The type of the source.</typeparam>
	/// <typeparam name="TDestination">The type of the destination.</typeparam>
	/// <param name="source">The source object to map from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Mapped destination object.</returns>
	ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);

	/// <summary>
	/// Execute a mapping from the source object collection to a new destination object collection.
	/// </summary>
	/// <typeparam name="TSource">The type of the source.</typeparam>
	/// <typeparam name="TDestination">The type of the destination.</typeparam>
	/// <param name="source">The source object to map from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Mapped destination object collection.</returns>
	ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);

	/// <summary>
	/// Execute a mapping from the source object to the existing destination object.
	/// </summary>
	/// <typeparam name="TSource">The type of the source.</typeparam>
	/// <typeparam name="TDestination">The type of the destination.</typeparam>
	/// <param name="source">The source object to map from.</param>
	/// <param name="destination">Destination object to map into.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The mapped destination object, same instance as the destination object.</returns>
	ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
}