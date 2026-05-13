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
			static (serviceProvider, source, ct) => NewInstanceDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (TSource)source, ct),
			new NewInstanceDispatcher<TMapper, TSource, TDestination>());

	/// <summary>
	/// Registers an asynchronous mapper that creates a new destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncNewInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewInstanceAsyncMapper<TSource, TDestination>
		=> AddNewInstanceCore(
			typeof(TMapper),
			static async (serviceProvider, source, cancellationToken) => await serviceProvider.GetRequiredService<TMapper>().MapAsync((TSource)source, cancellationToken).ConfigureAwait(false),
			new NewInstanceDispatcher<TMapper, TSource, TDestination>());

	/// <summary>
	/// Registers a synchronous mapper that creates a new destination collection.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddNewCollection<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewCollectionMapper<TSource, TDestination>
		=> AddNewCollectionCore(
			typeof(TMapper),
			static (serviceProvider, source, ct) => NewCollectionDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (IEnumerable<TSource>)source, ct),
			new NewCollectionDispatcher<TMapper, TSource, TDestination>());

	/// <summary>
	/// Registers an asynchronous mapper that creates a new destination collection.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncNewCollection<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyNewCollectionAsyncMapper<TSource, TDestination>
		=> AddNewCollectionCore(
			typeof(TMapper),
			static async (serviceProvider, source, cancellationToken) => await serviceProvider.GetRequiredService<TMapper>().MapAllAsync((IEnumerable<TSource>)source, cancellationToken).ConfigureAwait(false),
			new NewCollectionDispatcher<TMapper, TSource, TDestination>());

	/// <summary>
	/// Registers a synchronous mapper that maps onto an existing destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddExistingInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyExistingInstanceMapper<TSource, TDestination>
		=> AddExistingInstanceCore(
			typeof(TMapper),
			static (serviceProvider, source, destination, ct) => ExistingInstanceDispatcher<TMapper, TSource, TDestination>.MapWithPreferenceAsync(serviceProvider, (TSource)source, (TDestination)destination, ct),
			new ExistingInstanceDispatcher<TMapper, TSource, TDestination>());

	/// <summary>
	/// Registers an asynchronous mapper that maps onto an existing destination instance.
	/// </summary>
	public UmbrellaMapperRegistryBuilder AddAsyncExistingInstance<TMapper, TSource, TDestination>()
		where TMapper : class, IUmbrellaMapperlyExistingInstanceAsyncMapper<TSource, TDestination>
		=> AddExistingInstanceCore(
			typeof(TMapper),
			static async (serviceProvider, source, destination, cancellationToken) =>
			{
				await serviceProvider.GetRequiredService<TMapper>().MapAsync((TSource)source, (TDestination)destination, cancellationToken).ConfigureAwait(false);
				return destination;
			},
			new ExistingInstanceDispatcher<TMapper, TSource, TDestination>());

	internal UmbrellaMapperRegistry Build()
		=> new(
			_newInstanceRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync),
			GroupDispatchers(_newInstanceRegistrations.Values.Select(x => x.Dispatcher)),
			_newCollectionRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync),
			GroupDispatchers(_newCollectionRegistrations.Values.Select(x => x.Dispatcher)),
			_existingInstanceRegistrations.ToDictionary(x => x.Key, x => x.Value.MapAsync),
			GroupDispatchers(_existingInstanceRegistrations.Values.Select(x => x.Dispatcher)));

	private UmbrellaMapperRegistryBuilder AddNewInstanceCore(
		Type mapperType,
		Func<IServiceProvider, object, CancellationToken, ValueTask<object?>> mapAsync,
		IObjectNewInstanceDispatcher dispatcher)
	{
		AddRegistration(_newInstanceRegistrations, mapperType, dispatcher.SourceType, dispatcher.DestinationType, new(mapperType, mapAsync, dispatcher));
		return this;
	}

	private UmbrellaMapperRegistryBuilder AddNewCollectionCore(
		Type mapperType,
		Func<IServiceProvider, object, CancellationToken, ValueTask<object>> mapAsync,
		IObjectNewCollectionDispatcher dispatcher)
	{
		AddRegistration(_newCollectionRegistrations, mapperType, dispatcher.SourceType, dispatcher.DestinationType, new(mapperType, mapAsync, dispatcher));
		return this;
	}

	private UmbrellaMapperRegistryBuilder AddExistingInstanceCore(
		Type mapperType,
		Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>> mapAsync,
		IObjectExistingInstanceDispatcher dispatcher)
	{
		AddRegistration(_existingInstanceRegistrations, mapperType, dispatcher.SourceType, dispatcher.DestinationType, new(mapperType, mapAsync, dispatcher));
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
		Guard.IsNotNull(registrations);
		Guard.IsNotNull(mapperType);

		var key = (sourceType, destinationType);

		if (registrations.TryGetValue(key, out TRegistration? existingRegistration))
		{
			throw new InvalidOperationException(
				$"A registration already exists for the source and destination types. The type being registered is {mapperType.FullName} but the type named {existingRegistration.MapperType.FullName} has already been registered.");
		}

		registrations[key] = registration;
	}

	private static Dictionary<Type, IReadOnlyList<TDispatcher>> GroupDispatchers<TDispatcher>(IEnumerable<TDispatcher> dispatchers)
		where TDispatcher : IDispatcher
		=> dispatchers
			.GroupBy(x => x.DestinationType)
			.ToDictionary(x => x.Key, x => (IReadOnlyList<TDispatcher>)x.ToArray());

	private abstract record MapperRegistration(Type MapperType);
	private sealed record NewInstanceRegistration(Type MapperType, Func<IServiceProvider, object, CancellationToken, ValueTask<object?>> MapAsync, IObjectNewInstanceDispatcher Dispatcher) : MapperRegistration(MapperType);
	private sealed record NewCollectionRegistration(Type MapperType, Func<IServiceProvider, object, CancellationToken, ValueTask<object>> MapAsync, IObjectNewCollectionDispatcher Dispatcher) : MapperRegistration(MapperType);
	private sealed record ExistingInstanceRegistration(Type MapperType, Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>> MapAsync, IObjectExistingInstanceDispatcher Dispatcher) : MapperRegistration(MapperType);
}

internal sealed class UmbrellaMapperRegistry
{
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object?>>> _newInstanceMappings;
	private readonly IReadOnlyDictionary<Type, IReadOnlyList<IObjectNewInstanceDispatcher>> _newInstanceDispatchers;
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object>>> _newCollectionMappings;
	private readonly IReadOnlyDictionary<Type, IReadOnlyList<IObjectNewCollectionDispatcher>> _newCollectionDispatchers;
	private readonly IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>>> _existingInstanceMappings;
	private readonly IReadOnlyDictionary<Type, IReadOnlyList<IObjectExistingInstanceDispatcher>> _existingInstanceDispatchers;

	public UmbrellaMapperRegistry(
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object?>>> newInstanceMappings,
		IReadOnlyDictionary<Type, IReadOnlyList<IObjectNewInstanceDispatcher>> newInstanceDispatchers,
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, CancellationToken, ValueTask<object>>> newCollectionMappings,
		IReadOnlyDictionary<Type, IReadOnlyList<IObjectNewCollectionDispatcher>> newCollectionDispatchers,
		IReadOnlyDictionary<(Type SourceType, Type DestinationType), Func<IServiceProvider, object, object, CancellationToken, ValueTask<object>>> existingInstanceMappings,
		IReadOnlyDictionary<Type, IReadOnlyList<IObjectExistingInstanceDispatcher>> existingInstanceDispatchers)
	{
		_newInstanceMappings = newInstanceMappings;
		_newInstanceDispatchers = newInstanceDispatchers;
		_newCollectionMappings = newCollectionMappings;
		_newCollectionDispatchers = newCollectionDispatchers;
		_existingInstanceMappings = existingInstanceMappings;
		_existingInstanceDispatchers = existingInstanceDispatchers;
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

	public bool TryMapNewInstanceObject<TDestination>(
		IServiceProvider serviceProvider,
		object source,
		CancellationToken cancellationToken,
		out ValueTask<TDestination> result)
	{
		if (_newInstanceDispatchers.TryGetValue(typeof(TDestination), out IReadOnlyList<IObjectNewInstanceDispatcher>? dispatchers))
		{
			foreach (IObjectNewInstanceDispatcher dispatcher in dispatchers)
			{
				if (dispatcher.TryMap(serviceProvider, source, cancellationToken, out ValueTask<object?> dispatcherResult))
				{
					result = CastAsync<TDestination>(dispatcherResult);
					return true;
				}
			}
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

	public bool TryMapNewCollectionObject<TDestination>(
		IServiceProvider serviceProvider,
		object source,
		CancellationToken cancellationToken,
		out ValueTask<IReadOnlyCollection<TDestination>> result)
	{
		if (_newCollectionDispatchers.TryGetValue(typeof(TDestination), out IReadOnlyList<IObjectNewCollectionDispatcher>? dispatchers))
		{
			foreach (IObjectNewCollectionDispatcher dispatcher in dispatchers)
			{
				if (dispatcher.TryMap(serviceProvider, source, cancellationToken, out ValueTask<object> dispatcherResult))
				{
					result = CastCollectionAsync<TDestination>(dispatcherResult);
					return true;
				}
			}
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

	public bool TryMapExistingInstanceObject<TDestination>(
		IServiceProvider serviceProvider,
		object source,
		TDestination destination,
		CancellationToken cancellationToken,
		out ValueTask<TDestination> result)
	{
		if (_existingInstanceDispatchers.TryGetValue(typeof(TDestination), out IReadOnlyList<IObjectExistingInstanceDispatcher>? dispatchers))
		{
			foreach (IObjectExistingInstanceDispatcher dispatcher in dispatchers)
			{
				if (dispatcher.TryMap(serviceProvider, source, destination!, cancellationToken, out ValueTask<object> dispatcherResult))
				{
					result = CastNonNullableAsync<TDestination>(dispatcherResult);
					return true;
				}
			}
		}

		result = default;
		return false;
	}

	private static async ValueTask<T> CastAsync<T>(ValueTask<object?> task)
		=> (T)(await task.ConfigureAwait(false))!;

	private static async ValueTask<T> CastNonNullableAsync<T>(ValueTask<object> task)
		=> (T)(await task.ConfigureAwait(false));

	private static async ValueTask<IReadOnlyCollection<T>> CastCollectionAsync<T>(ValueTask<object> task)
		=> (IReadOnlyCollection<T>)(await task.ConfigureAwait(false));
}

internal interface IDispatcher
{
	Type SourceType { get; }
	Type DestinationType { get; }
}

internal interface IObjectNewInstanceDispatcher : IDispatcher
{
	bool TryMap(IServiceProvider serviceProvider, object source, CancellationToken cancellationToken, out ValueTask<object?> result);
}

internal interface IObjectNewCollectionDispatcher : IDispatcher
{
	bool TryMap(IServiceProvider serviceProvider, object source, CancellationToken cancellationToken, out ValueTask<object> result);
}

internal interface IObjectExistingInstanceDispatcher : IDispatcher
{
	bool TryMap(IServiceProvider serviceProvider, object source, object destination, CancellationToken cancellationToken, out ValueTask<object> result);
}

internal sealed class NewInstanceDispatcher<TMapper, TSource, TDestination> : IObjectNewInstanceDispatcher
	where TMapper : class
{
	public Type SourceType => typeof(TSource);
	public Type DestinationType => typeof(TDestination);

	public bool TryMap(IServiceProvider serviceProvider, object source, CancellationToken cancellationToken, out ValueTask<object?> result)
	{
		if (source is not TSource typedSource)
		{
			result = default;
			return false;
		}

		result = MapWithPreferenceAsync(serviceProvider, typedSource, cancellationToken);
		return true;
	}

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

internal sealed class NewCollectionDispatcher<TMapper, TSource, TDestination> : IObjectNewCollectionDispatcher
	where TMapper : class
{
	public Type SourceType => typeof(TSource);
	public Type DestinationType => typeof(TDestination);

	public bool TryMap(IServiceProvider serviceProvider, object source, CancellationToken cancellationToken, out ValueTask<object> result)
	{
		if (source is not IEnumerable<TSource> typedSource)
		{
			result = default;
			return false;
		}

		result = MapWithPreferenceAsync(serviceProvider, typedSource, cancellationToken);
		return true;
	}

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

internal sealed class ExistingInstanceDispatcher<TMapper, TSource, TDestination> : IObjectExistingInstanceDispatcher
	where TMapper : class
{
	public Type SourceType => typeof(TSource);
	public Type DestinationType => typeof(TDestination);

	public bool TryMap(IServiceProvider serviceProvider, object source, object destination, CancellationToken cancellationToken, out ValueTask<object> result)
	{
		if (source is not TSource typedSource || destination is not TDestination typedDestination)
		{
			result = default;
			return false;
		}

		result = MapWithPreferenceAsync(serviceProvider, typedSource, typedDestination, cancellationToken);
		return true;
	}

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
