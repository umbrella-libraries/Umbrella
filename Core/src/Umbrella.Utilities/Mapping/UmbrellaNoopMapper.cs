using Umbrella.Utilities.Mapping.Abstractions;

namespace Umbrella.Utilities.Mapping;

internal sealed class UmbrellaNoopMapper : IUmbrellaMapper
{
	private const string NotSupportedMessage = "Mapping has not been configured. Please ensure you have registered either the AutoMapper or Mapperly services.";

	public ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default) => throw new NotSupportedException(NotSupportedMessage);
	public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default) => throw new NotSupportedException(NotSupportedMessage);
	public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default) => throw new NotSupportedException(NotSupportedMessage);
}