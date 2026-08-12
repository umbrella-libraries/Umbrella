namespace Umbrella.Utilities.Data.Models;

/// <summary>
/// A no-op result model. This exists for use when specifying generic parameters for types that make use of the model types
/// but don't support creating items.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
public class NoopCreateResultModel<TKey> : ICreateResultModel<TKey>
	where TKey : IEquatable<TKey>
{
	/// <inheritdoc />
	public TKey Id => throw new NotImplementedException();
}