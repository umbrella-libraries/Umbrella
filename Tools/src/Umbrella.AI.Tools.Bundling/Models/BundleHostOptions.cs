namespace Umbrella.AI.Tools.Bundling.Models;

/// <summary>
/// Identifies the bundle a consuming installer tool owns. Everything the engine needs to know about
/// the hosting tool is supplied here so no repository-specific identifier is compiled into the engine.
/// </summary>
public sealed record BundleHostOptions
{
    /// <summary>
    /// The stable namespaced bundle id, e.g. <c>umbrella</c>. Determines the bundle definition path,
    /// the manifest location, and the managed block markers.
    /// </summary>
    public required string BundleId { get; init; }

    /// <summary>
    /// Human readable bundle name used in command line help, e.g. <c>Umbrella AI skills and agents</c>.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The NuGet package id of the hosting tool, recorded in installed manifests.
    /// </summary>
    public required string InstallerPackageId { get; init; }

    /// <summary>
    /// The version of the hosting tool, recorded in installed manifests.
    /// </summary>
    public required string InstallerVersion { get; init; }

    /// <summary>
    /// Name of the environment variable that can override asset root discovery,
    /// e.g. <c>UMBRELLA_AI_ASSET_ROOT</c>.
    /// </summary>
    public required string AssetRootEnvironmentVariable { get; init; }

    /// <summary>
    /// Path of the bundle definition relative to an asset root or repository root.
    /// </summary>
    public string BundleDefinitionRelativePath => Path.Combine(".ai-shared", "bundles", BundleId, "bundle.json");
}
