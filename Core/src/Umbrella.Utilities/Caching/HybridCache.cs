


using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using Umbrella.Utilities.Caching;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Caching.Options;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Options;

namespace Umbrella.Utilities.Caching;

/// <summary>
/// A hybrid cache that allows cache items to be stored in an <see cref="IMemoryCache"/> or a <see cref="IDistributedCache"/> implementation.
/// The cache includes the option to allow internal errors that occur when adding or retrieving items from the caches to be masked in the event of transient errors.
/// </summary>
/// <seealso cref="IHybridCache" />
[Obsolete("This caching mechanism will be removed at a future date with a version that implements a proper multi-tiered cache. Ask Rich if you have any questions.")]
public class HybridCache : IHybridCache, IDisposable
{
	#region Private Members
	private readonly ReaderWriterLockSlim _nukeReaderWriterLock = new();
	private CancellationTokenSource _nukeTokenSource = new();
	#endregion

	#region Protected Properties		
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	protected HybridCacheOptions Options { get; }

	/// <summary>
	/// Gets the lookup normalizer.
	/// </summary>
	protected IDataLookupNormalizer LookupNormalizer { get; }

	/// <summary>
	/// Gets a value indicating whether to track cache keys.
	/// </summary>
	protected bool TrackKeys { get; }

	/// <summary>
	/// Gets a value indicating whether to track cache keys and hits.
	/// </summary>
	protected bool TrackKeysAndHits { get; }

	/// <summary>
	/// Gets the distributed cache.
	/// </summary>
	protected IDistributedCache DistributedCache { get; }

	/// <summary>
	/// Gets the memory cache.
	/// </summary>
	protected IMemoryCache MemoryCache { get; }

	/// <summary>
	/// Gets the memory cache meta entry dictionary.
	/// </summary>
	protected ConcurrentDictionary<string, HybridCacheMetaEntry> MemoryCacheMetaEntryDictionary { get; } = new ConcurrentDictionary<string, HybridCacheMetaEntry>();
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="HybridCache"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	/// <param name="lookupNormalizer">The lookup normalizer.</param>
	/// <param name="distributedCache">The distributed cache.</param>
	/// <param name="memoryCache">The memory cache.</param>
	public HybridCache(
		ILogger<HybridCache> logger,
		HybridCacheOptions options,
		IDataLookupNormalizer lookupNormalizer,
		IDistributedCache distributedCache,
		IMemoryCache memoryCache)
	{
		Logger = logger;
		Options = options;
		LookupNormalizer = lookupNormalizer;
		DistributedCache = distributedCache;
		MemoryCache = memoryCache;
		TrackKeys = (Options.AnalyticsMode & HybridCacheAnalyticsMode.TrackKeys) is HybridCacheAnalyticsMode.TrackKeys;
		TrackKeysAndHits = (Options.AnalyticsMode & HybridCacheAnalyticsMode.TrackKeysAndHits) is HybridCacheAnalyticsMode.TrackKeysAndHits;
	}
	#endregion

	#region IHybridCache Members
	/// <inheritdoc />
	public T GetOrCreate<T>(string cacheKey, Func<T> actionFunction, Func<TimeSpan>? expirationTimeSpanBuilder = null, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool slidingExpiration = false, bool throwOnCacheFailure = true, CacheItemPriority priority = CacheItemPriority.Normal, bool? cacheEnabledOverride = null, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null)
	{
		Guard.IsNotNullOrWhiteSpace(cacheKey);
		Guard.IsNotNull(actionFunction);

		try
		{
			if (IsCacheEnabled(cacheEnabledOverride))
			{
				string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);
				TimeSpan expirationTimeSpan = DetermineExpirationTimeSpan(expirationTimeSpanBuilder, priority);

				if (cacheMode is HybridCacheMode.Distributed)
				{
					try
					{
						(T cacheItem, UmbrellaDistributedCacheException? exception) = DistributedCache.GetOrCreate(cacheKeyInternal, actionFunction, () => BuildDistributedCacheEntryOptions(expirationTimeSpan, slidingExpiration), false);

						if (exception is not null)
						{
							if (!throwOnCacheFailure)
							{
								_ = Logger.WriteError(exception, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride });
							}
							else
							{
								throw exception;
							}
						}

						return cacheItem;
					}
					catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
					{
						if (throwOnCacheFailure)
							throw new HybridCacheException("There has been a problem getting or creating the specified item in the cache.", exc);
					}
				}
				else
				{
					try
					{
						HybridCacheMetaEntry? cacheMetaEntry = null;

						if (TrackKeys)
							_ = MemoryCacheMetaEntryDictionary.TryGetValue(cacheKeyInternal, out cacheMetaEntry);

						T? cacheItem = MemoryCache.GetOrCreate(cacheKeyInternal, entry =>
						{
							MemoryCacheEntryOptions options = BuildMemoryCacheEntryOptions(expirationTimeSpan, slidingExpiration, priority, expirationTokensBuilder);
							_ = entry.SetOptions(options);

							T entryToAdd = actionFunction();

							if (entryToAdd is not null && TrackKeys)
							{
								cacheMetaEntry = new HybridCacheMetaEntry(cacheKeyInternal, expirationTimeSpan, slidingExpiration);
								_ = MemoryCacheMetaEntryDictionary.TryAdd(cacheKeyInternal, cacheMetaEntry);
								_ = entry.RegisterPostEvictionCallback((key, value, reason, state) => MemoryCacheMetaEntryDictionary.TryRemove(cacheKeyInternal, out HybridCacheMetaEntry? removedEntry));
							}

							return entryToAdd;
						});

						if (cacheMetaEntry is not null && TrackKeysAndHits)
							cacheMetaEntry.AddHit();

						return cacheItem ?? default!;
					}
					catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
					{
						if (throwOnCacheFailure)
							throw new HybridCacheException("There has been a problem getting or creating the specified item in the cache.", exc);
					}
				}
			}

			return actionFunction();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
		{
			// If we get this far then there has definitely been a problem with the actionFunction. We need to always throw here.
			throw new HybridCacheException("There has been a problem with the cache.", exc);
		}
	}

	/// <inheritdoc />
	public T GetOrCreate<T>(string cacheKey, Func<T> actionFunction, CacheableUmbrellaOptions options, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null)
		=> GetOrCreate(cacheKey, actionFunction, () => options.CacheTimeout, options.CacheMode, options.CacheSlidingExpiration, options.CacheThrowOnFailure, options.CachePriority, options.CacheEnabled, expirationTokensBuilder);

	/// <inheritdoc />
	public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<T> actionFunction, Func<TimeSpan>? expirationTimeSpanBuilder = null, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool slidingExpiration = false, bool throwOnCacheFailure = true, CacheItemPriority priority = CacheItemPriority.Normal, bool? cacheEnabledOverride = null, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(cacheKey);
		Guard.IsNotNull(actionFunction);

		try
		{
			return await GetOrCreateAsync(cacheKey, () => Task.FromResult(actionFunction()), expirationTimeSpanBuilder, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride, expirationTokensBuilder, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
		{
			throw new HybridCacheException("There has been a problem with the cache.", exc);
		}
	}

	/// <inheritdoc />
	public Task<T> GetOrCreateAsync<T>(string cacheKey, Func<T> actionFunction, CacheableUmbrellaOptions options, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
		=> GetOrCreateAsync(cacheKey, actionFunction, () => options.CacheTimeout, options.CacheMode, options.CacheThrowOnFailure, options.CacheThrowOnFailure, options.CachePriority, options.CacheEnabled, expirationTokensBuilder, cancellationToken);

	/// <inheritdoc />
	public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> actionFunction, Func<TimeSpan>? expirationTimeSpanBuilder = null, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool slidingExpiration = false, bool throwOnCacheFailure = true, CacheItemPriority priority = CacheItemPriority.Normal, bool? cacheEnabledOverride = null, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(cacheKey);
		Guard.IsNotNull(actionFunction);

		try
		{
			if (IsCacheEnabled(cacheEnabledOverride))
			{
				string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);
				TimeSpan expirationTimeSpan = DetermineExpirationTimeSpan(expirationTimeSpanBuilder, priority);

				if (cacheMode is HybridCacheMode.Distributed)
				{
					try
					{
						(T cacheItem, UmbrellaDistributedCacheException? exception) = await DistributedCache.GetOrCreateAsync(cacheKeyInternal, actionFunction, () => BuildDistributedCacheEntryOptions(expirationTimeSpan, slidingExpiration), throwOnCacheFailure, cancellationToken).ConfigureAwait(false);

						if (!throwOnCacheFailure)
						{
							_ = Logger.WriteError(exception, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride });
						}
						else
						{
							if (exception is null)
								throw new HybridCacheException("There has been an unknown problem");

							throw exception;
						}

						return cacheItem;
					}
					catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
					{
						if (throwOnCacheFailure)
							throw new HybridCacheException("There has been a problem getting or creating the specified item in the cache.", exc);
					}
				}
				else
				{
					try
					{
						HybridCacheMetaEntry? cacheMetaEntry = null;

						if (TrackKeys)
							_ = MemoryCacheMetaEntryDictionary.TryGetValue(cacheKeyInternal, out cacheMetaEntry);

						// TODO: Investigate using AsyncLazy<T> to ensure that the factory only executes once. Internally, MemoryCache doesn't use locking
						// so the factory could run multiple times. Only a problem if the factory is expensive though.
						// Replace the boolean 'useMemoryCache' with an enum: CacheMode: Memory, MemoryMutex, Distributed, DistributedMutex

						// We can use the new SynchronizationManager we have created to implement granular locking.
						// We could also potentially provide a new argument to specify a condition that would force eviction
						// of the item from the cache, e.g. there is a property on the cached item, e.g. expiration date
						T? cacheItem = await MemoryCache.GetOrCreateAsync(cacheKeyInternal, async entry =>
						{
							TimeSpan expirationTimeSpan = expirationTimeSpanBuilder?.Invoke() ?? Options.DefaultCacheTimeout;

							MemoryCacheEntryOptions options = BuildMemoryCacheEntryOptions(expirationTimeSpan, slidingExpiration, priority, expirationTokensBuilder);
							_ = entry.SetOptions(options);

							T entryToAdd = await actionFunction().ConfigureAwait(false);

							if (entryToAdd is not null && TrackKeys)
							{
								cacheMetaEntry = new HybridCacheMetaEntry(cacheKeyInternal, expirationTimeSpan, slidingExpiration);
								_ = MemoryCacheMetaEntryDictionary.TryAdd(cacheKeyInternal, cacheMetaEntry);
								_ = entry.RegisterPostEvictionCallback((key, value, reason, state) => MemoryCacheMetaEntryDictionary.TryRemove(cacheKeyInternal, out HybridCacheMetaEntry? removedEntry));
							}

							return entryToAdd;

						}).ConfigureAwait(false);

						if (cacheMetaEntry is not null && TrackKeysAndHits)
							cacheMetaEntry.AddHit();

						return cacheItem ?? default!;
					}
					catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheKeyInternal, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
					{
						if (throwOnCacheFailure)
							throw new HybridCacheException("There has been a problem getting or creating the specified item in the cache.", exc);
					}
				}
			}

			return await actionFunction().ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
		{
			// If we get this far then there has definitely been a problem with the actionFunction. We need to throw here.
			throw new HybridCacheException("There has been a problem with the cache.", exc);
		}
	}

	/// <inheritdoc />
	public Task<T> GetOrCreateAsync<T>(string cacheKey, Func<Task<T>> actionFunction, CacheableUmbrellaOptions options, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
		=> GetOrCreateAsync(cacheKey, actionFunction, () => options.CacheTimeout, options.CacheMode, options.CacheSlidingExpiration, options.CacheThrowOnFailure, options.CachePriority, options.CacheEnabled, expirationTokensBuilder, cancellationToken);

	/// <inheritdoc />
	public (bool itemFound, T? cacheItem) TryGetValue<T>(string cacheKey, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool? cacheEnabledOverride = null)
	{
		Guard.IsNotNullOrWhiteSpace(cacheKey);

		try
		{
			if (!IsCacheEnabled(cacheEnabledOverride))
				return default;

			string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);

			if (cacheMode is HybridCacheMode.Distributed)
			{
				(bool itemFound, T? cacheItem, UmbrellaDistributedCacheException? exception) = DistributedCache.TryGetValue<T>(cacheKeyInternal);

				if (exception is not null)
					_ = Logger.WriteError(exception, new { cacheKey, cacheMode });

				return (itemFound, cacheItem);
			}
			else
			{
				bool found = MemoryCache.TryGetValue(cacheKeyInternal, out T? value);

				if (found && TrackKeysAndHits && MemoryCacheMetaEntryDictionary.TryGetValue(cacheKeyInternal, out HybridCacheMetaEntry? cacheMetaEntry))
					cacheMetaEntry.AddHit();

				return (found, value);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheMode }))
		{
			return default;
		}
	}

	/// <inheritdoc />
	public async Task<(bool itemFound, T? cacheItem)> TryGetValueAsync<T>(string cacheKey, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool? cacheEnabledOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(cacheKey);

		try
		{
			if (!IsCacheEnabled(cacheEnabledOverride))
				return default;

			string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);

			if (cacheMode is HybridCacheMode.Distributed)
			{
				(bool itemFound, T? cacheItem, UmbrellaDistributedCacheException? exception) = await DistributedCache.TryGetValueAsync<T>(cacheKeyInternal, cancellationToken).ConfigureAwait(false);

				if (exception is not null)
					_ = Logger.WriteError(exception, new { cacheKey, cacheKeyInternal, cacheMode });

				return (itemFound, cacheItem);
			}
			else
			{
				bool found = MemoryCache.TryGetValue(cacheKeyInternal, out T? value);

				if (found && TrackKeysAndHits && MemoryCacheMetaEntryDictionary.TryGetValue(cacheKeyInternal, out HybridCacheMetaEntry? cacheMetaEntry))
					cacheMetaEntry.AddHit();

				return (found, value);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, cacheMode }))
		{
			return default;
		}
	}

	/// <inheritdoc />
	public T SetValue<T>(string cacheKey, T value, TimeSpan? expirationTimeSpan = null, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool slidingExpiration = false, bool throwOnCacheFailure = true, CacheItemPriority priority = CacheItemPriority.Normal, bool? cacheEnabledOverride = null, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null)
	{
		Guard.IsNotNullOrWhiteSpace(cacheKey);
		Guard.IsNotNull(value);

		try
		{
			if (!IsCacheEnabled(cacheEnabledOverride))
				return value;

			string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);
			TimeSpan tsExpiration = DetermineExpirationTimeSpan(expirationTimeSpan, priority);

			if (cacheMode is HybridCacheMode.Distributed)
			{
				DistributedCacheEntryOptions options = BuildDistributedCacheEntryOptions(tsExpiration, slidingExpiration);

				if (value is null)
					DistributedCache.Remove(cacheKeyInternal);
				else
					DistributedCache.Set(cacheKeyInternal, value, options);
			}
			else
			{
				MemoryCacheEntryOptions options = BuildMemoryCacheEntryOptions(tsExpiration, slidingExpiration, priority, expirationTokensBuilder);

				if (TrackKeys)
					_ = options.RegisterPostEvictionCallback((key, cachedValue, reason, state) => MemoryCacheMetaEntryDictionary.TryRemove(cacheKeyInternal, out HybridCacheMetaEntry? removedEntry));

				_ = MemoryCache.Set(cacheKeyInternal, value, options);

				if (TrackKeys)
					_ = MemoryCacheMetaEntryDictionary.TryAdd(cacheKeyInternal, new HybridCacheMetaEntry(cacheKeyInternal, tsExpiration, slidingExpiration));
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, expirationTimeSpan, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
		{
			if (throwOnCacheFailure)
				throw new HybridCacheException("There has been a problem setting the specified item in the cache.", exc);
		}

		return value;
	}

	/// <inheritdoc />
	public T SetValue<T>(string cacheKey, T value, CacheableUmbrellaOptions options, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null)
		=> SetValue(cacheKey, value, options.CacheTimeout, options.CacheMode, options.CacheSlidingExpiration, options.CacheThrowOnFailure, options.CachePriority, options.CacheEnabled, expirationTokensBuilder);

	/// <inheritdoc />
	public async Task<T> SetValueAsync<T>(string cacheKey, T value, TimeSpan? expirationTimeSpan = null, HybridCacheMode cacheMode = HybridCacheMode.Memory, bool slidingExpiration = false, bool throwOnCacheFailure = true, CacheItemPriority priority = CacheItemPriority.Normal, bool? cacheEnabledOverride = null, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(cacheKey);

		try
		{
			if (!IsCacheEnabled(cacheEnabledOverride))
				return value;

			string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);
			TimeSpan tsExpiration = DetermineExpirationTimeSpan(expirationTimeSpan, priority);

			if (cacheMode is HybridCacheMode.Distributed)
			{
				DistributedCacheEntryOptions options = BuildDistributedCacheEntryOptions(tsExpiration, slidingExpiration);

				if (value is null)
					await DistributedCache.RemoveAsync(cacheKeyInternal, cancellationToken).ConfigureAwait(false);
				else
					await DistributedCache.SetAsync(cacheKeyInternal, value, options, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				MemoryCacheEntryOptions options = BuildMemoryCacheEntryOptions(tsExpiration, slidingExpiration, priority, expirationTokensBuilder);

				if (TrackKeys)
					_ = options.RegisterPostEvictionCallback((key, cachedValue, reason, state) => MemoryCacheMetaEntryDictionary.TryRemove(cacheKeyInternal, out HybridCacheMetaEntry? removedEntry));

				_ = MemoryCache.Set(cacheKeyInternal, value, options);

				if (TrackKeys)
					_ = MemoryCacheMetaEntryDictionary.TryAdd(cacheKeyInternal, new HybridCacheMetaEntry(cacheKeyInternal, tsExpiration, slidingExpiration));
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey, expirationTimeSpan, cacheMode, slidingExpiration, throwOnCacheFailure, priority, cacheEnabledOverride }))
		{
			if (throwOnCacheFailure)
				throw new HybridCacheException("There has been a problem setting the specified item in the cache.", exc);
		}

		return value;
	}

	/// <inheritdoc />
	public Task<T> SetValueAsync<T>(string cacheKey, T value, CacheableUmbrellaOptions options, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder = null, CancellationToken cancellationToken = default)
		=> SetValueAsync(cacheKey, value, options.CacheTimeout, options.CacheMode, options.CacheSlidingExpiration, options.CacheThrowOnFailure, options.CachePriority, options.CacheEnabled, expirationTokensBuilder, cancellationToken);

	/// <inheritdoc />
	public IReadOnlyCollection<HybridCacheMetaEntry> GetAllMemoryCacheMetaEntries()
	{
		if (!TrackKeys)
			throw new HybridCacheException("Meta entries cannot be retrieved when analytics is disabled.");

		try
		{
			return MemoryCacheMetaEntryDictionary.Select(x => x.Value).ToList();
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new HybridCacheException("There has been a problem reading the memory cache keys.", exc);
		}
	}

	/// <inheritdoc />
	public async Task RemoveAsync<T>(string cacheKey, HybridCacheMode cacheMode = HybridCacheMode.Memory, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(cacheKey);

		try
		{
			string cacheKeyInternal = CreateCacheKeyNormalized<T>(cacheKey);

			if (cacheMode is HybridCacheMode.Memory)
				MemoryCache.Remove(cacheKeyInternal);
			else if (cacheMode is HybridCacheMode.Distributed)
				await DistributedCache.RemoveAsync(cacheKeyInternal, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { cacheKey }))
		{
			throw new HybridCacheException("There has been a problem removing the item with the key: " + cacheKey, exc);
		}
	}

	/// <inheritdoc />
	public void ClearMemoryCache()
	{
		_nukeReaderWriterLock.EnterWriteLock();

		try
		{
			_nukeTokenSource.Cancel();
			_nukeTokenSource.Dispose();

			// Reset things so that all future items added to the MemoryCache use a new CancellationToken.
			_nukeTokenSource = new CancellationTokenSource();
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new HybridCacheException("There was a problem clearing all items from the memory cache.", exc);
		}
		finally
		{
			_nukeReaderWriterLock.ExitWriteLock();
		}
	}
	#endregion

	#region Protected Methods
	/// <summary>
	/// Creates the normalized cache key.
	/// </summary>
	/// <typeparam name="T">The type of the item being stored in the cache.</typeparam>
	/// <param name="key">The key.</param>
	/// <returns>The normalized cache key.</returns>
	protected virtual string CreateCacheKeyNormalized<T>(string key)
		=> LookupNormalizer.Normalize(Options.CacheKeyBuilder?.Invoke(typeof(T), key) ?? key);
	#endregion

	#region Private Methods
	private bool IsCacheEnabled(bool? cacheEnabledOverride)
		=> Options.CacheEnabled && cacheEnabledOverride.HasValue ? cacheEnabledOverride.Value : Options.CacheEnabled;

	private TimeSpan DetermineExpirationTimeSpan(Func<TimeSpan>? expirationTimeSpanBuilder, CacheItemPriority cacheItemPriority)
		=> DetermineExpirationTimeSpan(expirationTimeSpanBuilder?.Invoke(), cacheItemPriority);

	private TimeSpan DetermineExpirationTimeSpan(TimeSpan? expirationTimeSpan, CacheItemPriority cacheItemPriority)
		=> cacheItemPriority is CacheItemPriority.NeverRemove ? TimeSpan.FromDays(365 * 10) : expirationTimeSpan ?? Options.DefaultCacheTimeout;

	private static DistributedCacheEntryOptions BuildDistributedCacheEntryOptions(in TimeSpan expirationTimeSpan, bool slidingExpiration)
	{
		var options = new DistributedCacheEntryOptions();

		if (slidingExpiration)
		{
			_ = options.SetSlidingExpiration(expirationTimeSpan);
		}
		else
		{
			_ = options.SetAbsoluteExpiration(expirationTimeSpan);
		}

		return options;
	}

	private MemoryCacheEntryOptions BuildMemoryCacheEntryOptions(in TimeSpan expirationTimeSpan, bool slidingExpiration, CacheItemPriority priority, Func<IEnumerable<IChangeToken>?>? expirationTokensBuilder)
	{
		var options = new MemoryCacheEntryOptions();

		if (slidingExpiration)
		{
			_ = options.SetSlidingExpiration(expirationTimeSpan);
		}
		else
		{
			_ = options.SetAbsoluteExpiration(expirationTimeSpan);
		}

		_ = options.SetPriority(priority);

		if (expirationTokensBuilder is not null)
		{
			IEnumerable<IChangeToken>? tokens = expirationTokensBuilder();

			if (tokens is not null)
			{
				foreach (IChangeToken token in tokens)
				{
					if (token is not null)
						_ = options.AddExpirationToken(token);
				}
			}
		}

		_nukeReaderWriterLock.EnterReadLock();

		try
		{
			_ = options.AddExpirationToken(new CancellationChangeToken(_nukeTokenSource.Token));
		}
		finally
		{
			_nukeReaderWriterLock.ExitReadLock();
		}

		return options;
	}
	#endregion

	#region IDisposable Support
	private bool _isDisposed; // To detect redundant calls

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			if (disposing)
			{
				_nukeTokenSource.Cancel();
				_nukeTokenSource.Dispose();
				_nukeReaderWriterLock.Dispose();
			}

			_isDisposed = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		try
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new HybridCacheException("There has been a problem disposing this instance.", exc);
		}
	}
	#endregion
}