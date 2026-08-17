using System.Text.Json.Serialization;

namespace Umbrella.AI.Tools.Bundling.Models;

public sealed class AiBundleDefinition
{
    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Short name used as the heading prefix in generated skill and agent catalogue blocks, e.g.
    /// <c>Umbrella</c> renders <c>## Umbrella Skills</c>. Falls back to <see cref="BundleId"/>.
    /// </summary>
    [JsonPropertyName("catalogName")]
    public string CatalogName { get; set; } = "";

    /// <summary>
    /// Resolves the catalogue heading prefix, falling back to the bundle id when unset.
    /// </summary>
    [JsonIgnore]
    public string ResolvedCatalogName => string.IsNullOrWhiteSpace(CatalogName) ? BundleId : CatalogName;

    [JsonPropertyName("managedDirectories")]
    public List<string> ManagedDirectories { get; set; } = [];

    [JsonPropertyName("adapterDirectories")]
    public List<AdapterDirectoryDefinition> AdapterDirectories { get; set; } = [];

    [JsonPropertyName("skillListBlocks")]
    public List<SkillListBlockDefinition> SkillListBlocks { get; set; } = [];

    [JsonPropertyName("managedBlocks")]
    public List<ManagedBlockDefinition> ManagedBlocks { get; set; } = [];

    [JsonPropertyName("mcpSourcePath")]
    public string McpSourcePath { get; set; } = "";

    /// <summary>
    /// Files copied into the target repository on install only when the destination does not already
    /// exist. Used for starter configuration a consuming repository then owns outright.
    /// </summary>
    [JsonPropertyName("starterFiles")]
    public List<StarterFileDefinition> StarterFiles { get; set; } = [];
}

/// <summary>
/// A starter file copied on install when absent, then left entirely to the consuming repository.
/// </summary>
public sealed class StarterFileDefinition
{
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = "";

    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = "";
}

public sealed class AdapterDirectoryDefinition
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("targets")]
    public List<AdapterTarget> Targets { get; set; } = [];
}

public sealed class AdapterTarget
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = "";

    [JsonPropertyName("substitutions")]
    public Dictionary<string, string>? Substitutions { get; set; }
}

public sealed class SkillListBlockDefinition
{
    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = "";

    [JsonPropertyName("skillsDirectory")]
    public string SkillsDirectory { get; set; } = "";
    [JsonPropertyName("agentsDirectory")]
    public string? AgentsDirectory { get; set; }
}

public sealed class ManagedBlockDefinition
{
    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = "";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = "";
}