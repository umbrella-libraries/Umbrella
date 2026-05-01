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

    [JsonPropertyName("managedBlocks")]
    public List<ManagedBlockDefinition> ManagedBlocks { get; set; } = [];

    [JsonPropertyName("mcpTemplatePath")]
    public string McpTemplatePath { get; set; } = "";

    [JsonPropertyName("exclusionsStarterPath")]
    public string ExclusionsStarterPath { get; set; } = "";
}

public sealed class ManagedBlockDefinition
{
    [JsonPropertyName("targetPath")]
    public string TargetPath { get; set; } = "";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = "";
}