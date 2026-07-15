using System.Text.Json.Serialization;

namespace Umbrella.AI.Tools.Models;

public sealed class AiBundleDefinition
{
    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

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

    [JsonPropertyName("exclusionsStarterPath")]
    public string ExclusionsStarterPath { get; set; } = "";
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