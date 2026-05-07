namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Represents a web URL and its optional version token.
/// </summary>
/// <param name="Url">The web URL.</param>
/// <param name="VersionToken">The optional version token associated with the URL.</param>
public readonly record struct UmbrellaVersionedUrl(string Url, string? VersionToken);
