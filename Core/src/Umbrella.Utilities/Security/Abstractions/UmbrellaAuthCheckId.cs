using Microsoft.Extensions.ObjectPool;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.Utilities.Security.Abstractions;

/// <summary>
/// The base class for auth check id types.
/// </summary>
/// <typeparam name="TAuthCheckId">The type from which this is derived.</typeparam>
/// <typeparam name="TValue">The type of the value stored by this type.</typeparam>
[Obsolete("Please favour a pattern of creating AuthCheckId types as readonly record structs with a Value property and then creating the AuthId types as needed instead of using this factory.")]
public abstract class UmbrellaAuthCheckId<TAuthCheckId, TValue> : IDisposable
	where TAuthCheckId : class
{
	/// <summary>
	/// Gets or sets the object pool.
	/// </summary>
	public ObjectPool<TAuthCheckId>? Pool { get; internal set; }

	/// <summary>
	/// Gets or sets the value.
	/// </summary>
	public TValue Value { get; internal set; } = default!;

	/// <inheritdoc />
	public void Dispose()
	{
		if (this is TAuthCheckId value)
			Pool?.Return(value);
		else
			throw new UmbrellaException($"The object is not of type: {typeof(TAuthCheckId).FullName}.");

		Pool = null;
		GC.SuppressFinalize(this);
	}
}