using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

namespace Umbrella.Utilities.Mapping.Mapperly;

/// <summary>
/// Builds the runtime mapping registry from one or more source-generated Mapperly catalogs.
/// </summary>
public sealed class UmbrellaMapperRegistryBuilder
{
	private readonly Dictionary<(Type SourceType, Type DestinationType), NewInstanceRegistration> _newInstanceRegistrations = [];
	private readonly Dictionary<(Type SourceType, Type DestinationType), NewCollectionRegistration> _newCollectionRegistrations = [];
	private readonly Dictionary<(Type SourceType, Type DestinationType), ExistingInstanceRegistration> _existingInstanceRegistrations = [];

	/// <summary>
	/// Registers a synchronous mapper that creates a new destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddNewInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewInstanceMapper<TSource, TDestination>
		=> AddNewInstanceCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static (serviceProvider, source, ct) => NewInstanceDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (TSource)source, ct));

	/// <summary>
	/// Registers an asynchronous mapper that creates a new destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncNewInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewInstanceAsyncMapper<TSource, TDestination>
		=> AddNewInstanceCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static async (serviceProvider, source, cancellationToken) => await serviceProvider.GetRequiredService<TMapper>().MapAsync((TSource)source, cancellationToken).ConfigureAwait(false));

	/// <summary>
	/// Registers a synchronous mapper that creates a new destination collection.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddNewCollection<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewCollectionMapper<TSource, TDestination>
		=> AddNewCollectionCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static (serviceProvider, source, ct) => NewCollectionDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (IEnumerable<TSource>)source, ct));

	/// <summary>
	/// Registers an asynchronous mapper that creates a new destination collection.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncNewCollection<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewCollectionAsyncMapper<TSource, TDestination>
		=> AddNewCollectionCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static async (serviceProvider, source, cancellationToken) => await serviceProvider.GetRequiredService<TMapper>().MapAllAsync((IEnumerable<TSource>)source, cancellationToken).ConfigureAwait(false));

	/// <summary>
	/// Registers a synchronous mapper that maps onto an existing destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddExistingInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyExistingInstanceMapper<TSource, TDestination>
		=> AddExistingInstanceCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static (serviceProvider, source, destination, ct) => ExistingInstanceDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (TSource)source, (TDestination)destination, ct));

	/// <summary>
	/// Registers an asynchronous mapper that maps onto an existing destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncExistingInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyExistingInstanceAsyncMapper<TSource, TDestination>
		=> AddExistingInstanceCore(
			typeof(TMapper),
			typeof(TSource),
			typeof(TDestination),
			static async (serviceProvider, source, destination, cancellationToken) =>
			{
				await serviceProvider.GetRequiredService<TMapper>().MapAsync((TSource)source, (TDestination)destination, cancellationToken).ConfigureAwait(false);
				return destination;
			});

	internal UmbrellaMapperRegistry Build()
		=> new(
			_newInstanceRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync),
			_newCollectionRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync),
			_existingInstanceRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync));

	private UmbrellaMapperRegistryBuilder AddNewInstanceCore(
		Type mapperType,
		Type sourceType,
		Type destinationType,
		Func<IServiceProvider, object, CancellationToken, ValueTask<object?>> mapAsync)
	{
		AddRegistration(_newInstanceRegistrations, mapperType, sourceType, destinationType, new(mapperType, mapAsync));
		return this;
	}

	private UmbrellaMapperRegistryBuilder AddNewCollectionCore(
		Type mapperType,
		Type sourceType,
		Type destinationType,
		Func<IServiceProvider, object, CancellationToken, ValueTask<object>> mapAsync)
	{
		AddRegistration(_newCollectionRegistrations, mapperType, sourceType, destinationType, new(mapperType, mapAsync));
		return this;
	}

	private UmbrellaMapperRegistryBuilder AddExistingInstanceCore(
		Type mapperType,
		Type sourceType,
		Type destinationType,
		Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>> mapAsync)
	{
		AddRegistration(_existingInstanceRegistrations, mapperType, sourceType, destinationType, new(mapperType, mapAsync));
		return this;
	}

	private static void AddRegistration<TRegistration>(
		IDictionary<(Type SourceType, Type DestinationType), TRegistration> registrations,
		Type mapperType,
		Type sourceType,
		Type destinationType,
		TRegistration registration)
		where TRegistration : MapperRegistration
	{
		Guard.IsNotNull(mapperType);

		var key = (sourceType, destinationType);

		if (registrations.TryGetValue(key, out TRegistration? existingRegistration))
		{
			throw new InvalidOperationException(
				$"A registration already exists for the source and destination types. The type being registered is {mapperType.FullName} but the type named {existingRegistration.MapperType.FullName} has already been registered.");
		}

		registrations[key] = registration;
	}

	private abstract record MapperRegistration(Type MapperType);
	private sealed record NewInstanceRegistration(Type MapperType, Func<IServiceProvider, object, CancellationToken, ValueTask<object?>> MapAsync) : MapperRegistration(MapperType);
	private sealed record NewCollectionRegistration(Type MapperType, Func<IServiceProvider, object, CancellationToken, ValueTask<object>> MapAsync) : MapperRegistration(MapperType);
	private sealed record ExistingInstanceRegistration(Type MapperType, Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>> MapAsync) : MapperRegistration(MapperType);
}

internal sealed class UmbrellaMapperRegistry
{
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object?>>> _newInstanceMappings;
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object>>> _newCollectionMappings;
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>>> _existingInstanceMappings;

	public UmbrellaMapperRegistry(
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object?>>> newInstanceMappings,
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object>>> newCollectionMappings,
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>>> existingInstanceMappings)
	{
		_newInstanceMappings = newInstanceMappings;
		_newCollectionMappings = newCollectionMappings;
		_existingInstanceMappings = existingInstanceMappings;
	}

	public bool TryMapNewInstanceExact<TSource, TDestination>(
		IServiceProvider serviceProvider,
		TSource source,
		CancellationToken cancellationToken,
		out ValueTask<TDestination> result)
	{
		if (_newInstanceMappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var mapping))
		{
			result = CastAsync<TDestination>(mapping(serviceProvider, source!, cancellationToken));
			return true;
		}

		result = default;
		return false;
	}

	public bool TryMapNewCollectionExact<TSource, TDestination>(
		IServiceProvider serviceProvider,
		IEnumerable<TSource> source,
		CancellationToken cancellationToken,
		out ValueTask<IReadOnlyCollection<TDestination>> result)
	{
		if (_newCollectionMappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var mapping))
		{
			result = CastCollectionAsync<TDestination>(mapping(serviceProvider, source, cancellationToken));
			return true;
		}

		result = default;
		return false;
	}

	public bool TryMapExistingInstanceExact<TSource, TDestination>(
		IServiceProvider serviceProvider,
		TSource source,
		TDestination destination,
		CancellationToken cancellationToken,
		out ValueTask<TDestination> result)
	{
		if (_existingInstanceMappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var mapping))
		{
			result = CastNonNullableAsync<TDestination>(mapping(serviceProvider, source!, destination!, cancellationToken));
			return true;
		}

		result = default;
		return false;
	}

#pragma warning disable VSTHRD103
	private static ValueTask<T> CastAsync<T>(ValueTask<object?> task)
		=> task.IsCompletedSuccessfully ? new ValueTask<T>((T)task.Result!) : SlowCastAsync<T>(task);

	private static ValueTask<T> CastNonNullableAsync<T>(ValueTask<object> task)
		=> task.IsCompletedSuccessfully ? new ValueTask<T>((T)task.Result) : SlowCastNonNullableAsync<T>(task);

	private static ValueTask<IReadOnlyCollection<T>> CastCollectionAsync<T>(ValueTask<object> task)
		=> task.IsCompletedSuccessfully ? new ValueTask<IReadOnlyCollection<T>>((IReadOnlyCollection<T>)task.Result) : SlowCastCollectionAsync<T>(task);
#pragma warning restore VSTHRD103

	private static async ValueTask<T> SlowCastAsync<T>(ValueTask<object?> task)
		=> (T)(await task.ConfigureAwait(false))!;

	private static async ValueTask<T> SlowCastNonNullableAsync<T>(ValueTask<object> task)
		=> (T)(await task.ConfigureAwait(false));

	private static async ValueTask<IReadOnlyCollection<T>> SlowCastCollectionAsync<T>(ValueTask<object> task)
		=> (IReadOnlyCollection<T>)(await task.ConfigureAwait(false));
}

internal sealed class NewInstanceDispatcher<TMapper, TSource, TDestination>
	where TMapper : class
{
	internal static ValueTask<object?> MapWithPreferenceAsync(IServiceProvider serviceProvider, TSource source, CancellationToken cancellationToken)
		=> serviceProvider.GetRequiredService<TMapper>() switch
		{
			IUmbrellaMapperlyNewInstanceAsyncMapper<TSource, TDestination> asyncMapper => MapAsync(asyncMapper, source, cancellationToken),
			IUmbrellaMapperlyNewInstanceMapper<TSource, TDestination> mapper => new ValueTask<object?>(mapper.Map(source)),
			_ => throw new InvalidOperationException($"The mapper type {typeof(TMapper).FullName} does not implement a supported new instance mapping interface.")
		};

	private static async ValueTask<object?> MapAsync(
		IUmbrellaMapperlyNewInstanceAsyncMapper<TSource, TDestination> mapper,
		TSource source,
		CancellationToken cancellationToken)
		=> await mapper.MapAsync(source, cancellationToken).ConfigureAwait(false);
}

internal sealed class NewCollectionDispatcher<TMapper, TSource, TDestination>
	where TMapper : class
{
	internal static ValueTask<object> MapWithPreferenceAsync(IServiceProvider serviceProvider, IEnumerable<TSource> source, CancellationToken cancellationToken)
		=> serviceProvider.GetRequiredService<TMapper>() switch
		{
			IUmbrellaMapperlyNewCollectionAsyncMapper<TSource, TDestination> asyncMapper => MapAsync(asyncMapper, source, cancellationToken),
			IUmbrellaMapperlyNewCollectionMapper<TSource, TDestination> mapper => new ValueTask<object>(mapper.MapAll(source)),
			_ => throw new InvalidOperationException($"The mapper type {typeof(TMapper).FullName} does not implement a supported new collection mapping interface.")
		};

	private static async ValueTask<object> MapAsync(
		IUmbrellaMapperlyNewCollectionAsyncMapper<TSource, TDestination> mapper,
		IEnumerable<TSource> source,
		CancellationToken cancellationToken)
		=> await mapper.MapAllAsync(source, cancellationToken).ConfigureAwait(false);
}

internal sealed class ExistingInstanceDispatcher<TMapper, TSource, TDestination>
	where TMapper : class
{
	internal static ValueTask<object> MapWithPreferenceAsync(IServiceProvider serviceProvider, TSource source, TDestination destination, CancellationToken cancellationToken)
		=> serviceProvider.GetRequiredService<TMapper>() switch
		{
			IUmbrellaMapperlyExistingInstanceAsyncMapper<TSource, TDestination> asyncMapper => MapAsync(asyncMapper, source, destination, cancellationToken),
			IUmbrellaMapperlyExistingInstanceMapper<TSource, TDestination> mapper => MapAsValueTaskAsync(mapper, source, destination),
			_ => throw new InvalidOperationException($"The mapper type {typeof(TMapper).FullName} does not implement a supported existing instance mapping interface.")
		};

	private static async ValueTask<object> MapAsync(
		IUmbrellaMapperlyExistingInstanceAsyncMapper<TSource, TDestination> mapper,
		TSource source,
		TDestination destination,
		CancellationToken cancellationToken)
	{
		await mapper.MapAsync(source, destination, cancellationToken).ConfigureAwait(false);
		return destination!;
	}

	private static ValueTask<object> MapAsValueTaskAsync(
		IUmbrellaMapperlyExistingInstanceMapper<TSource, TDestination> mapper,
		TSource source,
		TDestination destination)
	{
		mapper.Map(source, destination);
		return new ValueTask<object>(destination!);
	}
}
