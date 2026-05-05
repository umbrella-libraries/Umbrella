using System.Text.Json.Nodes;
using Umbrella.AI.Tools.Services;
using Xunit;

namespace Umbrella.AI.Tools.Test;

public class BundleInstallerTest
{
    private static string RepoRoot => GetRepoRoot();

    [Fact]
    public void InstallCreatesManagedArtifacts()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();

        var result = installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "nuget-safe-upgrade.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".mcp.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "nuget-upgrade-exclusions.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "umbrella", "manifest.json")));

        string agents = File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md"));
        Assert.Contains("<!-- ai-bundle:umbrella:start -->", agents, StringComparison.Ordinal);

        JsonObject mcpServers = LoadServers(Path.Combine(workspace.RootPath, ".mcp.json"));
        Assert.NotNull(mcpServers["aspire"]);
        Assert.NotNull(mcpServers["playwright"]);
        Assert.Null(mcpServers["sql-mcp-server"]);
    }

    [Fact]
    public void InstallMergesExistingFilesWithoutOverwritingUserContent()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();
        string agentsPath = Path.Combine(workspace.RootPath, "AGENTS.md");
        string mcpPath = Path.Combine(workspace.RootPath, ".mcp.json");
        string exclusionsPath = Path.Combine(workspace.RootPath, "nuget-upgrade-exclusions.json");

        File.WriteAllText(agentsPath, "# Custom agent guidance\n\nUser-owned intro.");
        File.WriteAllText(exclusionsPath, "{\"packages\":[\"Contoso\"]}");
        File.WriteAllText(mcpPath, "{\"version\":1,\"inputs\":{\"token\":{\"type\":\"promptString\"}},\"servers\":{\"existing\":{\"type\":\"stdio\",\"command\":\"custom\"}}}");

        var result = installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.True(result.Success);
        string agents = File.ReadAllText(agentsPath);
        Assert.Contains("User-owned intro.", agents, StringComparison.Ordinal);
        Assert.Contains("<!-- ai-bundle:umbrella:start -->", agents, StringComparison.Ordinal);
        Assert.Equal("{\"packages\":[\"Contoso\"]}", File.ReadAllText(exclusionsPath));

        JsonObject mcpServers = LoadServers(mcpPath);
        Assert.NotNull(mcpServers["existing"]);
        Assert.NotNull(mcpServers["aspire"]);
        JsonObject mcpRoot = JsonNode.Parse(File.ReadAllText(mcpPath))!.AsObject();
        Assert.Equal(1, mcpRoot["version"]!.GetValue<int>());
        Assert.NotNull(mcpRoot["inputs"]);
    }

    [Fact]
    public void UpdateBlocksWhenManagedMcpServerHasDrifted()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();

        Assert.True(installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath }).Success);

        string mcpPath = Path.Combine(workspace.RootPath, ".mcp.json");
        JsonNode root = JsonNode.Parse(File.ReadAllText(mcpPath))!;
        root["servers"]!["aspire"]!["command"] = "changed";
        File.WriteAllText(mcpPath, root.ToJsonString());

        var result = installer.Update(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("aspire", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RemovePreservesUnownedContent()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();
        string agentsPath = Path.Combine(workspace.RootPath, "AGENTS.md");
        string mcpPath = Path.Combine(workspace.RootPath, ".mcp.json");

        File.WriteAllText(agentsPath, "# User heading");
        File.WriteAllText(mcpPath, "{\"version\":1,\"servers\":{\"existing\":{\"type\":\"stdio\",\"command\":\"custom\"}}}");
        Assert.True(installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath }).Success);

        var result = installer.Remove(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.True(result.Success);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "nuget-safe-upgrade.md")));
        Assert.Contains("# User heading", File.ReadAllText(agentsPath), StringComparison.Ordinal);
        Assert.DoesNotContain("ai-bundle:umbrella", File.ReadAllText(agentsPath), StringComparison.Ordinal);
        JsonObject mcpRoot = JsonNode.Parse(File.ReadAllText(mcpPath))!.AsObject();
        Assert.Equal(1, mcpRoot["version"]!.GetValue<int>());
        Assert.NotNull(LoadServers(mcpPath)["existing"]);
        Assert.Null(LoadServers(mcpPath)["aspire"]);
    }

    [Fact]
    public void InstallBlocksWhenAnotherBundleOwnsManagedFile()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();
        string conflictingPath = Path.Combine(workspace.RootPath, ".github", "agents", "nuget-safe-upgrade.agent.md");
        Directory.CreateDirectory(Path.GetDirectoryName(conflictingPath)!);
        File.WriteAllText(conflictingPath, "owned by another bundle");

        string otherManifestPath = Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "other.bundle", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(otherManifestPath)!);
        var otherManifest = new
        {
            bundleId = "other.bundle",
            bundleVersion = "1.0.0",
            installerPackageId = "Other",
            installerVersion = "1.0.0",
            installedAt = "2025-01-01T00:00:00+00:00",
            managedFiles = new[] { new { path = ".github\\agents\\nuget-safe-upgrade.agent.md", hash = "ABC" } },
            managedBlocks = Array.Empty<object>(),
            managedMcpServers = Array.Empty<object>()
        };
        File.WriteAllText(otherManifestPath, System.Text.Json.JsonSerializer.Serialize(otherManifest));

        var result = installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("another bundle", StringComparison.OrdinalIgnoreCase));
    }

    private static AiBundleInstaller CreateInstaller() => new(RepoRoot, "Umbrella.AI.Tools.Test", "1.0.0-test");

    private static JsonObject LoadServers(string path) => JsonNode.Parse(File.ReadAllText(path))!["servers"]!.AsObject();

    private static string GetRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".ai-shared"))
                && Directory.Exists(Path.Combine(directory.FullName, ".github"))
                && Directory.Exists(Path.Combine(directory.FullName, ".claude")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to locate the repository root for tests.");
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Umbrella.AI.Tools.Test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(Path.Combine(RootPath, ".git"));
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
