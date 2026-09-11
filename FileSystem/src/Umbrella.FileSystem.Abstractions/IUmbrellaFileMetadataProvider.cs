namespace Umbrella.FileSystem.Abstractions;

/// <summary>Creates independent metadata sessions. Implementations must be safe to share between files.</summary>
public interface IUmbrellaFileMetadataProvider
{
	/// <summary>Creates a session without performing I/O or retaining disposable resources.</summary>
	IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context);

	/// <summary>Idempotently removes metadata after content deletion, including when content is already absent.</summary>
	Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default);

	/// <summary>Removes metadata after complete directory deletion. Match the directory and descendants at slash boundaries, within this namespace only.</summary>
	Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default);
}