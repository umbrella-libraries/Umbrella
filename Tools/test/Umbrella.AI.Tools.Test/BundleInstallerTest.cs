using System.Text.Json.Nodes;
using Umbrella.AI.Tools.Services;

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
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "umbrella-nuget-safe-upgrade.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "agents", "umbrella-nuget-safe-upgrade.agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "umbrella-dotnet-blazor-admin-crud-feature-agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "agents", "umbrella-dotnet-blazor-admin-crud-feature-agent.agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "skills", "umbrella-dotnet-scaffold-service", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "skills", "umbrella-dotnet-scaffold-service", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".mcp.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "nuget-upgrade-exclusions.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "umbrella", "manifest.json")));

        string claudeSkill = File.ReadAllText(Path.Combine(workspace.RootPath, ".claude", "skills", "umbrella-dotnet-add-ef-migration", "SKILL.md"));
        string githubSkill = File.ReadAllText(Path.Combine(workspace.RootPath, ".github", "skills", "umbrella-dotnet-add-ef-migration", "SKILL.md"));
        Assert.Contains(@".claude\skills\umbrella-dotnet-add-ef-migration", claudeSkill, StringComparison.Ordinal);
        Assert.Contains(@".github\skills\umbrella-dotnet-add-ef-migration", githubSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", claudeSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", githubSkill, StringComparison.Ordinal);

        string agents = File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md"));
        Assert.Contains("<!-- ai-bundle:umbrella:start -->", agents, StringComparison.Ordinal);
        Assert.Contains("## Umbrella Agents", agents, StringComparison.Ordinal);
        Assert.Contains("`umbrella-dotnet-core-service-agent`", agents, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", agents, StringComparison.Ordinal);

        string claudeGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, "CLAUDE.md"));
        Assert.Contains("## Umbrella Agents", claudeGuidance, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", claudeGuidance, StringComparison.Ordinal);

        string copilotGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, ".github", "copilot-instructions.md"));
        Assert.Contains("## Umbrella Agents", copilotGuidance, StringComparison.Ordinal);
        Assert.Contains(@".github\agents\umbrella-dotnet-core-service-agent.agent.md", copilotGuidance, StringComparison.Ordinal);

        JsonObject mcpServers = LoadServers(Path.Combine(workspace.RootPath, ".mcp.json"));
        Assert.NotNull(mcpServers["aspire"]);
        Assert.NotNull(mcpServers["playwright"]);
        Assert.Null(mcpServers["sql-mcp-server"]);
        JsonObject legacyMcpServers = LoadMcpServers(Path.Combine(workspace.RootPath, ".mcp.json"));
        Assert.NotNull(legacyMcpServers["aspire"]);
        Assert.NotNull(legacyMcpServers["playwright"]);

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
        Assert.Contains("## Umbrella Agents", agents, StringComparison.Ordinal);
        Assert.Contains("`umbrella-dotnet-core-service-agent`", agents, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", agents, StringComparison.Ordinal);

        string claudeGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, "CLAUDE.md"));
        Assert.Contains("## Umbrella Agents", claudeGuidance, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", claudeGuidance, StringComparison.Ordinal);

        string copilotGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, ".github", "copilot-instructions.md"));
        Assert.Contains("## Umbrella Agents", copilotGuidance, StringComparison.Ordinal);
        Assert.Contains(@".github\agents\umbrella-dotnet-core-service-agent.agent.md", copilotGuidance, StringComparison.Ordinal);
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
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "umbrella-nuget-safe-upgrade.md")));
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
        string conflictingPath = Path.Combine(workspace.RootPath, ".github", "agents", "umbrella-nuget-safe-upgrade.agent.md");
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
            managedFiles = new[] { new { path = ".github\\agents\\umbrella-nuget-safe-upgrade.agent.md", hash = "ABC" } },
            managedBlocks = Array.Empty<object>(),
            managedMcpServers = Array.Empty<object>()
        };
        File.WriteAllText(otherManifestPath, System.Text.Json.JsonSerializer.Serialize(otherManifest));

        var result = installer.Install(new Umbrella.AI.Tools.CommandOptions { TargetPath = workspace.RootPath });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("another bundle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SyncPropagatesCanonicalEditInASingleRun()
    {
        using var workspace = new TemporaryWorkspace();
        CreateSyncWorkspace(workspace.RootPath);
        var installer = CreateInstaller();

        Assert.True(installer.Sync(workspace.RootPath).Success);
        string claudeSkillPath = Path.Combine(workspace.RootPath, ".claude", "skills", "sample-skill", "SKILL.md");
        Assert.Contains(@".claude\skills", File.ReadAllText(claudeSkillPath), StringComparison.Ordinal);

        string canonicalPath = Path.Combine(workspace.RootPath, ".ai-shared", "skills", "sample-skill", "SKILL.md");
        File.AppendAllText(canonicalPath, "\nEdited line.\n");

        var result = installer.Sync(workspace.RootPath);

        Assert.True(result.Success);

        foreach (string adapterDir in new[] { ".claude", ".github", ".agents" })
        {
            string content = File.ReadAllText(Path.Combine(workspace.RootPath, adapterDir, "skills", "sample-skill", "SKILL.md"));
            Assert.Contains("Edited line.", content, StringComparison.Ordinal);
            Assert.DoesNotContain("{{skill_dir}}", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SyncResolvesRepositoryRootFromSubdirectory()
    {
        using var workspace = new TemporaryWorkspace();
        CreateSyncWorkspace(workspace.RootPath);
        var installer = CreateInstaller();
        string subdirectory = Path.Combine(workspace.RootPath, ".ai-shared", "agents");

        var result = installer.Sync(subdirectory);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "sample-agent.md")));
    }

    [Fact]
    public void SyncFailsWithClearMessageOutsideRepository()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();

        var result = installer.Sync(workspace.RootPath);

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("bundle.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SyncDoesNotRewriteUnchangedTargets()
    {
        using var workspace = new TemporaryWorkspace();
        CreateSyncWorkspace(workspace.RootPath);
        var installer = CreateInstaller();
        Assert.True(installer.Sync(workspace.RootPath).Success);

        string targetPath = Path.Combine(workspace.RootPath, ".claude", "skills", "sample-skill", "SKILL.md");
        DateTime firstWriteTime = File.GetLastWriteTimeUtc(targetPath);

        var result = installer.Sync(workspace.RootPath);

        Assert.True(result.Success);
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(targetPath));
        Assert.Contains(result.Messages, x => x.StartsWith("Unchanged:", StringComparison.Ordinal));
    }

    private static void CreateSyncWorkspace(string root)
    {
        string skillDir = Path.Combine(root, ".ai-shared", "skills", "sample-skill");
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: sample-skill\ndescription: Sample skill.\n---\n\nRun the script in `{{skill_dir}}`.\n");

        string agentDir = Path.Combine(root, ".ai-shared", "agents", "claude");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, "sample-agent.md"), "# Sample agent\n");

        string bundleDir = Path.Combine(root, ".ai-shared", "bundles", "umbrella");
        Directory.CreateDirectory(bundleDir);
        File.WriteAllText(Path.Combine(bundleDir, "bundle.json"), """
        {
          "bundleId": "umbrella",
          "adapterDirectories": [
            {
              "source": ".ai-shared\\skills",
              "targets": [
                { "destination": ".claude\\skills", "substitutions": { "{{skill_dir}}": ".claude\\skills" } },
                { "destination": ".github\\skills", "substitutions": { "{{skill_dir}}": ".github\\skills" } },
                { "destination": ".agents\\skills", "substitutions": { "{{skill_dir}}": ".agents\\skills" } }
              ]
            },
            {
              "source": ".ai-shared\\agents\\claude",
              "targets": [ { "destination": ".claude\\agents" } ]
            }
          ],
          "skillListBlocks": [
            { "targetPath": ".ai-shared\\bundles\\umbrella\\blocks\\CLAUDE.block.md", "skillsDirectory": ".claude\\skills" }
          ]
        }
        """);
    }

    private static AiBundleInstaller CreateInstaller() => new(RepoRoot, "Umbrella.AI.Tools.Test", "1.0.0-test");

    private static JsonObject LoadServers(string path) => JsonNode.Parse(File.ReadAllText(path))!["servers"]!.AsObject();
    private static JsonObject LoadMcpServers(string path) => JsonNode.Parse(File.ReadAllText(path))!["mcpServers"]!.AsObject();

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
