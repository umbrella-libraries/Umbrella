


namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions;

/// <summary>
/// Specifies the contract for a Mapperly mapping class.
/// </summary>
/// <remarks>
/// This interface encapsulates the following 3 interfaces:
/// <list type="bullet">
/// <item><see cref="IUmbrellaMapperlyNewInstanceMapper{TSource, TDestination}"/></item>
/// <item><see cref="IUmbrellaMapperlyNewCollectionMapper{TSource, TDestination}"/></item>
/// <item><see cref="IUmbrellaMapperlyExistingInstanceMapper{TSource, TDestination}"/></item>
/// </list>
/// </remarks>
/// <typeparam name="TSource">The type of the source.</typeparam>
/// <typeparam name="TDestination">The type of the destination.</typeparam>
public interface IUmbrellaMapperlyMapper<TSource, TDestination> : IUmbrellaMapperlyNewInstanceMapper<TSource, TDestination>, IUmbrellaMapperlyNewCollectionMapper<TSource, TDestination>, IUmbrellaMapperlyExistingInstanceMapper<TSource, TDestination>
{
}