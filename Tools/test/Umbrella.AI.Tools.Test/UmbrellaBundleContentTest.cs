using System.Text.Json.Nodes;
using Umbrella.AI.Tools.Bundling;
using Umbrella.AI.Tools.Bundling.Models;
using Umbrella.AI.Tools.Bundling.Services;

namespace Umbrella.AI.Tools.Test;

/// <summary>
/// Conformance tests for the Umbrella bundle's own content and configuration. The bundling engine's
/// mechanics are covered by Umbrella.AI.Tools.Bundling.Test against synthetic fixtures; these tests
/// assert only what is specific to this repository's skills, agents, and bundle definition.
/// </summary>
public class UmbrellaBundleContentTest
{
    private static string RepoRoot => GetRepoRoot();

    /// <summary>
    /// Mirrors the options the umbrella-ai tool supplies in its entry point. If this drifts from
    /// Program.cs, the installed manifests and asset discovery would differ from what ships.
    /// </summary>
    private static BundleHostOptions Options => new()
    {
        BundleId = "umbrella",
        DisplayName = "Umbrella AI skills and agents",
        InstallerPackageId = "Umbrella.AI.Tools",
        InstallerVersion = "1.0.0-test",
        AssetRootEnvironmentVariable = "UMBRELLA_AI_ASSET_ROOT"
    };

    private static AiBundleInstaller CreateInstaller() => new(Options, RepoRoot);

    [Fact]
    public void TheRepositoryRootIsAValidAssetRootForTheUmbrellaBundle()
        => Assert.Equal(RepoRoot, new AiBundleAssetLocator(Options).ResolveAssetRoot(RepoRoot));

    [Fact]
    public void InstallDeliversTheUmbrellaSkillsAgentsAndStarterFiles()
    {
        using var workspace = new TemporaryWorkspace();

        var result = CreateInstaller().Install(new CommandOptions { TargetPath = workspace.RootPath });

        Assert.True(result.Success, string.Join("; ", result.Conflicts));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "umbrella-nuget-safe-upgrade.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "agents", "umbrella-nuget-safe-upgrade.agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "agents", "umbrella-dotnet-blazor-admin-crud-feature-agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "agents", "umbrella-dotnet-blazor-admin-crud-feature-agent.agent.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "skills", "umbrella-dotnet-scaffold-service", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "skills", "umbrella-dotnet-scaffold-service", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".claude", "skills", "umbrella-dotnet-configure-dynamic-image", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".github", "skills", "umbrella-dotnet-configure-dynamic-image", "references", "dynamic-image-contract.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".agents", "skills", "umbrella-dotnet-scaffold-service", "agents", "openai.yaml")));

        // Declared through starterFiles in bundle.json rather than hardcoded in the engine.
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "nuget-upgrade-exclusions.json")));

        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "umbrella", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "umbrella", "analyzer-compatibility.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai-shared", "bundles", "umbrella", "skill-validation.json")));
    }

    [Fact]
    public void SkillDirectoryTokensResolvePerAdapter()
    {
        using var workspace = new TemporaryWorkspace();
        Assert.True(CreateInstaller().Install(new CommandOptions { TargetPath = workspace.RootPath }).Success);

        string claudeSkill = File.ReadAllText(Path.Combine(workspace.RootPath, ".claude", "skills", "umbrella-dotnet-add-ef-migration", "SKILL.md"));
        string githubSkill = File.ReadAllText(Path.Combine(workspace.RootPath, ".github", "skills", "umbrella-dotnet-add-ef-migration", "SKILL.md"));

        Assert.Contains(@".claude\skills\umbrella-dotnet-add-ef-migration", claudeSkill, StringComparison.Ordinal);
        Assert.Contains(@".github\skills\umbrella-dotnet-add-ef-migration", githubSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", claudeSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("{{skill_dir}}", githubSkill, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogueBlocksUseTheUmbrellaHeadingAndPointAtTheRightAdapter()
    {
        using var workspace = new TemporaryWorkspace();
        Assert.True(CreateInstaller().Install(new CommandOptions { TargetPath = workspace.RootPath }).Success);

        string agents = File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md"));
        Assert.Contains("<!-- ai-bundle:umbrella:start -->", agents, StringComparison.Ordinal);
        Assert.Contains("## Umbrella Agents", agents, StringComparison.Ordinal);
        Assert.Contains("`umbrella-dotnet-core-service-agent`", agents, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", agents, StringComparison.Ordinal);

        string claudeGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, "CLAUDE.md"));
        Assert.Contains("## Umbrella Skills", claudeGuidance, StringComparison.Ordinal);
        Assert.Contains("## Umbrella Agents", claudeGuidance, StringComparison.Ordinal);
        Assert.Contains(@".claude\agents\umbrella-dotnet-core-service-agent.md", claudeGuidance, StringComparison.Ordinal);

        string copilotGuidance = File.ReadAllText(Path.Combine(workspace.RootPath, ".github", "copilot-instructions.md"));
        Assert.Contains("## Umbrella Agents", copilotGuidance, StringComparison.Ordinal);
        Assert.Contains(@".github\agents\umbrella-dotnet-core-service-agent.agent.md", copilotGuidance, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUmbrellaMcpServersReachBothMcpJsonAndTheCodexRegion()
    {
        using var workspace = new TemporaryWorkspace();
        Assert.True(CreateInstaller().Install(new CommandOptions { TargetPath = workspace.RootPath }).Success);
        string mcpPath = Path.Combine(workspace.RootPath, ".mcp.json");

        JsonObject servers = JsonNode.Parse(File.ReadAllText(mcpPath))!["servers"]!.AsObject();
        Assert.NotNull(servers["aspire"]);
        Assert.NotNull(servers["playwright"]);
        Assert.Null(servers["sql-mcp-server"]);

        JsonObject compatServers = JsonNode.Parse(File.ReadAllText(mcpPath))!["mcpServers"]!.AsObject();
        Assert.NotNull(compatServers["aspire"]);
        Assert.NotNull(compatServers["playwright"]);

        string codexConfig = File.ReadAllText(Path.Combine(workspace.RootPath, ".codex", "config.toml"));
        Assert.Contains("# ai-bundle:codex-mcp:start", codexConfig, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.\"aspire\"]", codexConfig, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.\"ado-remote-mcp\"]", codexConfig, StringComparison.Ordinal);
        Assert.Contains("\"http_headers\" =", codexConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\" =", codexConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallThenStatusIsCleanForTheShippedBundle()
    {
        using var workspace = new TemporaryWorkspace();
        var installer = CreateInstaller();
        Assert.True(installer.Install(new CommandOptions { TargetPath = workspace.RootPath }).Success);

        var status = installer.GetStatus(new CommandOptions { TargetPath = workspace.RootPath });

        Assert.True(status.Success, string.Join("; ", status.Conflicts));
    }

    [Fact]
    public void InstallLeavesAnExistingExclusionsFileAlone()
    {
        using var workspace = new TemporaryWorkspace();
        string exclusionsPath = Path.Combine(workspace.RootPath, "nuget-upgrade-exclusions.json");
        File.WriteAllText(exclusionsPath, "{\"packages\":[\"Contoso\"]}");

        Assert.True(CreateInstaller().Install(new CommandOptions { TargetPath = workspace.RootPath }).Success);

        Assert.Equal("{\"packages\":[\"Contoso\"]}", File.ReadAllText(exclusionsPath));
    }

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
            _ = Directory.CreateDirectory(RootPath);
            _ = Directory.CreateDirectory(Path.Combine(RootPath, ".git"));
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
