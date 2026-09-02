using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;
using Umbrella.AI.Tools.Bundling.Models;
using Umbrella.AI.Tools.Bundling.Services;

namespace Umbrella.AI.Tools.Bundling.Test;

/// <summary>
/// A throwaway directory that behaves like a repository root.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory(bool asRepository = false)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Umbrella.AI.Tools.Bundling.Test", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);

        if (asRepository)
        {
            _ = Directory.CreateDirectory(System.IO.Path.Combine(Path, ".git"));
        }
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A stray file lock must not fail an otherwise passing test.
            }
        }
    }
}

/// <summary>
/// Builds a synthetic bundle asset tree so the engine can be exercised without any real bundle content.
/// </summary>
internal sealed class FixtureBundle : IDisposable
{
    private readonly TemporaryDirectory _assetRoot = new();

    private FixtureBundle(string bundleId, string catalogName)
    {
        BundleId = bundleId;
        CatalogName = catalogName;
    }

    public string BundleId { get; }

    public string CatalogName { get; }

    public string AssetRoot => _assetRoot.Path;

    public BundleHostOptions Options => new()
    {
        BundleId = BundleId,
        DisplayName = $"{CatalogName} test",
        InstallerPackageId = $"{CatalogName}.Test",
        InstallerVersion = "1.0.0-test",
        AssetRootEnvironmentVariable = $"{BundleId.ToUpperInvariant()}_TEST_ASSET_ROOT"
    };

    public AiBundleInstaller CreateInstaller() => new(Options, AssetRoot);

    /// <summary>
    /// Copies this bundle's canonical skill and agent sources into <paramref name="repoRoot"/>, making
    /// a repository that bundles were installed into an authoring repository for this bundle too.
    /// That is the shape of a real repository which owns one bundle and consumes another.
    /// </summary>
    public void CopyCanonicalSourcesTo(string repoRoot)
    {
        foreach (string directory in new[] { "skills", "agents" })
        {
            string source = Path.Combine(AssetRoot, ".ai-shared", directory);
            string destination = Path.Combine(repoRoot, ".ai-shared", directory);
            _ = Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetRelativePath(source, file));
                _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }
    }

    /// <summary>
    /// Writes a complete bundle: one skill, one agent, a catalogue block, a managed doc block, MCP
    /// servers, and optionally a starter file.
    /// </summary>
    /// <param name="bundleId">Bundle id, also used to prefix skill and agent names so two fixtures never collide.</param>
    /// <param name="servers">Canonical MCP servers, or null for none.</param>
    /// <param name="catalogName">Catalogue heading prefix. Pass null to omit it and exercise the bundle id fallback.</param>
    /// <param name="starterFile">Starter file target path, or null for none.</param>
    public static FixtureBundle Create(
        string bundleId,
        JsonObject? servers = null,
        string? catalogName = "Fixture",
        string? starterFile = null)
    {
        var fixture = new FixtureBundle(bundleId, catalogName ?? bundleId);
        string root = fixture.AssetRoot;

        string skillDir = Path.Combine(root, ".ai-shared", "skills", $"{bundleId}-sample-skill");
        _ = Directory.CreateDirectory(skillDir);
        File.WriteAllText(
            Path.Combine(skillDir, "SKILL.md"),
            $"---\nname: {bundleId}-sample-skill\ndescription: Sample skill for {bundleId}.\n---\n\nRun the script in `{{{{skill_dir}}}}`.\n");

        string agentDir = Path.Combine(root, ".ai-shared", "agents", "claude");
        _ = Directory.CreateDirectory(agentDir);
        File.WriteAllText(
            Path.Combine(agentDir, $"{bundleId}-sample-agent.md"),
            $"---\nname: {bundleId}-sample-agent\ndescription: Sample agent for {bundleId}.\n---\n\nPlaybook body.\n");

        string bundleDir = Path.Combine(root, ".ai-shared", "bundles", bundleId);
        _ = Directory.CreateDirectory(Path.Combine(bundleDir, "blocks"));
        File.WriteAllText(
            Path.Combine(bundleDir, "blocks", "CLAUDE.block.md"),
            $"## {catalogName ?? bundleId} Skills\n\nPlaceholder catalogue for {bundleId}.\n");

        var bundle = new JsonObject
        {
            ["bundleId"] = bundleId,
            ["displayName"] = $"{catalogName ?? bundleId} test bundle",
            ["managedDirectories"] = new JsonArray($@".ai-shared\bundles\{bundleId}"),
            ["adapterDirectories"] = new JsonArray(
                new JsonObject
                {
                    ["source"] = @".ai-shared\skills",
                    ["targets"] = new JsonArray(
                        new JsonObject
                        {
                            ["destination"] = @".claude\skills",
                            ["substitutions"] = new JsonObject { ["{{skill_dir}}"] = @".claude\skills" }
                        },
                        new JsonObject
                        {
                            ["destination"] = @".agents\skills",
                            ["substitutions"] = new JsonObject { ["{{skill_dir}}"] = @".agents\skills" }
                        })
                },
                new JsonObject
                {
                    ["source"] = @".ai-shared\agents\claude",
                    ["targets"] = new JsonArray(new JsonObject { ["destination"] = @".claude\agents" })
                }),
            ["skillListBlocks"] = new JsonArray(
                new JsonObject
                {
                    ["targetPath"] = $@".ai-shared\bundles\{bundleId}\blocks\CLAUDE.block.md",
                    ["skillsDirectory"] = @".claude\skills",
                    ["agentsDirectory"] = @".claude\agents"
                }),
            ["managedBlocks"] = new JsonArray(
                new JsonObject
                {
                    ["targetPath"] = "CLAUDE.md",
                    ["sourcePath"] = $@".ai-shared\bundles\{bundleId}\blocks\CLAUDE.block.md"
                })
        };

        if (catalogName is not null)
        {
            bundle["catalogName"] = catalogName;
        }

        if (servers is not null)
        {
            bundle["mcpSourcePath"] = ".mcp.json";
            File.WriteAllText(
                Path.Combine(root, ".mcp.json"),
                new JsonObject { ["servers"] = servers.DeepClone() }.ToJsonString());
        }

        if (starterFile is not null)
        {
            _ = Directory.CreateDirectory(Path.Combine(bundleDir, "templates"));
            File.WriteAllText(Path.Combine(bundleDir, "templates", "starter.json"), "{\"starter\":true}");
            bundle["starterFiles"] = new JsonArray(
                new JsonObject
                {
                    ["sourcePath"] = $@".ai-shared\bundles\{bundleId}\templates\starter.json",
                    ["targetPath"] = starterFile
                });
        }

        File.WriteAllText(Path.Combine(bundleDir, "bundle.json"), bundle.ToJsonString());
        return fixture;
    }

    public void Dispose() => _assetRoot.Dispose();
}

internal static class ConfigAssert
{
    public static JsonObject Servers(string mcpJsonPath)
        => JsonNode.Parse(File.ReadAllText(mcpJsonPath))!["servers"]!.AsObject();

    public static JsonObject CompatServers(string mcpJsonPath)
        => JsonNode.Parse(File.ReadAllText(mcpJsonPath))!["mcpServers"]!.AsObject();

    /// <summary>
    /// Parses <c>.codex/config.toml</c> and returns the declared MCP server names, proving the document
    /// is valid TOML and therefore free of duplicate <c>[mcp_servers.x]</c> tables.
    /// </summary>
    public static List<string> CodexServerNames(string codexPath)
    {
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(codexPath)) ?? [];

        return root.TryGetValue("mcp_servers", out object? value) && value is TomlTable servers
            ? [.. servers.Keys]
            : [];
    }

    public static JsonObject StdioServer(string command)
        => new() { ["type"] = "stdio", ["command"] = command };

    public static JsonObject HttpServer(string url)
        => new() { ["type"] = "http", ["url"] = url };
}
