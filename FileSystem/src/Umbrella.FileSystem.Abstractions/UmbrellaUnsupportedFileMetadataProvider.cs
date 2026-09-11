namespace Umbrella.FileSystem.Abstractions;

/// <summary>A metadata backend for storage providers that do not support metadata by default.</summary>
public sealed class UmbrellaUnsupportedFileMetadataProvider : IUmbrellaFileMetadataProvider
{
	/// <inheritdoc />
	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context) => new Session();
	/// <inheritdoc />
	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	private sealed class Session : IUmbrellaFileMetadataSession
	{
		public bool IsSupported => false;
		public Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default) => throw Unsupported();
		public Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default) => throw Unsupported();
		public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default) => throw Unsupported();
		public Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default) => throw Unsupported();
		public Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default) => throw Unsupported();
		public Task<IReadOnlyDictionary<string, object?>> ReadPersistedMetadataAsync(CancellationToken cancellationToken = default) => throw Unsupported();
		private static NotSupportedException Unsupported() => new("Metadata is not supported by the configured file provider.");
	}
}