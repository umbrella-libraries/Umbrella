using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Umbrella.AI.Tools.Bundling.Models;

public sealed class AiBundleManifest
{
    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = "";

    [JsonPropertyName("bundleVersion")]
    public string BundleVersion { get; set; } = "";

    [JsonPropertyName("installerPackageId")]
    public string InstallerPackageId { get; set; } = "";

    [JsonPropertyName("installerVersion")]
    public string InstallerVersion { get; set; } = "";

    [JsonPropertyName("installedAt")]
    public DateTimeOffset InstalledAt { get; set; }

    [JsonPropertyName("managedFiles")]
    public List<PathHashRecord> ManagedFiles { get; set; } = [];

    [JsonPropertyName("managedBlocks")]
    public List<PathHashRecord> ManagedBlocks { get; set; } = [];

    [JsonPropertyName("managedMcpServers")]
    public List<NameHashRecord> ManagedMcpServers { get; set; } = [];

    /// <summary>
    /// Effective Codex server definitions after applying this bundle's Codex-specific overrides.
    /// Persisting them lets another installed bundle rebuild the shared Codex region without needing
    /// this bundle's original authoring sources.
    /// </summary>
    [JsonPropertyName("managedCodexMcpServers")]
    public List<NameConfigurationRecord> ManagedCodexMcpServers { get; set; } = [];

    [JsonPropertyName("managedCodexMcp")]
    public PathHashRecord? ManagedCodexMcp { get; set; }
}

public sealed class PathHashRecord
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";
}

public sealed class NameHashRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";
}

public sealed class OperationResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; } = [];
    public List<string> Conflicts { get; } = [];
}

public sealed class NameConfigurationRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("configuration")]
    public JsonObject Configuration { get; set; } = [];
}
