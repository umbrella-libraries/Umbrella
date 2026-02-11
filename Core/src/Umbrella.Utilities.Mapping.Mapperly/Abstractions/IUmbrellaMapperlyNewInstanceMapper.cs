


namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Specifies the contract for a Mapperly mapping class that can map a <typeparamref name="TSource"/> instance onto a new <typeparamref name="TDestination"/> instance. 
/// </summary>
/// <typeparam name="TSource">The type of the source.</typeparam>
/// <typeparam name="TDestination">The type of the destination.</typeparam>
public interface IUmbrellaMapperlyNewInstanceMapper<TSource, TDestination>
{
	/// <summary>
	/// Maps the specified <paramref name="source"/> to the <typeparamref name="TDestination"/> type.
	/// </summary>
	/// <param name="source">The source.</param>
	/// <returns>The mapped destination type.</returns>
	TDestination Map(TSource source);
}