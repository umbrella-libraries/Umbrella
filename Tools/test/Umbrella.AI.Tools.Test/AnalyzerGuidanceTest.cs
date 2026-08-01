using System.Text.RegularExpressions;

namespace Umbrella.AI.Tools.Test;

public partial class AnalyzerGuidanceTest
{
    private static readonly string[] _excludedSkillNames =
    [
        "umbrella-dotnet-audit-api-controller-response-contract",
        "umbrella-dotnet-audit-aspnetcore-integration-test-readiness",
        "umbrella-dotnet-audit-server-bootstrap",
        "umbrella-nuget-safe-upgrade"
    ];

    private static readonly string[] _obsoleteContracts =
    [
        "UA021",
        "UmbrellaExcludeFromModelStandards",
        "UmbrellaAllowMutableCollection"
    ];

    private static string RepoRoot => GetRepoRoot();

    [Fact]
    public void CompatibilityReferenceCoversEveryCurrentDiagnostic()
    {
        string reference = ReadCompatibilityReference();
        string[] releaseFiles =
        [
            .. Directory.EnumerateFiles(Path.Combine(RepoRoot, "Analyzers", "src"), "AnalyzerReleases.Unshipped.md", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(Path.Combine(RepoRoot, "Generators", "src"), "AnalyzerReleases.Unshipped.md", SearchOption.AllDirectories)
        ];

        string[] diagnosticIds = releaseFiles
            .SelectMany(path => DiagnosticIdRegex().Matches(File.ReadAllText(path)))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(diagnosticIds);

        foreach (string diagnosticId in diagnosticIds)
            Assert.Contains(diagnosticId, reference, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicableCanonicalSkillsReferenceAnalyzerCompatibilityGuidance()
    {
        string skillsRoot = Path.Combine(RepoRoot, ".ai-shared", "skills");

        foreach (string skillDirectory in Directory.EnumerateDirectories(skillsRoot))
        {
            string skillName = Path.GetFileName(skillDirectory);

            if (_excludedSkillNames.Contains(skillName, StringComparer.Ordinal))
                continue;

            string content = File.ReadAllText(Path.Combine(skillDirectory, "SKILL.md"));
            Assert.Contains(@".ai-shared\bundles\umbrella\analyzer-compatibility.md", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalAgentPlaybooksReferenceAnalyzerCompatibilityGuidance()
    {
        foreach (string agentDirectoryName in new[] { "claude", "github" })
        {
            string agentRoot = Path.Combine(RepoRoot, ".ai-shared", "agents", agentDirectoryName);

            foreach (string agentPath in Directory.EnumerateFiles(agentRoot, "*.md"))
            {
                if (Path.GetFileNameWithoutExtension(agentPath).StartsWith("umbrella-nuget-safe-upgrade", StringComparison.Ordinal))
                    continue;

                string content = File.ReadAllText(agentPath);
                Assert.Contains(@".ai-shared\bundles\umbrella\analyzer-compatibility.md", content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CanonicalGuidanceDoesNotContainObsoleteAnalyzerContracts()
    {
        string canonicalRoot = Path.Combine(RepoRoot, ".ai-shared");

        foreach (string path in Directory.EnumerateFiles(canonicalRoot, "*.md", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(path);

            foreach (string obsoleteContract in _obsoleteContracts)
                Assert.DoesNotContain(obsoleteContract, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalGuidanceContainsRequiredAnalyzerForms()
    {
        string reference = ReadCompatibilityReference();
        string modelSkill = ReadSkill("umbrella-dotnet-scaffold-api-server-models");
        string mapperSkill = ReadSkill("umbrella-dotnet-scaffold-mapperly-factories");
        string managePageSkill = ReadSkill("umbrella-blazor-scaffold-manage-page");

        Assert.Contains("CancellationToken cancellationToken = default", reference, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaInputModel]", modelSkill, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaAllowNonRequiredProperty(\"reason\")]", reference, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaAllowMutableProperty(\"reason\")]", reference, StringComparison.Ordinal);
        Assert.Contains("IUmbrellaMapperlyNewInstanceAsyncMapper", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("GetVersionedWebFilePathAsync", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("ImageVersionToken", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("VersionToken=\"@Model?.ImageVersionToken\"", managePageSkill, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicImageGuidanceMatchesSupportedIntegrationContract()
    {
        string skill = ReadSkill("umbrella-dotnet-configure-dynamic-image");
        string reference = File.ReadAllText(Path.Combine(
            RepoRoot,
            ".ai-shared",
            "skills",
            "umbrella-dotnet-configure-dynamic-image",
            "references",
            "dynamic-image-contract.md"));
        string mapperSkill = ReadSkill("umbrella-dotnet-scaffold-mapperly-factories");
        string component = File.ReadAllText(Path.Combine(
            RepoRoot,
            "AspNetCore",
            "src",
            "Umbrella.AspNetCore.Blazor",
            "Components",
            "FileImagePreviewUpload",
            "UmbrellaFileImagePreviewUpload.razor.cs"));
        string componentMarkup = File.ReadAllText(Path.Combine(
            RepoRoot,
            "AspNetCore",
            "src",
            "Umbrella.AspNetCore.Blazor",
            "Components",
            "FileImagePreviewUpload",
            "UmbrellaFileImagePreviewUpload.razor"));

        Assert.Contains("Umbrella.Generators.DynamicImage` only in the Server project", skill, StringComparison.Ordinal);
        Assert.Contains("UmbrellaDynamicImageEnableUrlFingerprinting", reference, StringComparison.Ordinal);
        Assert.Contains("UmbrellaDynamicImageSourceRoot", reference, StringComparison.Ordinal);
        Assert.Contains("AddAllowedVariantCatalogs", reference, StringComparison.Ordinal);
        Assert.Contains("MiddlewareHttpCacheability.Public", reference, StringComparison.Ordinal);
        Assert.Contains("MiddlewareHttpCacheability.Private", reference, StringComparison.Ordinal);
        Assert.Contains("MiddlewareHttpCacheability.NoStore", reference, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("public string? VersionToken { get; set; }", component, StringComparison.Ordinal);
        Assert.Contains("VersionToken=\"@UpdatedImageVersionToken\"", componentMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedAdaptersMatchCanonicalSources()
    {
        string canonicalSkillsRoot = Path.Combine(RepoRoot, ".ai-shared", "skills");

        foreach ((string destination, string replacement) in new[]
        {
            (Path.Combine(RepoRoot, ".claude", "skills"), @".claude\skills"),
            (Path.Combine(RepoRoot, ".github", "skills"), @".github\skills"),
            (Path.Combine(RepoRoot, ".agents", "skills"), @".agents\skills")
        })
        {
            foreach (string canonicalPath in Directory.EnumerateFiles(canonicalSkillsRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(canonicalSkillsRoot, canonicalPath);
                string expected = File.ReadAllText(canonicalPath).Replace("{{skill_dir}}", replacement, StringComparison.Ordinal);
                Assert.Equal(expected, File.ReadAllText(Path.Combine(destination, relativePath)));
            }
        }

        AssertAgentAdaptersMatch("claude", ".claude", "*.md");
        AssertAgentAdaptersMatch("github", ".github", "*.md");
    }

    private static void AssertAgentAdaptersMatch(string canonicalDirectoryName, string adapterDirectoryName, string pattern)
    {
        string canonicalRoot = Path.Combine(RepoRoot, ".ai-shared", "agents", canonicalDirectoryName);
        string adapterRoot = Path.Combine(RepoRoot, adapterDirectoryName, "agents");

        foreach (string canonicalPath in Directory.EnumerateFiles(canonicalRoot, pattern))
        {
            string targetPath = Path.Combine(adapterRoot, Path.GetFileName(canonicalPath));
            Assert.Equal(File.ReadAllText(canonicalPath), File.ReadAllText(targetPath));
        }
    }

    private static string ReadCompatibilityReference()
        => File.ReadAllText(Path.Combine(RepoRoot, ".ai-shared", "bundles", "umbrella", "analyzer-compatibility.md"));

    private static string ReadSkill(string skillName)
        => File.ReadAllText(Path.Combine(RepoRoot, ".ai-shared", "skills", skillName, "SKILL.md"));

    [GeneratedRegex(@"^(?:UA|UDA|UMA|UWDI)\d{3}(?=\s*\|)", RegexOptions.Multiline)]
    private static partial Regex DiagnosticIdRegex();

    private static string GetRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".ai-shared"))
                && Directory.Exists(Path.Combine(directory.FullName, "Analyzers")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to locate the repository root for tests.");
    }
}
