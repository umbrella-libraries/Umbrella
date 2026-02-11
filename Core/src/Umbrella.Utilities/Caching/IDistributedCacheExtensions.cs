
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Umbrella.Utilities.Caching;

/// <summary>
/// A set of extension methods for <see cref="IDistributedCache"/> instances to bring things more into line
/// with those available for the <see cref="IMemoryCache"/>.
/// </summary>
public static class IDistributedCacheExtensions
{
	#region Public Static Methods        
	/// <summary>
	/// Gets the string from the cache using the specified <paramref name="key"/> or creates the specified string value using the provided <paramref name="factory"/> delegate if
	/// it is not present in the cache.
	/// </summary>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="factory">The factory.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <param name="throwOnCacheFailure">
	/// <para>Specifies whether or not to throw an exception if the operation to get or set the cache item in the underlying cache fails.</para>
	/// <para>Setting this as false means that the failure is handled silently allowing the new cache item to be built with any cache failures being masked.
	/// Any exceptions are then returned by the method as an <see cref="UmbrellaDistributedCacheException"/> which has an inner exception of type <see cref="AggregateException"/> to be handled manually by the caller.
	/// </para>
	/// </param>
	/// <returns>The string that has either been retrieved or added to the cache together with any exception information</returns>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="cache"/>, <paramref name="key"/>,<paramref name="factory"/> or <paramref name="optionsBuilder"/> are null.</exception>
	/// <exception cref="ArgumentException">Thrown when the <paramref name="key"/> is either an empty string or whitespace.</exception>
	public static (string? item, UmbrellaDistributedCacheException? exception) GetOrCreateString(this IDistributedCache cache, string key, Func<string> factory, Func<DistributedCacheEntryOptions> optionsBuilder, bool throwOnCacheFailure = true)
	{
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));
		Guard.IsNotNull(factory, nameof(factory));
		Guard.IsNotNull(optionsBuilder, nameof(optionsBuilder));

		try
		{
			string? result = null;

			List<Exception>? lstException = throwOnCacheFailure ? [] : null;

			try
			{
				result = cache.GetString(key);
			}
			catch (Exception exc)
			{
				if (throwOnCacheFailure)
					throw;

				lstException?.Add(exc);
			}

			if (string.IsNullOrWhiteSpace(result))
			{
				//A failure to create the item should always throw an exception
				result = factory();

				try
				{
					if (!string.IsNullOrWhiteSpace(result))
						cache.SetString(key, result, optionsBuilder());
				}
				catch (Exception exc)
				{
					if (throwOnCacheFailure)
						throw;

					lstException?.Add(exc);
				}
			}

			return (result, CreateUmbrellaException(lstException));
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(GetOrCreateString)} failed.", exc);
		}
	}

	/// <summary>
	/// Gets the string from the cache using the specified <paramref name="key"/> or creates the specified string value using the provided <paramref name="factory"/> delegate if
	/// it is not present in the cache.
	/// </summary>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="factory">The factory.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <param name="throwOnCacheFailure">
	/// <para>Specifies whether or not to throw an exception if the operation to get or set the cache item in the underlying cache fails.</para>
	/// <para>Setting this as false means that the failure is handled silently allowing the new cache item to be built with any cache failures being masked.
	/// Any exceptions are then returned by the method as an <see cref="UmbrellaDistributedCacheException"/> which has an inner exception of type <see cref="AggregateException"/> to be handled manually by the caller.
	/// </para>
	/// </param>
	/// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
	/// <returns>The string that has either been retrieved or added to the cache together with any exception information</returns>
	/// <exception cref="ArgumentNullException">Thrown when the <paramref name="cache"/>, <paramref name="key"/>,<paramref name="factory"/> or <paramref name="optionsBuilder"/> are null.</exception>
	/// <exception cref="ArgumentException">Thrown when the <paramref name="key"/> is either an empty string or whitespace.</exception>
	public static async Task<(string? item, UmbrellaDistributedCacheException? exception)> GetOrCreateStringAsync(this IDistributedCache cache, string key, Func<Task<string>> factory, Func<DistributedCacheEntryOptions> optionsBuilder, bool throwOnCacheFailure = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));
		Guard.IsNotNull(factory, nameof(factory));
		Guard.IsNotNull(optionsBuilder, nameof(optionsBuilder));

		try
		{
			string? result = null;

			List<Exception>? lstException = throwOnCacheFailure ? [] : null;

			try
			{
				result = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				if (throwOnCacheFailure)
					throw;

				lstException?.Add(exc);
			}

			if (string.IsNullOrWhiteSpace(result))
			{
				//A failure to create the item should always throw an exception
				result = await factory().ConfigureAwait(false);

				try
				{
					if (!string.IsNullOrWhiteSpace(result))
						await cache.SetStringAsync(key, result, optionsBuilder(), cancellationToken).ConfigureAwait(false);
				}
				catch (Exception exc)
				{
					if (throwOnCacheFailure)
						throw;

					lstException?.Add(exc);
				}
			}

			return (result, CreateUmbrellaException(lstException));
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(GetOrCreateString)} failed.", exc);
		}
	}

	/// <summary>
	/// Tries to get the item of type <typeparamref name="TItem"/> with the specifed <paramref name="key"/> from the <paramref name="cache"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <returns>A tuple containing a success status, the item if present and any exception thrown internally when trying to get the item.</returns>
	public static (bool itemFound, TItem? cacheItem, UmbrellaDistributedCacheException? exception) TryGetValue<TItem>(this IDistributedCache cache, string key)
	{
		var (itemFound, cacheItem, exception) = TryGetValue<TItem>(cache, key, false);

		return (itemFound, cacheItem, exception is not null ? new UmbrellaDistributedCacheException($"{nameof(TryGetValue)} failed.", exception) : null);
	}

	/// <summary>
	///  Tries to get the item of type <typeparamref name="TItem"/> with the specifed <paramref name="key"/> from the <paramref name="cache"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> containing a tuple containing a success status, the item if present and any exception thrown internally when trying to get the item.</returns>
	public static async Task<(bool itemFound, TItem? cacheItem, UmbrellaDistributedCacheException? exception)> TryGetValueAsync<TItem>(this IDistributedCache cache, string key, CancellationToken cancellationToken = default)
	{
		var (itemFound, cacheItem, exception) = await TryGetValueAsync<TItem>(cache, key, false, cancellationToken).ConfigureAwait(false);

		return (itemFound, cacheItem, exception is not null ? new UmbrellaDistributedCacheException($"{nameof(TryGetValueAsync)} failed.", exception) : null);
	}

	/// <summary>
	/// Gets the item with the specified key from the cache.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <returns>The item if found, otherwise the default value for the type.</returns>
	/// <exception cref="UmbrellaDistributedCacheException">Get failed.</exception>
	public static TItem? Get<TItem>(this IDistributedCache cache, string key)
	{
		try
		{
			var (itemFound, cacheItem, exception) = TryGetValue<TItem>(cache, key, true);

			return cacheItem;
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(Get)} failed.", exc);
		}
	}

	/// <summary>
	/// Gets the item with the specified key from the cache.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The item if found, otherwise the default value for the type.</returns>
	/// <exception cref="UmbrellaDistributedCacheException">GetAsync failed.</exception>
	public static async Task<TItem?> GetAsync<TItem>(this IDistributedCache cache, string key, CancellationToken cancellationToken = default)
	{
		try
		{
			var (itemFound, cacheItem, exception) = await TryGetValueAsync<TItem>(cache, key, true, cancellationToken).ConfigureAwait(false);

			return cacheItem;
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(GetAsync)} failed.", exc);
		}
	}

	/// <summary>
	/// Sets the <paramref name="item"/> in the <paramref name="cache"/> using the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="item">The item.</param>
	/// <param name="options">The options.</param>
	/// <exception cref="UmbrellaDistributedCacheException">Set failed.</exception>
	public static void Set(this IDistributedCache cache, string key, object item, DistributedCacheEntryOptions options)
	{
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNull(item, nameof(item));
		Guard.IsNotNull(options, nameof(options));

		try
		{
			string json = UmbrellaStatics.SerializeJson(item, false);

			cache.SetString(key, json, options);
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(Set)} failed.", exc);
		}
	}

	/// <summary>
	/// Sets the <paramref name="item"/> in the <paramref name="cache"/> using the specified <paramref name="key"/>.
	/// </summary>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="item">The item.</param>
	/// <param name="options">The options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <exception cref="UmbrellaDistributedCacheException">SetAsync failed.</exception>
	public static async Task SetAsync(this IDistributedCache cache, string key, object item, DistributedCacheEntryOptions options, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNull(item, nameof(item));
		Guard.IsNotNull(options, nameof(options));

		try
		{
			string json = UmbrellaStatics.SerializeJson(item, false);

			await cache.SetStringAsync(key, json, options, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(SetAsync)} failed.", exc);
		}
	}

	/// <summary>
	/// Gets or creates the item in the cache using the specified <paramref name="factory"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="factory">The factory.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <param name="throwOnCacheFailure">
	/// if set to <c>true</c> throws any internal exceptions. If this is <see langword="false"/>, the exception will be returned
	/// in the method result.
	/// </param>
	/// <returns>
	/// A tuple containing the cached item together with any exception thrown internally when accessing the cache.
	/// If the <paramref name="throwOnCacheFailure"/> parameter is <see langword="true"/>, this method will throw this exception
	/// instead of returning.
	/// </returns>
	/// <exception cref="UmbrellaDistributedCacheException">GetOrCreate failed.</exception>
	public static (TItem cacheItem, UmbrellaDistributedCacheException? exception) GetOrCreate<TItem>(this IDistributedCache cache, string key, Func<TItem> factory, Func<DistributedCacheEntryOptions> optionsBuilder, bool throwOnCacheFailure = true)
	{
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));
		Guard.IsNotNull(factory, nameof(factory));
		Guard.IsNotNull(optionsBuilder, nameof(optionsBuilder));

		try
		{
			List<Exception>? lstException = throwOnCacheFailure ? [] : null;

			var (itemFound, cacheItem, exception) = cache.TryGetValue<TItem>(key);

			if (throwOnCacheFailure && exception is not null)
				throw exception;

			if (itemFound && cacheItem is not null)
				return (cacheItem, exception);

			if (exception is not null)
				lstException?.Add(exception);

			// If we get this far then we haven't found the cached item
			// Always allow this to throw an exception
			TItem createdItem = factory();

			try
			{
				if (createdItem is not null)
					Set(cache, key, createdItem, optionsBuilder());
			}
			catch (Exception exc)
			{
				if (throwOnCacheFailure && exception is not null)
					throw;

				lstException?.Add(exc);
			}

			return (createdItem, CreateUmbrellaException(lstException));
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(GetOrCreate)} failed.", exc);
		}
	}

	/// <summary>
	/// Gets or creates the item in the cache using the specified <paramref name="factory"/>.
	/// </summary>
	/// <typeparam name="TItem">The type of the item.</typeparam>
	/// <param name="cache">The cache.</param>
	/// <param name="key">The key.</param>
	/// <param name="factory">The factory.</param>
	/// <param name="optionsBuilder">The options builder.</param>
	/// <param name="throwOnCacheFailure">
	/// if set to <c>true</c> throws any internal exceptions. If this is <see langword="false"/>, the exception will be returned
	/// in the method result.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A tuple containing the cached item together with any exception thrown internally when accessing the cache.
	/// If the <paramref name="throwOnCacheFailure"/> parameter is <see langword="true"/>, this method will throw this exception
	/// instead of returning.
	/// </returns>
	/// <exception cref="UmbrellaDistributedCacheException">GetOrCreateAsync failed.</exception>
	public static async Task<(TItem cacheItem, UmbrellaDistributedCacheException? exception)> GetOrCreateAsync<TItem>(this IDistributedCache cache, string key, Func<Task<TItem>> factory, Func<DistributedCacheEntryOptions> optionsBuilder, bool throwOnCacheFailure = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(cache);
		Guard.IsNotNullOrWhiteSpace(key);
		Guard.IsNotNull(factory);
		Guard.IsNotNull(optionsBuilder);

		try
		{
			List<Exception>? lstException = throwOnCacheFailure ? [] : null;

			var (itemFound, cacheItem, exception) = await cache.TryGetValueAsync<TItem>(key, cancellationToken).ConfigureAwait(false);

			if (throwOnCacheFailure && exception is not null)
				throw exception;

			if (itemFound && cacheItem is not null)
				return (cacheItem, exception);

			if (exception is not null)
				lstException?.Add(exception);

			// If we get this far then we haven't found the cached item
			// Always allow this to throw an exception
			TItem createdItem = await factory().ConfigureAwait(false);

			try
			{
				if (createdItem is not null)
					await SetAsync(cache, key, createdItem, optionsBuilder(), cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exc)
			{
				if (throwOnCacheFailure)
					throw;

				lstException?.Add(exc);
			}

			return (createdItem, CreateUmbrellaException(lstException));
		}
		catch (Exception exc)
		{
			throw new UmbrellaDistributedCacheException($"{nameof(GetOrCreateAsync)} failed.", exc);
		}
	}
	#endregion

	#region Private Static Methods
	private static (bool itemFound, TItem? cacheItem, Exception? exception) TryGetValue<TItem>(this IDistributedCache cache, string key, bool throwError)
	{
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));

		try
		{
			string? result = cache.GetString(key);

			if (!string.IsNullOrWhiteSpace(result))
			{
				var item = UmbrellaStatics.DeserializeJson<TItem>(result);

				return (true, item, null);
			}
		}
		catch (Exception exc)
		{
			if (throwError)
				throw;

			return (false, default, exc);
		}

		return (false, default, null);
	}

	private static async Task<(bool itemFound, TItem? cacheItem, Exception? exception)> TryGetValueAsync<TItem>(this IDistributedCache cache, string key, bool throwError, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(cache, nameof(cache));
		Guard.IsNotNullOrWhiteSpace(key, nameof(key));

		try
		{
			string? result = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);

			if (!string.IsNullOrWhiteSpace(result))
			{
				var item = UmbrellaStatics.DeserializeJson<TItem>(result);

				return (true, item, null);
			}
		}
		catch (Exception exc)
		{
			if (throwError)
				throw;

			return (false, default, exc);
		}

		return (false, default, null);
	}

	private static UmbrellaDistributedCacheException? CreateUmbrellaException(List<Exception>? exceptions)
		=> exceptions?.Count > 0
			? new UmbrellaDistributedCacheException("One or more errors have occurred. Please see the inner exception for details.", new AggregateException(exceptions))
			: null;
	#endregion
}