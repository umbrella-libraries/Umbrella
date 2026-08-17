using System.Text.Json.Nodes;
using Umbrella.AI.Tools.Bundling.Models;
using Umbrella.AI.Tools.Bundling.Services;

namespace Umbrella.AI.Tools.Bundling.Test;

/// <summary>
/// The engine must carry no bundle-specific identifier of its own: everything comes from
/// <see cref="BundleHostOptions"/> and the bundle definition.
/// </summary>
public class BundleConfigurationTest
{
    [Fact]
    public void CatalogueHeadingsComeFromTheBundleDefinition()
    {
        using var fixture = FixtureBundle.Create("alpha", catalogName: "Contoso Platform");

        Assert.True(fixture.CreateInstaller().Sync(fixture.AssetRoot).Success);

        string block = File.ReadAllText(Path.Combine(fixture.AssetRoot, ".ai-shared", "bundles", "alpha", "blocks", "CLAUDE.block.md"));
        Assert.Contains("## Contoso Platform Skills", block, StringComparison.Ordinal);
        Assert.Contains("## Contoso Platform Agents", block, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogueHeadingsFallBackToTheBundleIdWhenCatalogNameIsAbsent()
    {
        using var fixture = FixtureBundle.Create("alpha", catalogName: null);

        Assert.True(fixture.CreateInstaller().Sync(fixture.AssetRoot).Success);

        string block = File.ReadAllText(Path.Combine(fixture.AssetRoot, ".ai-shared", "bundles", "alpha", "blocks", "CLAUDE.block.md"));
        Assert.Contains("## alpha Skills", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBundleDefinitionPathIsDerivedFromTheBundleId()
    {
        var options = new BundleHostOptions
        {
            BundleId = "contoso",
            DisplayName = "Contoso",
            InstallerPackageId = "Contoso.AI.Tools",
            InstallerVersion = "1.0.0",
            AssetRootEnvironmentVariable = "CONTOSO_AI_ASSET_ROOT"
        };

        Assert.Equal(Path.Combine(".ai-shared", "bundles", "contoso", "bundle.json"), options.BundleDefinitionRelativePath);
    }

    [Fact]
    public void TheAssetLocatorAcceptsAnExplicitRoot()
    {
        using var fixture = FixtureBundle.Create("alpha");
        var locator = new AiBundleAssetLocator(fixture.Options);

        Assert.Equal(Path.GetFullPath(fixture.AssetRoot), locator.ResolveAssetRoot(fixture.AssetRoot));
    }

    [Fact]
    public void TheAssetLocatorRejectsAnExplicitRootWithoutABundleDefinition()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var empty = new TemporaryDirectory();
        var locator = new AiBundleAssetLocator(fixture.Options);

        var exception = Assert.Throws<InvalidOperationException>(() => locator.ResolveAssetRoot(empty.Path));
        Assert.Contains(Path.Combine(".ai-shared", "bundles", "alpha", "bundle.json"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAssetLocatorReadsTheHostSpecificEnvironmentVariable()
    {
        using var fixture = FixtureBundle.Create("alpha");
        var locator = new AiBundleAssetLocator(fixture.Options);
        string variableName = fixture.Options.AssetRootEnvironmentVariable;

        try
        {
            Environment.SetEnvironmentVariable(variableName, fixture.AssetRoot);
            Assert.Equal(Path.GetFullPath(fixture.AssetRoot), locator.ResolveAssetRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public void TheAssetLocatorErrorNamesTheBundleAndItsOverrides()
    {
        // The locator only matches a root that holds this bundle's definition, which is what stops one
        // installed tool adopting another bundle's assets.
        var options = new BundleHostOptions
        {
            BundleId = "does-not-exist",
            DisplayName = "Missing",
            InstallerPackageId = "Missing.AI.Tools",
            InstallerVersion = "1.0.0",
            AssetRootEnvironmentVariable = "MISSING_AI_ASSET_ROOT"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new AiBundleAssetLocator(options).ResolveAssetRoot());

        Assert.Contains("does-not-exist", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MISSING_AI_ASSET_ROOT", exception.Message, StringComparison.Ordinal);
        Assert.Contains("--asset-root", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallFailsClearlyWhenADeclaredBundleSourceDirectoryIsMissing()
    {
        using var fixture = FixtureBundle.Create("alpha");
        using var repo = new TemporaryDirectory(asRepository: true);
        Directory.Delete(Path.Combine(fixture.AssetRoot, ".ai-shared", "agents", "claude"), recursive: true);

        var exception = Assert.Throws<InvalidOperationException>(
            () => fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }));

        Assert.Contains("adapterDirectories", exception.Message, StringComparison.Ordinal);
        Assert.Contains(@".ai-shared\agents\claude", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestsRecordTheHostingToolIdentity()
    {
        using var fixture = FixtureBundle.Create("alpha", new JsonObject { ["shared"] = ConfigAssert.StdioServer("shared") });
        using var repo = new TemporaryDirectory(asRepository: true);

        Assert.True(fixture.CreateInstaller().Install(new CommandOptions { TargetPath = repo.Path }).Success);

        JsonObject manifest = JsonNode.Parse(File.ReadAllText(repo.Combine(".ai-shared", "bundles", "alpha", "manifest.json")))!.AsObject();
        Assert.Equal("alpha", manifest["bundleId"]!.GetValue<string>());
        Assert.Equal("Fixture.Test", manifest["installerPackageId"]!.GetValue<string>());
        Assert.Equal("1.0.0-test", manifest["installerVersion"]!.GetValue<string>());
        Assert.Equal(
            Path.Combine(".codex", "config.toml"),
            manifest["managedCodexMcp"]!["path"]!.GetValue<string>());
    }
}
