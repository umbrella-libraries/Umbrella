using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Security.Abstractions;

/// <summary>
/// The base class for factories used to create AuthCheckId instances.
/// </summary>
[Obsolete("Please favour a pattern of creating AuthCheckId types as readonly record structs with a Value property and then creating the AuthId types as needed instead of using this factory.")]
public abstract class UmbrellaAuthCheckIdFactory
{
	private static readonly ConcurrentDictionary<Type, object> _objectPoolDictionary = new();

	/// <summary>
	/// Create a new instance of the specified <typeparamref name="TAuthCheckId"/> type that has contains a value of <typeparamref name="TValue"/>.
	/// </summary>
	/// <typeparam name="TAuthCheckId">The auth check id type.</typeparam>
	/// <typeparam name="TValue">The value type.</typeparam>
	/// <param name="id">The id value.</param>
	/// <returns>A new auth check id instance that derives from <typeparamref name="TAuthCheckId"/>.</returns>
	/// <exception cref="UmbrellaException">Thrown if an <see cref="ObjectPool"/> cannot be found for the specified <typeparamref name="TAuthCheckId"/>.</exception>
	protected static TAuthCheckId GetAuthCheckId<TAuthCheckId, TValue>(TValue id)
		where TAuthCheckId : UmbrellaAuthCheckId<TAuthCheckId, TValue>, new()
	{
		if (_objectPoolDictionary.GetOrAdd(typeof(TAuthCheckId), type => ObjectPool.Create<TAuthCheckId>()) is not ObjectPool<TAuthCheckId> objectPool)
			throw new UmbrellaException("The object pool cannot be found.");

		TAuthCheckId obj = objectPool.Get();
		obj.Value = id;
		obj.Pool = objectPool;

		return obj;
	}
}