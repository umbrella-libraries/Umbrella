using CommunityToolkit.Diagnostics;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>Reusable caching and deferred-mutation support. Backends implement reads and atomic application of mutations.</summary>
public abstract class UmbrellaFileMetadataSession : IUmbrellaFileMetadataSession
{
	private readonly IGenericTypeConverter _converter;
	private readonly StringComparer _keyComparer;
	private readonly Dictionary<string, object?> _pending;
	private Dictionary<string, object?>? _cache;
	private bool _clear;

	/// <summary>Creates a file-local session with the backend's metadata key comparison rules.</summary>
	protected UmbrellaFileMetadataSession(IGenericTypeConverter converter, StringComparer? keyComparer = null)
	{
		Guard.IsNotNull(converter);
		_converter = converter;
		_keyComparer = keyComparer ?? StringComparer.Ordinal;
		_pending = new(_keyComparer);
	}

	/// <inheritdoc />
	public bool IsSupported => true;

	/// <inheritdoc />
	public async Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(key);
		key = NormalizeKey(key);
		if (_pending.TryGetValue(key, out object? pending))
			return pending is null ? fallback : _converter.Convert(ConvertToString(pending), fallback, customValueConverter)!;
		if (_clear)
			return fallback;
		_cache ??= new Dictionary<string, object?>(await ReadCoreAsync(cancellationToken).ConfigureAwait(false), _keyComparer);
		return _cache.TryGetValue(key, out object? value) && value is not null
			? _converter.Convert(ConvertToString(value), fallback, customValueConverter)!
			: fallback;
	}

	/// <inheritdoc />
	public async Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(key);
		if (!TryConvertValue(key, value, out object? converted))
			return;
		key = NormalizeKey(key);
		_pending[key] = converted;
		if (writeChanges)
			await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
		=> SetMetadataValueAsync<object?>(key, null, writeChanges, cancellationToken);

	/// <inheritdoc />
	public async Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_clear = true;
		_pending.Clear();
		if (writeChanges)
			await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_clear && _pending.Count == 0)
			return;
		var updatedCache = _clear
			? new Dictionary<string, object?>(_keyComparer)
			: new Dictionary<string, object?>(_cache ?? await ReadCoreAsync(cancellationToken).ConfigureAwait(false), _keyComparer);
		foreach (var pair in _pending)
		{
			if (pair.Value is null)
				_ = updatedCache.Remove(pair.Key);
			else
				updatedCache[pair.Key] = pair.Value;
		}

		await WriteCoreAsync(new Dictionary<string, object?>(_pending, _keyComparer), _clear, cancellationToken).ConfigureAwait(false);
		_pending.Clear();
		_clear = false;
		_cache = updatedCache;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, object?>> ReadPersistedMetadataAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return new Dictionary<string, object?>(await ReadCoreAsync(cancellationToken).ConfigureAwait(false), _keyComparer);
	}

	/// <summary>Converts a value for persistence. Return false for unsupported keys. The default preserves the original value.</summary>
	protected virtual bool TryConvertValue(string key, object? value, out object? converted)
	{
		converted = value;
		return true;
	}

	/// <summary>Converts a stored value for the generic type converter.</summary>
	protected virtual string? ConvertToString(object? value) => value?.ToString();

	/// <summary>Resolves backend aliases to one session key. The default leaves keys unchanged.</summary>
	protected virtual string NormalizeKey(string key) => key;

	/// <summary>Reads persisted logical keys and values without incorporating session state.</summary>
	protected abstract Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken);

	/// <summary>Applies clear first, then changes (null means remove). Implement atomically where possible; retries must be idempotent.</summary>
	protected abstract Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken);
}
