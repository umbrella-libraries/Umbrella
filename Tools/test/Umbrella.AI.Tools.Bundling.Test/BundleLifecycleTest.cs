using System.Text.Json;
using System.Text.Json.Nodes;

namespace Umbrella.AI.Tools.Bundling.Test;

public class BundleLifecycleTest
{
    [Fact]
    public void InstallCreatesEveryManagedArtifact()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") }, starterFile: "alpha-starter.json");
        using var repo = new TemporaryDirectory(asRepository: true);

        var result = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.True(File.Exists(repo.Combine(".claude", "skills", "alpha-sample-skill", "SKILL.md")));
        Assert.True(File.Exists(repo.Combine(".agents", "skills", "alpha-sample-skill", "SKILL.md")));
        Assert.True(File.Exists(repo.Combine(".claude", "agents", "alpha-sample-agent.md")));
        Assert.True(File.Exists(repo.Combine("CLAUDE.md")));
        Assert.True(File.Exists(repo.Combine(".mcp.json")));
        Assert.True(File.Exists(repo.Combine(".codex", "config.toml")));
        Assert.True(File.Exists(repo.Combine("alpha-starter.json")));
        Assert.True(File.Exists(repo.Combine(".ai-shared", "bundles", "alpha", "manifest.json")));
    }

    [Fact]
    public void InstallAppliesPerTargetSubstitutions()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string claudeSkill = File.ReadAllText(repo.Combine(".claude", "skills", "alpha-sample-skill", "SKILL.md"));
        string agentsSkill = File.ReadAllText(repo.Combine(".agents", "skills", "alpha-sample-skill", "SKILL.md"));

        Assert.Contains(@".claude\skills", claudeSkill, StringComparison.Ordinal);
        Assert.Contains(@".agents\skills", agentsSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", claudeSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", agentsSkill, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallPreservesUserContentOutsideManagedRegions()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        const string unrelatedCodex = "model = \"gpt-5\"\r\n";

        File.WriteAllText(repo.Combine("CLAUDE.md"), "# House rules\n\nUser-owned intro.");
        File.WriteAllText(repo.Combine(".mcp.json"), "{\"version\":1,\"servers\":{\"user-server\":{\"type\":\"stdio\",\"command\":\"user\"}}}");
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        File.WriteAllText(repo.Combine(".codex", "config.toml"), unrelatedCodex);

        Assert.True(fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string claude = File.ReadAllText(repo.Combine("CLAUDE.md"));
        Assert.Contains("User-owned intro.", claude, StringComparison.Ordinal);
        Assert.Contains("<!-- ai-bundle:alpha:start -->", claude, StringComparison.Ordinal);

        JsonObject mcpRoot = JsonNode.Parse(File.ReadAllText(repo.Combine(".mcp.json")))!.AsObject();
        Assert.Equal(1, mcpRoot["version"]!.GetValue<int>());
        Assert.NotNull(ConfigAssert.Servers(repo.Combine(".mcp.json"))["user-server"]);
        Assert.NotNull(ConfigAssert.Servers(repo.Combine(".mcp.json"))["shared"]);
        Assert.StartsWith(unrelatedCodex, File.ReadAllText(repo.Combine(".codex", "config.toml")), StringComparison.Ordinal);
    }

    [Fact]
    public void InstallDoesNotOverwriteAnExistingStarterFile()
    {
        using var fixture = FixtureBundle.Create("alpha", starterFile: "alpha-starter.json");
        using var repo = new TemporaryDirectory(asRepository: true);
        File.WriteAllText(repo.Combine("alpha-starter.json"), "{\"userOwned\":true}");

        Assert.True(fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        Assert.Equal("{\"userOwned\":true}", File.ReadAllText(repo.Combine("alpha-starter.json")));
    }

    [Fact]
    public void InstallSucceedsForABundleThatDeclaresNoStarterFilesOrMcpServers()
    {
        // Regression: an empty starter path previously reached File.Copy with the asset root directory.
        using var fixture = FixtureBundle.Create("alpha");
        using var repo = new TemporaryDirectory(asRepository: true);

        var result = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.False(File.Exists(repo.Combine(".mcp.json")));
    }

    [Fact]
    public void StatusReportsCleanInstallThenDetectsFileDrift()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        Assert.True(installer.GetStatus(new CommandOptions { TargetPath = repo.Path }).Success);

        File.AppendAllText(repo.Combine(".claude", "agents", "alpha-sample-agent.md"), "\nhand edited\n");
        var drifted = installer.GetStatus(new CommandOptions { TargetPath = repo.Path });

        Assert.False(drifted.Success);
        Assert.Contains(drifted.Conflicts, x => x.Contains("alpha-sample-agent.md", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdateBlocksWhenAManagedMcpServerHasDrifted()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        JsonNode root = JsonNode.Parse(File.ReadAllText(repo.Combine(".mcp.json")))!;
        root["servers"]!["shared"]!["command"] = "changed";
        File.WriteAllText(repo.Combine(".mcp.json"), root.ToJsonString());

        var result = installer.Update(new CommandOptions { TargetPath = repo.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("shared", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateBlocksWhenTheCodexRegionNoLongerDeclaresOwnedServers()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        File.WriteAllText(codexPath, File.ReadAllText(codexPath).Replace("\"shared\"", "\"renamed\"", StringComparison.Ordinal));

        var result = installer.Update(new CommandOptions { TargetPath = repo.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("Codex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateRemovesServersThatLeftTheBundle()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject
        {
            ["keep"] = ConfigAssert.StdioServer("keep"),
            ["drop"] = ConfigAssert.StdioServer("drop")
        });
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string sourceMcpPath = Path.Combine(fixture.AssetRoot, ".mcp.json");
        File.WriteAllText(sourceMcpPath, new JsonObject
        {
            ["servers"] = new JsonObject { ["keep"] = ConfigAssert.StdioServer("keep") }
        }.ToJsonString());

        var result = installer.Update(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.Null(ConfigAssert.Servers(repo.Combine(".mcp.json"))["drop"]);
        Assert.DoesNotContain("drop", ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")));
    }

    [Fact]
    public void InstallBlocksWhenAnotherBundleOwnsAManagedFile()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var repo = new TemporaryDirectory(asRepository: true);
        string contested = repo.Combine(".claude", "agents", "alpha-sample-agent.md");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(contested)!);
        File.WriteAllText(contested, "owned by another bundle");

        WriteForeignManifest(repo.Path, "other.bundle", managedFiles: [@".claude\agents\alpha-sample-agent.md"]);

        var result = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("other.bundle", StringComparison.Ordinal));
    }

    [Fact]
    public void RemoveDeletesOwnedContentAndLeavesUserContentIntact()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        const string unrelatedCodex = "model = \"gpt-5\"\n";

        File.WriteAllText(repo.Combine("CLAUDE.md"), "# User heading");
        File.WriteAllText(repo.Combine(".mcp.json"), "{\"version\":1,\"servers\":{\"user-server\":{\"type\":\"stdio\",\"command\":\"user\"}}}");
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        File.WriteAllText(repo.Combine(".codex", "config.toml"), unrelatedCodex);
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var result = installer.Remove(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.False(File.Exists(repo.Combine(".claude", "agents", "alpha-sample-agent.md")));
        Assert.Contains("# User heading", File.ReadAllText(repo.Combine("CLAUDE.md")), StringComparison.Ordinal);
        Assert.DoesNotContain("ai-bundle:alpha", File.ReadAllText(repo.Combine("CLAUDE.md")), StringComparison.Ordinal);
        Assert.NotNull(ConfigAssert.Servers(repo.Combine(".mcp.json"))["user-server"]);
        Assert.Null(ConfigAssert.Servers(repo.Combine(".mcp.json"))["shared"]);
        Assert.StartsWith(unrelatedCodex, File.ReadAllText(repo.Combine(".codex", "config.toml")), StringComparison.Ordinal);
        Assert.DoesNotContain("ai-bundle:codex-mcp", File.ReadAllText(repo.Combine(".codex", "config.toml")), StringComparison.Ordinal);
        Assert.False(File.Exists(repo.Combine(".ai-shared", "bundles", "alpha", "manifest.json")));
    }

    [Fact]
    public void InstallRejectsATargetThatDoesNotLookLikeARepository()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var plainDirectory = new TemporaryDirectory();

        var blocked = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = plainDirectory.Path });
        Assert.False(blocked.Success);
        Assert.Contains(blocked.Conflicts, x => x.Contains("does not look like a repository", StringComparison.Ordinal));

        var allowed = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = plainDirectory.Path, AllowNonRepo = true });
        Assert.True(allowed.Success, string.Join("; ", allowed.Conflicts));
    }

    internal static void WriteForeignManifest(
        string repoRoot,
        string bundleId,
        IEnumerable<string>? managedFiles = null,
        IEnumerable<(string Name, string Hash)>? managedMcpServers = null)
    {
        string manifestPath = Path.Combine(repoRoot, ".ai-shared", "bundles", bundleId, "manifest.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        var manifest = new
        {
            bundleId,
            bundleVersion = "1.0.0",
            installerPackageId = "Foreign",
            installerVersion = "1.0.0",
            installedAt = "2025-01-01T00:00:00+00:00",
            managedFiles = (managedFiles ?? []).Select(x => new { path = x, hash = "ABC" }).ToArray(),
            managedBlocks = Array.Empty<object>(),
            managedMcpServers = (managedMcpServers ?? []).Select(x => new { name = x.Name, hash = x.Hash }).ToArray()
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
    }
}
