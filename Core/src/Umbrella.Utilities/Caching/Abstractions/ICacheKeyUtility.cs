namespace Umbrella.Utilities.Caching.Abstractions;

/// <summary>
/// A utility used to create cache keys.
/// </summary>
public interface ICacheKeyUtility
{
	/// <summary>
	/// Creates a cache key based on the type of the item being cached and the key provided.
	/// </summary>
	/// <typeparam name="T">The type of the item being cached.</typeparam>
	/// <param name="key">The key.</param>
	/// <returns>The generated cache key.</returns>
	string Create<T>(string key);

	/// <summary>
	/// Creates a cache key based on the type of the item being cached and the key provided.
	/// </summary>
	/// <param name="type">The type of the item being cached.</param>
	/// <param name="key">The key.</param>
	/// <returns>The generated cache key.</returns>
	string Create(Type type, string key);

	/// <summary>
	/// Creates a cache key based on the type of the item being cached and the key provided.
	/// </summary>
	/// <typeparam name="T">The type of the item being cached.</typeparam>
	/// <param name="keyParts">The key parts.</param>
	/// <param name="keyPartsLength">
	/// Length of the key parts. This will result in exactly this number of items being read.
	/// </param>
	/// <returns>The generated cache key.</returns>
	string Create<T>(in ReadOnlySpan<string> keyParts, int? keyPartsLength = null);

	/// <summary>
	/// Creates a cache key based on the type of the item being cached and the key provided.
	/// </summary>
	/// <param name="type">The type of the item being cached.</param>
	/// <param name="keyParts">The key parts.</param>
	/// <param name="keyPartsLength">
	/// Length of the key parts. This will result in exactly this number of items being read.
	/// </param>
	/// <returns>The generated cache key.</returns>
	string Create(Type type, in ReadOnlySpan<string> keyParts, int? keyPartsLength = null);

	/// <summary>
	/// Creates a cache key based on the key parts provided.
	/// </summary>
	/// <param name="keyParts">The key parts.</param>
	/// <returns>The generated cache key.</returns>
	string Create<T>(params scoped ReadOnlySpan<string> keyParts);
}