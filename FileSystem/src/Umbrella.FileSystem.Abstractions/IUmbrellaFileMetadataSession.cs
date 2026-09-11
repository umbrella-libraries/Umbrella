namespace Umbrella.FileSystem.Abstractions;

/// <summary>A file-local metadata session. Concurrent use of a single session is not supported.</summary>
public interface IUmbrellaFileMetadataSession
{
	/// <summary>Whether metadata operations are supported. Unsupported sessions are skipped during file lifecycle operations.</summary>
	bool IsSupported { get; }
	/// <summary>Reads a value, including pending changes, without invoking file authorization.</summary>
	Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default);
	/// <summary>Stages a value; null removes it. Optionally persists all pending changes.</summary>
	Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default);
	/// <summary>Stages removal of a value and optionally persists all pending changes.</summary>
	Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default);
	/// <summary>Stages removal of all values and optionally persists all pending changes.</summary>
	Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default);
	/// <summary>Persists pending changes, retaining them if persistence fails.</summary>
	Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default);
	/// <summary>Reads an independent snapshot of persisted values, excluding pending changes.</summary>
	Task<IReadOnlyDictionary<string, object?>> ReadPersistedMetadataAsync(CancellationToken cancellationToken = default);
}