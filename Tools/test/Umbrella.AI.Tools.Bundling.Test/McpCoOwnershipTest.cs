using System.Text.Json.Nodes;

namespace Umbrella.AI.Tools.Bundling.Test;

/// <summary>
/// Two bundles installed into one repository. Shared MCP servers are the case that must work without
/// a force flag, and the shared Codex region is the part that cannot express duplicates at all.
/// </summary>
public class McpCoOwnershipTest
{
    private static JsonObject AlphaServers => new()
    {
        ["shared-http"] = ConfigAssert.HttpServer("https://example.test/mcp"),
        ["alpha-only"] = ConfigAssert.StdioServer("alpha")
    };

    private static JsonObject BetaServers => new()
    {
        ["shared-http"] = ConfigAssert.HttpServer("https://example.test/mcp"),
        ["beta-only"] = ConfigAssert.StdioServer("beta")
    };

    [Fact]
    public void BothBundlesInstallIntoOneRepositoryWithoutForce()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        var alphaResult = alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });
        Assert.True(alphaResult.Success, string.Join("; ", alphaResult.Conflicts));

        var betaResult = beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });
        Assert.True(betaResult.Success, string.Join("; ", betaResult.Conflicts));

        Assert.True(File.Exists(repo.Combine(".ai-shared", "bundles", "alpha", "manifest.json")));
        Assert.True(File.Exists(repo.Combine(".ai-shared", "bundles", "beta", "manifest.json")));
        Assert.Contains(betaResult.Messages, x => x.Contains("shared-http (co-owned)", StringComparison.Ordinal));
    }

    [Fact]
    public void BothBundlesContributeSeparateDocBlocksToOneFile()
    {
        using var alpha = FixtureBundle.Create("alpha");
        using var beta = FixtureBundle.Create("beta");
        using var repo = new TemporaryDirectory(asRepository: true);
        File.WriteAllText(repo.Combine("CLAUDE.md"), "# House rules\n\nUser-owned intro.");

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        string claude = File.ReadAllText(repo.Combine("CLAUDE.md"));
        Assert.Contains("User-owned intro.", claude, StringComparison.Ordinal);
        Assert.Contains("<!-- ai-bundle:alpha:start -->", claude, StringComparison.Ordinal);
        Assert.Contains("<!-- ai-bundle:beta:start -->", claude, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSharedCodexRegionDeclaresEachServerExactlyOnce()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        // Deserializing at all proves there is no duplicate [mcp_servers.x] table.
        List<string> codexServers = ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml"));

        Assert.Equal(["alpha-only", "beta-only", "shared-http"], codexServers.Order(StringComparer.Ordinal));
        _ = Assert.Single(codexServers, x => x == "shared-http");
        Assert.DoesNotContain("ai-bundle:alpha:codex-mcp", File.ReadAllText(repo.Combine(".codex", "config.toml")), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSharedCodexRegionIsOrderedByNameRegardlessOfInstallOrder()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var alphaFirst = new TemporaryDirectory(asRepository: true);
        using var betaFirst = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = alphaFirst.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = alphaFirst.Path }).Success);

        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = betaFirst.Path }).Success);
        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = betaFirst.Path }).Success);

        Assert.Equal(
            ConfigAssert.CodexServerNames(alphaFirst.Combine(".codex", "config.toml")),
            ConfigAssert.CodexServerNames(betaFirst.Combine(".codex", "config.toml")));
    }

    [Fact]
    public void StatusStaysHealthyForBothBundlesAndReportsCoOwnership()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var alphaStatus = alpha.CreateInstaller().GetStatus(new CommandOptions { TargetPath = repo.Path });
        var betaStatus = beta.CreateInstaller().GetStatus(new CommandOptions { TargetPath = repo.Path });

        Assert.True(alphaStatus.Success, string.Join("; ", alphaStatus.Conflicts));
        Assert.True(betaStatus.Success, string.Join("; ", betaStatus.Conflicts));
        Assert.Contains(alphaStatus.Messages, x => x.Contains("co-owned with another bundle: 1", StringComparison.Ordinal));
        Assert.Contains(alphaStatus.Messages, x => x.Contains("Other bundles installed here: beta", StringComparison.Ordinal));
    }

    [Fact]
    public void UpdatingOneBundleDoesNotDriftTheOther()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var alphaUpdate = alpha.CreateInstaller().Update(new CommandOptions { TargetPath = repo.Path });
        Assert.True(alphaUpdate.Success, string.Join("; ", alphaUpdate.Conflicts));

        var betaStatus = beta.CreateInstaller().GetStatus(new CommandOptions { TargetPath = repo.Path });
        Assert.True(betaStatus.Success, string.Join("; ", betaStatus.Conflicts));
    }

    [Fact]
    public void RemovingOneBundleRetainsCoOwnedServersAndDropsExclusiveOnes()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var removal = alpha.CreateInstaller().Remove(new CommandOptions { TargetPath = repo.Path });

        Assert.True(removal.Success, string.Join("; ", removal.Conflicts));
        Assert.Contains(removal.Messages, x => x.Contains("Retained co-owned MCP server: shared-http", StringComparison.Ordinal));

        JsonObject servers = ConfigAssert.Servers(repo.Combine(".mcp.json"));
        Assert.NotNull(servers["shared-http"]);
        Assert.NotNull(servers["beta-only"]);
        Assert.Null(servers["alpha-only"]);

        List<string> codexServers = ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml"));
        Assert.Equal(["beta-only", "shared-http"], codexServers.Order(StringComparer.Ordinal));

        // The surviving bundle must still be healthy without needing an update run.
        var betaStatus = beta.CreateInstaller().GetStatus(new CommandOptions { TargetPath = repo.Path });
        Assert.True(betaStatus.Success, string.Join("; ", betaStatus.Conflicts));
    }

    [Fact]
    public void RemovingTheLastBundleClearsTheSharedRegion()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(alpha.CreateInstaller().Remove(new CommandOptions { TargetPath = repo.Path }).Success);
        Assert.True(beta.CreateInstaller().Remove(new CommandOptions { TargetPath = repo.Path }).Success);

        Assert.Empty(ConfigAssert.Servers(repo.Combine(".mcp.json")));
        Assert.DoesNotContain("ai-bundle:codex-mcp", File.ReadAllText(repo.Combine(".codex", "config.toml")), StringComparison.Ordinal);
    }

    [Fact]
    public void InstallBlocksWhenTwoBundlesDescribeTheSameServerDifferently()
    {
        using var alpha = FixtureBundle.Create("alpha", new JsonObject { ["contested"] = ConfigAssert.StdioServer("alpha-command") });
        using var beta = FixtureBundle.Create("beta", new JsonObject { ["contested"] = ConfigAssert.StdioServer("beta-command") });
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var result = beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, x =>
            x.Contains("contested", StringComparison.Ordinal)
            && x.Contains("owned by bundle 'alpha'", StringComparison.Ordinal));
    }

    [Fact]
    public void ForceTakesOwnershipWhenTwoBundlesDisagree()
    {
        using var alpha = FixtureBundle.Create("alpha", new JsonObject { ["contested"] = ConfigAssert.StdioServer("alpha-command") });
        using var beta = FixtureBundle.Create("beta", new JsonObject { ["contested"] = ConfigAssert.StdioServer("beta-command") });
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        var result = beta.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path, Force = true });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.Equal("beta-command", ConfigAssert.Servers(repo.Combine(".mcp.json"))["contested"]!["command"]!.GetValue<string>());
        _ = Assert.Single(ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")), x => x == "contested");
    }

    [Fact]
    public void ASharedServerOwnedOnlyByAForeignManifestIsStillRenderedIntoTheRegion()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var repo = new TemporaryDirectory(asRepository: true);

        // A bundle installed by a tool that is not present here still owns its server entry.
        File.WriteAllText(repo.Combine(".mcp.json"), new JsonObject
        {
            ["servers"] = new JsonObject { ["foreign-only"] = ConfigAssert.StdioServer("foreign") }
        }.ToJsonString());
        BundleLifecycleTest.WriteForeignManifest(
            repo.Path,
            "gamma",
            managedMcpServers: [("foreign-only", "IGNORED")]);

        var result = alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.Contains("foreign-only", ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")));
    }

    [Fact]
    public void SyncDoesNotClaimServersOwnedByAnotherInstalledBundle()
    {
        // An authoring repo can also have bundles installed into it, in which case the canonical
        // .mcp.json it syncs from is the same file installs merge into. Sync regenerates derived output;
        // it must not rewrite this bundle's manifest to claim a server another bundle owns.
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };

        Assert.True(alpha.CreateInstaller().Install(options).Success);
        Assert.True(beta.CreateInstaller().Install(options).Success);

        // Installing already placed .ai-shared\bundles\alpha there; adding alpha's canonical sources
        // makes the same directory an authoring repo for alpha as well as an install target.
        alpha.CopyCanonicalSourcesTo(repo.Path);

        var syncResult = alpha.CreateInstaller().Sync(repo.Path);
        Assert.True(syncResult.Success, string.Join("; ", syncResult.Conflicts));

        string[] alphaOwned = ManifestServerNames(repo.Path, "alpha");
        Assert.DoesNotContain("beta-only", alphaOwned);
        Assert.Equal(["alpha-only", "shared-http"], alphaOwned.Order(StringComparer.Ordinal));

        // beta keeps its own entry and stays healthy.
        Assert.Contains("beta-only", ManifestServerNames(repo.Path, "beta"));
        var betaStatus = beta.CreateInstaller().GetStatus(options);
        Assert.True(betaStatus.Success, string.Join("; ", betaStatus.Conflicts));
    }

    [Fact]
    public void UpdateKeepsAServerInTheRegionWhenThisBundleDropsItButAnotherStillOwnsIt()
    {
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var beta = FixtureBundle.Create("beta", BetaServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };

        Assert.True(alpha.CreateInstaller().Install(options).Success);
        Assert.True(beta.CreateInstaller().Install(options).Success);

        // alpha stops shipping the shared server; beta still owns it.
        File.WriteAllText(Path.Combine(alpha.AssetRoot, ".mcp.json"), new JsonObject
        {
            ["servers"] = new JsonObject { ["alpha-only"] = ConfigAssert.StdioServer("alpha") }
        }.ToJsonString());

        var update = alpha.CreateInstaller().Update(options);

        Assert.True(update.Success, string.Join("; ", update.Conflicts));
        Assert.Contains(update.Messages, x => x.Contains("Retained co-owned MCP server no longer in this bundle: shared-http", StringComparison.Ordinal));

        // The entry stays in .mcp.json for beta, so the shared region must keep declaring it.
        Assert.NotNull(ConfigAssert.Servers(repo.Combine(".mcp.json"))["shared-http"]);
        Assert.Contains("shared-http", ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")));

        var betaStatus = beta.CreateInstaller().GetStatus(options);
        Assert.True(betaStatus.Success, string.Join("; ", betaStatus.Conflicts));
    }

    [Fact]
    public void InstallSucceedsWhenAForeignManifestClaimsAServerMissingFromMcpJson()
    {
        // A foreign manifest can name a server the user has since deleted from .mcp.json. There is no
        // definition to disagree with, so this is not a co-ownership conflict: the entry is recreated.
        using var alpha = FixtureBundle.Create("alpha", new JsonObject { ["ghost"] = ConfigAssert.StdioServer("ghost") });
        using var repo = new TemporaryDirectory(asRepository: true);
        File.WriteAllText(repo.Combine(".mcp.json"), new JsonObject { ["servers"] = new JsonObject() }.ToJsonString());
        BundleLifecycleTest.WriteForeignManifest(repo.Path, "gamma", managedMcpServers: [("ghost", "STALE-HASH")]);

        var result = alpha.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.NotNull(ConfigAssert.Servers(repo.Combine(".mcp.json"))["ghost"]);
        Assert.Contains("ghost", ConfigAssert.CodexServerNames(repo.Combine(".codex", "config.toml")));
    }

    [Fact]
    public void StatusDetectsAnEditMadeInsideTheSharedRegion()
    {
        // The manifest hash covers this bundle's contribution rendered in isolation, so it cannot see an
        // edit to the region itself. Status compares the region against a fresh render of the union.
        using var alpha = FixtureBundle.Create("alpha", AlphaServers);
        using var repo = new TemporaryDirectory(asRepository: true);
        var options = new CommandOptions { TargetPath = repo.Path };
        var installer = alpha.CreateInstaller();
        Assert.True(installer.Install(options).Success);
        Assert.True(installer.GetStatus(options).Success);

        string codexPath = repo.Combine(".codex", "config.toml");
        File.WriteAllText(codexPath, File.ReadAllText(codexPath)
            .Replace("https://example.test/mcp", "https://evil.test/mcp", StringComparison.Ordinal));

        var status = installer.GetStatus(options);

        Assert.False(status.Success);
        Assert.Contains(status.Conflicts, x => x.Contains("Codex MCP region content drifted", StringComparison.Ordinal));
    }

    private static string[] ManifestServerNames(string repoRoot, string bundleId)
    {
        JsonObject manifest = JsonNode
            .Parse(File.ReadAllText(Path.Combine(repoRoot, ".ai-shared", "bundles", bundleId, "manifest.json")))!
            .AsObject();

        return [.. manifest["managedMcpServers"]!.AsArray().Select(x => x!["name"]!.GetValue<string>())];
    }
}
