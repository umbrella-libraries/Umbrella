using System.Text.Json.Nodes;

namespace Umbrella.AI.Tools.Bundling.Test;

/// <summary>
/// Sync regenerates adapters and derived MCP outputs from a bundle's canonical sources, so it runs
/// against the authoring repository. A fixture's asset root already has that exact shape.
/// </summary>
public class SyncTest
{
    [Fact]
    public void SyncPropagatesACanonicalEditToEveryAdapterInOneRun()
    {
        using var fixture = FixtureBundle.Create("alpha");
        var installer = fixture.CreateInstaller();

        Assert.True(installer.Sync(fixture.AssetRoot).Success);
        Assert.Contains(
            @".claude\skills",
            File.ReadAllText(Path.Combine(fixture.AssetRoot, ".claude", "skills", "alpha-sample-skill", "SKILL.md")),
            StringComparison.Ordinal);

        File.AppendAllText(Path.Combine(fixture.AssetRoot, ".ai-shared", "skills", "alpha-sample-skill", "SKILL.md"), "\nEdited line.\n");

        var result = installer.Sync(fixture.AssetRoot);

        Assert.True(result.Success, string.Join("; ", result.Conflicts));

        foreach (string adapterDir in new[] { ".claude", ".agents" })
        {
            string content = File.ReadAllText(Path.Combine(fixture.AssetRoot, adapterDir, "skills", "alpha-sample-skill", "SKILL.md"));
            Assert.Contains("Edited line.", content, StringComparison.Ordinal);
            Assert.DoesNotContain("{{skill_dir}}", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SyncResolvesTheRepositoryRootFromASubdirectory()
    {
        using var fixture = FixtureBundle.Create("alpha");

        var result = fixture.CreateInstaller().Sync(Path.Combine(fixture.AssetRoot, ".ai-shared", "agents"));

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.True(File.Exists(Path.Combine(fixture.AssetRoot, ".claude", "agents", "alpha-sample-agent.md")));
    }

    [Fact]
    public void SyncFailsWithAClearMessageOutsideAnAuthoringRepository()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var elsewhere = new TemporaryDirectory();

        var result = fixture.CreateInstaller().Sync(elsewhere.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("bundle.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SyncFailsWithAClearMessageWhenAnAdapterSourceIsMissing()
    {
        // Regression: this used to throw DirectoryNotFoundException. An installed target repository has
        // the bundle definition but not the canonical sources, so it lands here on every run.
        using var fixture = FixtureBundle.Create("alpha");
        Directory.Delete(Path.Combine(fixture.AssetRoot, ".ai-shared", "skills"), recursive: true);

        var result = fixture.CreateInstaller().Sync(fixture.AssetRoot);

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x =>
            x.Contains(@".ai-shared\skills", StringComparison.Ordinal)
            && x.Contains("authors the bundle", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncDoesNotRewriteUnchangedTargets()
    {
        using var fixture = FixtureBundle.Create("alpha");
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Sync(fixture.AssetRoot).Success);

        string targetPath = Path.Combine(fixture.AssetRoot, ".claude", "skills", "alpha-sample-skill", "SKILL.md");
        DateTime firstWriteTime = File.GetLastWriteTimeUtc(targetPath);

        var result = installer.Sync(fixture.AssetRoot);

        Assert.True(result.Success);
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(targetPath));
        Assert.Contains(result.Messages, x => x.StartsWith("Unchanged:", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncGeneratesTheCatalogueBlockFromSkillAndAgentFrontmatter()
    {
        using var fixture = FixtureBundle.Create("alpha", catalogName: "Fixture");

        Assert.True(fixture.CreateInstaller().Sync(fixture.AssetRoot).Success);

        string block = File.ReadAllText(Path.Combine(fixture.AssetRoot, ".ai-shared", "bundles", "alpha", "blocks", "CLAUDE.block.md"));
        Assert.Contains("## Fixture Skills", block, StringComparison.Ordinal);
        Assert.Contains("## Fixture Agents", block, StringComparison.Ordinal);
        Assert.Contains("`alpha-sample-skill` -- Sample skill for alpha.", block, StringComparison.Ordinal);
        Assert.Contains(@"Playbook: `.claude\agents\alpha-sample-agent.md`", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncKeepsAnotherInstalledBundlesSkillsAndAgentsOutOfThisBundlesCatalogueBlock()
    {
        // An authoring repo can also have bundles installed into it, and both bundles' adapters land
        // in the same .claude\skills and .claude\agents directories. The catalogue block must
        // enumerate this bundle's canonical sources, not the shared destinations, or every bundle
        // ends up advertising its sibling's content under its own heading.
        using var alpha = FixtureBundle.Create("alpha", catalogName: "Fixture");
        using var beta = FixtureBundle.Create("beta", catalogName: "Other");
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        alpha.CopyCanonicalSourcesTo(repo.Path);

        // beta's adapters really are present in the directories the block points readers at.
        Assert.True(File.Exists(repo.Combine(".claude", "agents", "beta-sample-agent.md")));
        Assert.True(File.Exists(repo.Combine(".claude", "skills", "beta-sample-skill", "SKILL.md")));

        var result = alpha.CreateInstaller().Sync(repo.Path);
        Assert.True(result.Success, string.Join("; ", result.Conflicts));

        string block = File.ReadAllText(repo.Combine(".ai-shared", "bundles", "alpha", "blocks", "CLAUDE.block.md"));
        Assert.Contains("`alpha-sample-skill` -- Sample skill for alpha.", block, StringComparison.Ordinal);
        Assert.Contains(@"Playbook: `.claude\agents\alpha-sample-agent.md`", block, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-sample-agent", block, StringComparison.Ordinal);
        Assert.DoesNotContain("beta-sample-skill", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncFailsWhenACatalogueBlockNamesADirectoryNoAdapterGenerates()
    {
        // Without a declared adapter source there is no bundle-owned content to enumerate, so the
        // block would silently fall back to a shared destination. Say so instead.
        using var fixture = FixtureBundle.Create("alpha");
        string bundlePath = Path.Combine(fixture.AssetRoot, ".ai-shared", "bundles", "alpha", "bundle.json");
        JsonObject bundle = JsonNode.Parse(File.ReadAllText(bundlePath))!.AsObject();
        bundle["skillListBlocks"]![0]!["agentsDirectory"] = @".github\agents";
        File.WriteAllText(bundlePath, bundle.ToJsonString());

        var result = fixture.CreateInstaller().Sync(fixture.AssetRoot);

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains(@"agentsDirectory", StringComparison.Ordinal)
            && x.Contains(@".github\agents", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncMirrorsCanonicalServersIntoCompatibilityEntriesAndTheCodexRegion()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["sample"] = ConfigAssert.StdioServer("sample") });
        var installer = fixture.CreateInstaller();
        string codexPath = Path.Combine(fixture.AssetRoot, ".codex", "config.toml");
        const string unrelatedCodex = "model = \"gpt-5\"\r\n";
        _ = Directory.CreateDirectory(Path.GetDirectoryName(codexPath)!);
        File.WriteAllText(codexPath, unrelatedCodex);

        string mcpPath = Path.Combine(fixture.AssetRoot, ".mcp.json");
        JsonObject seeded = JsonNode.Parse(File.ReadAllText(mcpPath))!.AsObject();
        seeded["mcpServers"] = new JsonObject { ["stale"] = ConfigAssert.StdioServer("stale") };
        File.WriteAllText(mcpPath, seeded.ToJsonString());

        Assert.True(installer.Sync(fixture.AssetRoot).Success);

        Assert.True(JsonNode.DeepEquals(ConfigAssert.Servers(mcpPath), ConfigAssert.CompatServers(mcpPath)));
        Assert.Null(ConfigAssert.CompatServers(mcpPath)["stale"]);
        Assert.StartsWith(unrelatedCodex, File.ReadAllText(codexPath), StringComparison.Ordinal);
        Assert.Equal(["sample"], ConfigAssert.CodexServerNames(codexPath));

        JsonObject mcpRoot = JsonNode.Parse(File.ReadAllText(mcpPath))!.AsObject();
        mcpRoot["servers"] = new JsonObject
        {
            ["replacement"] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = "https://example.test/mcp",
                ["headers"] = new JsonObject { ["X-Test"] = "value" }
            }
        };
        File.WriteAllText(mcpPath, mcpRoot.ToJsonString());

        Assert.True(installer.Sync(fixture.AssetRoot).Success);

        Assert.True(JsonNode.DeepEquals(ConfigAssert.Servers(mcpPath), ConfigAssert.CompatServers(mcpPath)));
        Assert.Null(ConfigAssert.CompatServers(mcpPath)["sample"]);
        Assert.Equal(["replacement"], ConfigAssert.CodexServerNames(codexPath));

        string codexConfig = File.ReadAllText(codexPath);
        Assert.StartsWith(unrelatedCodex, codexConfig, StringComparison.Ordinal);
        Assert.Contains("\"http_headers\" = { \"X-Test\" = \"value\" }", codexConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\" =", codexConfig, StringComparison.Ordinal);
    }
}
