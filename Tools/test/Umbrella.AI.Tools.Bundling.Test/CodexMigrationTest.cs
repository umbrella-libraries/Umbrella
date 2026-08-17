using System.Text.Json.Nodes;

namespace Umbrella.AI.Tools.Bundling.Test;

/// <summary>
/// Earlier tool versions wrote a namespaced Codex region per bundle. Install and update absorb any
/// that remain, without treating the differing content or hash as drift.
/// </summary>
public class CodexMigrationTest
{
    // Insertion order deliberately differs from ordinal order, so the migrated region is not merely a
    // marker rename: the tables are also reordered.
    private static JsonObject LegacyOrderedServers => new()
    {
        ["zeta"] = ConfigAssert.StdioServer("zeta"),
        ["alpha-server"] = ConfigAssert.StdioServer("alpha-server")
    };

    private static void WriteLegacyRegion(string codexPath, string bundleId, string preamble, string newLine = "\n")
    {
        string content =
            preamble
            + $"# ai-bundle:{bundleId}:codex-mcp:start\n"
            + "[mcp_servers.\"zeta\"]\n\"command\" = \"zeta\"\n\n"
            + "[mcp_servers.\"alpha-server\"]\n\"command\" = \"alpha-server\"\n"
            + $"# ai-bundle:{bundleId}:codex-mcp:end\n";

        File.WriteAllText(codexPath, content.Replace("\n", newLine, StringComparison.Ordinal));
    }

    private static void SetManifestCodexHash(string repoRoot, string bundleId, string hash)
    {
        string manifestPath = Path.Combine(repoRoot, ".ai-shared", "bundles", bundleId, "manifest.json");
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["managedCodexMcp"]!["hash"] = hash;
        File.WriteAllText(manifestPath, manifest.ToJsonString());
    }

    // Both line endings matter: a Windows-authored config.toml is CRLF, and marker detection has to
    // cope with the \r before the line break.
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void UpdateAbsorbsALegacyPerBundleRegionWithoutReportingDrift(string newLine)
    {
        using var fixture = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        WriteLegacyRegion(codexPath, "alpha", "model = \"gpt-5\"\n\n", newLine);
        // The old scheme hashed the block in canonical .mcp.json order, so the recorded hash will not
        // match what the new scheme renders. Migration must not read that as tampering.
        SetManifestCodexHash(repo.Path, "alpha", "LEGACY-HASH-FROM-OLD-SCHEME");

        var result = installer.Update(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));

        string codexConfig = File.ReadAllText(codexPath);
        Assert.DoesNotContain("ai-bundle:alpha:codex-mcp", codexConfig, StringComparison.Ordinal);
        Assert.Contains("# ai-bundle:codex-mcp:start", codexConfig, StringComparison.Ordinal);
        Assert.StartsWith("model = \"gpt-5\"", codexConfig, StringComparison.Ordinal);
        Assert.Equal(["alpha-server", "zeta"], ConfigAssert.CodexServerNames(codexPath).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void StatusIsHealthyAfterMigration()
    {
        using var fixture = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        WriteLegacyRegion(repo.Combine(".codex", "config.toml"), "alpha", string.Empty);
        SetManifestCodexHash(repo.Path, "alpha", "LEGACY-HASH-FROM-OLD-SCHEME");
        Assert.True(installer.Update(new CommandOptions { TargetPath = repo.Path }).Success);

        var status = installer.GetStatus(new CommandOptions { TargetPath = repo.Path });

        Assert.True(status.Success, string.Join("; ", status.Conflicts));
    }

    [Fact]
    public void InstallAbsorbsALegacyRegionLeftBehindByAnotherBundle()
    {
        using var alpha = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        // A legacy region written by a bundle whose tool is not the one running now.
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        WriteLegacyRegion(repo.Combine(".codex", "config.toml"), "gamma", string.Empty);

        var result = alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));

        string codexConfig = File.ReadAllText(repo.Combine(".codex", "config.toml"));
        Assert.DoesNotContain("ai-bundle:gamma:codex-mcp", codexConfig, StringComparison.Ordinal);
        Assert.Contains("# ai-bundle:codex-mcp:start", codexConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationIsIdempotent()
    {
        using var fixture = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        WriteLegacyRegion(codexPath, "alpha", string.Empty);
        SetManifestCodexHash(repo.Path, "alpha", "LEGACY-HASH-FROM-OLD-SCHEME");

        Assert.True(installer.Update(new CommandOptions { TargetPath = repo.Path }).Success);
        string afterFirst = File.ReadAllText(codexPath);

        Assert.True(installer.Update(new CommandOptions { TargetPath = repo.Path }).Success);

        Assert.Equal(afterFirst, File.ReadAllText(codexPath));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void RemoveAbsorbsALegacyRegionInsteadOfReportingItMissing(string newLine)
    {
        // Removing from a repository last touched by an older tool must not fail because the shared
        // region is absent, nor leave the legacy region's server tables behind.
        using var fixture = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(options).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        WriteLegacyRegion(codexPath, "alpha", "model = \"gpt-5\"\n\n", newLine);
        SetManifestCodexHash(repo.Path, "alpha", "LEGACY-HASH-FROM-OLD-SCHEME");

        var result = installer.Remove(options);

        Assert.True(result.Success, string.Join("; ", result.Conflicts));

        string codexConfig = File.ReadAllText(codexPath);
        Assert.DoesNotContain("ai-bundle", codexConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp_servers", codexConfig, StringComparison.Ordinal);
        Assert.StartsWith("model = \"gpt-5\"", codexConfig, StringComparison.Ordinal);
        Assert.Empty(ConfigAssert.Servers(repo.Combine(".mcp.json")));
        Assert.False(File.Exists(repo.Combine(".ai-shared", "bundles", "alpha", "manifest.json")));
    }

    [Fact]
    public void RemoveFromALegacyRepositoryRetainsServersACoOwnerStillNeeds()
    {
        using var alpha = FixtureBundle.Create("alpha", LegacyOrderedServers);
        using var beta = FixtureBundle.Create("beta", new JsonObject { ["zeta"] = ConfigAssert.StdioServer("zeta") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };

        Assert.True(alpha.CreateInstaller().Install(options).Success);
        Assert.True(beta.CreateInstaller().Install(options).Success);

        // Drop back to the legacy shape before removing alpha.
        WriteLegacyRegion(repo.Combine(".codex", "config.toml"), "alpha", string.Empty);
        SetManifestCodexHash(repo.Path, "alpha", "LEGACY-HASH-FROM-OLD-SCHEME");

        var result = alpha.CreateInstaller().Remove(options);

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.Contains(result.Messages, x => x.Contains("Retained co-owned MCP server: zeta", StringComparison.Ordinal));
        Assert.Equal(["zeta"], ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")));
        Assert.Null(ConfigAssert.Servers(repo.Combine(".mcp.json"))["alpha-server"]);
    }

    [Fact]
    public void AnUnownedServerInsideTheRegionBlocksAFreshInstall()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        File.WriteAllText(repo.Combine(".codex", "config.toml"),
            "# ai-bundle:codex-mcp:start\n[mcp_servers.\"hand-written\"]\n\"command\" = \"mine\"\n# ai-bundle:codex-mcp:end\n");

        var result = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("hand-written", StringComparison.Ordinal));
    }

    [Fact]
    public void AServerDeclaredOutsideTheRegionBlocksInstallUntilForced()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        File.WriteAllText(repo.Combine(".codex", "config.toml"), "[mcp_servers.\"shared\"]\n\"command\" = \"user-authored\"\n");

        var blocked = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });
        Assert.False(blocked.Success);
        Assert.Contains(blocked.Conflicts, x => x.Contains("outside the managed region", StringComparison.Ordinal));

        var forced = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path, Force = true });
        Assert.True(forced.Success, string.Join("; ", forced.Conflicts));
        _ = Assert.Single(ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")), x => x == "shared");
    }

    [Fact]
    public void TakingOverAnInlineServerPreservesUnrelatedTomlThatFollowsIt()
    {
        // Taking ownership removes the inline server table. Everything after it, up to the next table
        // header, must survive: these sections are the user's Codex configuration, not ours.
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        _ = Directory.CreateDirectory(repo.Combine(".codex"));
        File.WriteAllText(repo.Combine(".codex", "config.toml"),
            """
            model = "gpt-5"

            [mcp_servers."shared"]
            "command" = "user-authored"

            [model_providers.custom]
            name = "My provider"
            base_url = "https://example.test/v1"

            [profiles.work]
            model = "gpt-5"
            approval_policy = "never"
            """);

        var forced = fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path, Force = true });

        Assert.True(forced.Success, string.Join("; ", forced.Conflicts));

        string codexConfig = File.ReadAllText(repo.Combine(".codex", "config.toml"));
        Assert.Contains("[model_providers.custom]", codexConfig, StringComparison.Ordinal);
        Assert.Contains("name = \"My provider\"", codexConfig, StringComparison.Ordinal);
        Assert.Contains("base_url = \"https://example.test/v1\"", codexConfig, StringComparison.Ordinal);
        Assert.Contains("[profiles.work]", codexConfig, StringComparison.Ordinal);
        Assert.Contains("approval_policy = \"never\"", codexConfig, StringComparison.Ordinal);
        Assert.StartsWith("model = \"gpt-5\"", codexConfig, StringComparison.Ordinal);

        // The user's inline definition is gone, replaced by the managed one inside the region.
        Assert.DoesNotContain("user-authored", codexConfig, StringComparison.Ordinal);
        _ = Assert.Single(ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")), x => x == "shared");
    }

    [Fact]
    public void UpdateBlocksWhenAServerIsHandAddedInsideTheRegion()
    {
        // The region is regenerated on every run, so an edit inside the markers would be silently
        // discarded. Update blocks instead, matching how a hand-edited managed doc block behaves.
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };
        var installer = fixture.CreateInstaller();
        Assert.True(installer.Install(options).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        File.WriteAllText(codexPath, File.ReadAllText(codexPath).Replace(
            "# ai-bundle:codex-mcp:end",
            "[mcp_servers.\"hand-added\"]\r\n\"command\" = \"mine\"\r\n# ai-bundle:codex-mcp:end",
            StringComparison.Ordinal));

        var result = installer.Update(options);

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x => x.Contains("hand-added", StringComparison.Ordinal));

        // Forcing takes ownership and regenerates the region.
        var forced = installer.Update(new CommandOptions { TargetPath = repo.Path, Force = true });
        Assert.True(forced.Success, string.Join("; ", forced.Conflicts));
        Assert.DoesNotContain("hand-added", ConfigAssert.CodexServerNames(codexPath));
    }
}
