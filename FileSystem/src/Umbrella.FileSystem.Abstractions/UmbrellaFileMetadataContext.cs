namespace Umbrella.FileSystem.Abstractions;

/// <summary>Identifies metadata before storage-specific path translation.</summary>
/// <param name="FileInfo">The file whose metadata is being accessed.</param>
/// <param name="StorageNamespace">Application-assigned stable namespace; null for an unconfigured native backend.</param>
/// <param name="SubPath">The normalized logical file path.</param>
public sealed record UmbrellaFileMetadataContext(IUmbrellaFileInfo FileInfo, string? StorageNamespace, string SubPath);