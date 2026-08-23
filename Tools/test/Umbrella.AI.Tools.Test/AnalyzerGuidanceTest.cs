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
        "UmbrellaExcludeFromModelStandards",
        "UmbrellaAllowMutableCollection",
        "[UmbrellaInputModel] CreateUpdate<Name>ModelBase"
    ];

    private static string RepoRoot => GetRepoRoot();

    [Fact]
    public void TestProjectStandardizerPreservesLayoutWhenAddingProperties()
    {
        string? powerShellPath = FindPowerShellPath();

        if (powerShellPath is null)
            return;

        string fixtureRoot = Path.Combine(Path.GetTempPath(), $"umbrella-standardizer-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(fixtureRoot);

        try
        {
            File.WriteAllText(Path.Combine(fixtureRoot, "global.json"), "{}\r\n");
            File.WriteAllText(
                Path.Combine(fixtureRoot, "Directory.Build.props"),
                "<Project>\r\n  <PropertyGroup>\r\n    <TargetFramework>net10.0</TargetFramework>\r\n  </PropertyGroup>\r\n</Project>\r\n");
            File.WriteAllText(Path.Combine(fixtureRoot, "Directory.Build.targets"), "<Project>\r\n</Project>\r\n");
            File.WriteAllText(Path.Combine(fixtureRoot, "Directory.Packages.props"), "<Project>\r\n</Project>\r\n");

            string projectPath = Path.Combine(fixtureRoot, "Example.Test.csproj");
            File.WriteAllText(
                projectPath,
                "<Project>\r\n  <PropertyGroup>\r\n    <TargetFramework>net10.0</TargetFramework>\r\n  </PropertyGroup>\r\n</Project>\r\n");

            string projectWithoutPropertyGroupPath = Path.Combine(fixtureRoot, "OnlyItems.Test.csproj");
            File.WriteAllText(
                projectWithoutPropertyGroupPath,
                "<Project>\r\n  <ItemGroup>\r\n    <ProjectReference Include=\"..\\App.csproj\" />\r\n  </ItemGroup>\r\n</Project>\r\n");

            string scriptPath = Path.Combine(
                RepoRoot,
                ".ai-shared",
                "skills",
                "umbrella-dotnet-standardize-test-projects",
                "scripts",
                "Invoke-StandardizeTestProjects.ps1");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-Mode");
            startInfo.ArgumentList.Add("Apply");
            startInfo.ArgumentList.Add("-RepoRoot");
            startInfo.ArgumentList.Add(fixtureRoot);

            using var process = System.Diagnostics.Process.Start(startInfo)!;
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"Standardizer failed. Output: {standardOutput}{Environment.NewLine}Error: {standardError}");

            string project = File.ReadAllText(projectPath);
            Assert.Matches("\\r?\\n    <IsTestProject>true</IsTestProject>\\r?\\n  </PropertyGroup>", project);
            Assert.DoesNotContain("</IsTestProject></PropertyGroup>", project, StringComparison.Ordinal);

            string projectWithoutPropertyGroup = File.ReadAllText(projectWithoutPropertyGroupPath);
            Assert.Matches(
                "<Project>\\r?\\n  <PropertyGroup>\\r?\\n    <IsTestProject>true</IsTestProject>\\r?\\n  </PropertyGroup>\\r?\\n  <ItemGroup>",
                projectWithoutPropertyGroup);
            Assert.DoesNotContain("</PropertyGroup><ItemGroup>", projectWithoutPropertyGroup, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    [Fact]
    public void EfMigrationWrapperParsesJsonAfterBracketedBuildWarning()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string? powerShellPath = FindPowerShellPath();

        if (powerShellPath is null)
            return;

        string fixtureRoot = Path.Combine(Path.GetTempPath(), $"umbrella-ef-parser-{Guid.NewGuid():N}");
        string fakeBin = Path.Combine(fixtureRoot, "bin");
        string migrationsDirectory = Path.Combine(fixtureRoot, "App.Migrations");
        string startupDirectory = Path.Combine(fixtureRoot, "App.Web");
        _ = Directory.CreateDirectory(fakeBin);
        _ = Directory.CreateDirectory(migrationsDirectory);
        _ = Directory.CreateDirectory(startupDirectory);

        try
        {
            File.WriteAllText(Path.Combine(migrationsDirectory, "App.Migrations.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />\r\n");
            File.WriteAllText(Path.Combine(startupDirectory, "App.Web.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />\r\n");
            File.WriteAllText(
                Path.Combine(fakeBin, "dotnet.cmd"),
                "@echo off\r\necho warning NU1903: package warning [D:\\Path\\App.csproj]\r\necho [\r\necho   {\"name\":\"AppDbContext\"}\r\necho ]\r\nexit /b 0\r\n");

            string scriptPath = Path.Combine(
                RepoRoot,
                ".ai-shared",
                "skills",
                "umbrella-dotnet-add-ef-migration",
                "scripts",
                "Invoke-AddEfMigration.ps1");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powerShellPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.Environment["PATH"] = $"{fakeBin}{Path.PathSeparator}{startInfo.Environment["PATH"]}";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-MigrationName");
            startInfo.ArgumentList.Add("1.0.0");
            startInfo.ArgumentList.Add("-RepoRoot");
            startInfo.ArgumentList.Add(fixtureRoot);
            startInfo.ArgumentList.Add("-MigrationsProject");
            startInfo.ArgumentList.Add("App.Migrations\\App.Migrations.csproj");
            startInfo.ArgumentList.Add("-StartupProject");
            startInfo.ArgumentList.Add("App.Web\\App.Web.csproj");

            using var process = System.Diagnostics.Process.Start(startInfo)!;
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, $"EF migration wrapper failed. Output: {standardOutput}{Environment.NewLine}Error: {standardError}");
            Assert.Contains("Context            : AppDbContext", standardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Could not parse JSON output", standardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

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
        string fileHandlerSkill = ReadSkill("umbrella-dotnet-scaffold-file-handler");
        string managePageSkill = ReadSkill("umbrella-blazor-scaffold-manage-page");
        string renameClientServiceSkill = ReadSkill("umbrella-dotnet-rename-client-repository-to-service");
        string resourceAuthorizationSkill = ReadSkill("umbrella-dotnet-scaffold-resource-auth-handler");
        string migrateRepositoryControllerSkill = ReadSkill("umbrella-dotnet-migrate-repo-controller-to-data-service");
        string repositoryControllerSkill = ReadSkill("umbrella-dotnet-scaffold-api-repo-controller");
        string dataServiceControllerSkill = ReadSkill("umbrella-dotnet-scaffold-api-data-service-controller");
        string customControllerSkill = ReadSkill("umbrella-dotnet-scaffold-custom-api-controller");
        string clientDataSkill = ReadSkill("umbrella-dotnet-scaffold-client-data");
        string indexPageSkill = ReadSkill("umbrella-blazor-scaffold-index-page");
        string navItemSkill = ReadSkill("umbrella-blazor-register-nav-item");
        string autoMapperMigrationSkill = ReadSkill("umbrella-dotnet-migrate-automapper-to-mapperly");
        string testProjectSkill = ReadSkill("umbrella-dotnet-scaffold-test-project");
        string repositoryControllerTestsSkill = ReadSkill("umbrella-dotnet-generate-api-repo-controller-tests");
        string dataServiceControllerTestsSkill = ReadSkill("umbrella-dotnet-generate-api-data-service-controller-tests");
        string standardizeTestProjectsScript = File.ReadAllText(Path.Combine(
            RepoRoot,
            ".ai-shared",
            "skills",
            "umbrella-dotnet-standardize-test-projects",
            "scripts",
            "Invoke-StandardizeTestProjects.ps1"));
        string addEfMigrationScript = File.ReadAllText(Path.Combine(
            RepoRoot,
            ".ai-shared",
            "skills",
            "umbrella-dotnet-add-ef-migration",
            "scripts",
            "Invoke-AddEfMigration.ps1"));
        string nuGetUpgradeScript = File.ReadAllText(Path.Combine(
            RepoRoot,
            ".ai-shared",
            "skills",
            "umbrella-nuget-safe-upgrade",
            "scripts",
            "Invoke-NuGetSafeUpgrade.ps1"));

        Assert.Contains("CancellationToken cancellationToken = default", reference, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaInputModel]", modelSkill, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaAllowUnsealedModel", modelSkill, StringComparison.Ordinal);
        Assert.Contains("using Umbrella.Analyzers;", modelSkill, StringComparison.Ordinal);
        Assert.Contains("public sealed record", modelSkill, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyConcurrencyStamp", modelSkill, StringComparison.Ordinal);
        Assert.Contains("I<Name>InputModel", modelSkill, StringComparison.Ordinal);

        string compatibilityReference = File.ReadAllText(
            Path.Combine(RepoRoot, ".ai-shared", "bundles", "umbrella", "analyzer-compatibility.md"));
        Assert.Contains("UA021", compatibilityReference, StringComparison.Ordinal);
        Assert.Contains("UA022", compatibilityReference, StringComparison.Ordinal);
        Assert.Contains("UA023", compatibilityReference, StringComparison.Ordinal);
        Assert.DoesNotContain("using Umbrella.Utilities.Annotations;", modelSkill, StringComparison.Ordinal);
        Assert.Contains("public sealed record <Name>PaginatedResultModel : PaginatedResultModel<Slim<Name>Model>;", modelSkill, StringComparison.Ordinal);
        Assert.Contains("Prefer `PaginatedResultModel<Slim<Name>Model>` directly", modelSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("`PaginatedResultModel<T>` is a class", modelSkill, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaAllowNonRequiredProperty(\"reason\")]", reference, StringComparison.Ordinal);
        Assert.Contains("[UmbrellaAllowMutableProperty(\"reason\")]", reference, StringComparison.Ordinal);
        Assert.Contains("IUmbrellaMapperlyNewInstanceAsyncMapper", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exc) when (_logger.WriteError(exc, new { source }))", mapperSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception exc) when (_logger.WriteError(exc))", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("entities.Select(MapSlimInternal)", mapperSkill, StringComparison.Ordinal);
        Assert.DoesNotContain("MapAllInternal", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("[MapperIgnoreTarget(nameof(<Name>Entity.ConcurrencyStamp))]", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("Do not accept RMG012 warnings as harmless", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("use `umbrella-dotnet-scaffold-file-handler` first", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("including create and update result mappings", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("#pragma warning disable CS0618 // UmbrellaFileHandler currently requires the legacy IHybridCache abstraction.", fileHandlerSkill, StringComparison.Ordinal);
        Assert.Contains("Do not suppress `CS0618` project-wide", fileHandlerSkill, StringComparison.Ordinal);
        Assert.Contains("GetVersionedWebFilePathAsync", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("ImageVersionToken", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("VersionToken=\"@Model?.ImageVersionToken\"", managePageSkill, StringComparison.Ordinal);
        Assert.Contains("Search the entire solution", renameClientServiceSkill, StringComparison.Ordinal);
        Assert.Contains("do not create an ad-hoc controller", resourceAuthorizationSkill, StringComparison.Ordinal);
        Assert.Contains("cannot use Pattern 1's `object`/`NoOp*` placeholders", migrateRepositoryControllerSkill, StringComparison.Ordinal);
        Assert.Contains("endpoint-enablement and authorization-check overrides", migrateRepositoryControllerSkill, StringComparison.Ordinal);
        Assert.Contains("base controller enables resource authorization checks by default", repositoryControllerSkill, StringComparison.Ordinal);
        Assert.Contains("File replacement/deletion hooks are destructive behavior", repositoryControllerSkill, StringComparison.Ordinal);
        Assert.Contains("base enables resource authorization checks by default", dataServiceControllerSkill, StringComparison.Ordinal);
        Assert.Contains("result.Result is not null", managePageSkill, StringComparison.Ordinal);
        Assert.Contains("Use their exact discovered names", managePageSkill, StringComparison.Ordinal);
        Assert.Contains("A file-provider filename is not a raw text field", managePageSkill, StringComparison.Ordinal);
        Assert.Contains("A plain `Task<T>` method is not compatible", customControllerSkill, StringComparison.Ordinal);
        Assert.Contains("if not, put them on the concrete controller class", customControllerSkill, StringComparison.Ordinal);
        Assert.Contains("using Umbrella.Utilities.DataAnnotations.Abstractions;", clientDataSkill, StringComparison.Ordinal);
        Assert.Contains("using Umbrella.Utilities.Http.Abstractions;", clientDataSkill, StringComparison.Ordinal);
        Assert.Contains("Preserve unrelated registration order and formatting", clientDataSkill, StringComparison.Ordinal);
        Assert.Contains("local `_Imports.razor`", indexPageSkill, StringComparison.Ordinal);
        Assert.Contains("service exposes delete", indexPageSkill, StringComparison.Ordinal);
        Assert.Contains("Do not leave permanent navigation", indexPageSkill, StringComparison.Ordinal);
        Assert.Contains("same contiguous group and authorization block", navItemSkill, StringComparison.Ordinal);
        Assert.Contains("A direct member-access rename", autoMapperMigrationSkill, StringComparison.Ordinal);
        Assert.Contains("required nested target", autoMapperMigrationSkill, StringComparison.Ordinal);
        Assert.Contains("test or secondary consumer projects", autoMapperMigrationSkill, StringComparison.Ordinal);
        Assert.Contains("fully manual mapper-interface implementations", autoMapperMigrationSkill, StringComparison.Ordinal);
        Assert.Contains("internal sealed partial class", mapperSkill, StringComparison.Ordinal);
        Assert.Contains("internal sealed partial class", autoMapperMigrationSkill, StringComparison.Ordinal);
        Assert.Contains("every unresolved inherited global using", testProjectSkill, StringComparison.Ordinal);
        Assert.Contains("generated `obj\\<Configuration>\\<TargetFramework>\\*.GlobalUsings.g.cs`", testProjectSkill, StringComparison.Ordinal);
        Assert.Contains("Dynamic Image URL/version-token pair", repositoryControllerTestsSkill, StringComparison.Ordinal);
        Assert.Contains("Dynamic Image URL/version-token pair", dataServiceControllerTestsSkill, StringComparison.Ordinal);
        Assert.Contains("$xml.PreserveWhitespace = $true", standardizeTestProjectsScript, StringComparison.Ordinal);
        Assert.Contains("Remove-XmlNodePreservingLayout -Node $itemGroup", standardizeTestProjectsScript, StringComparison.Ordinal);
        Assert.Contains("function Remove-XmlNodePreservingLayout", standardizeTestProjectsScript, StringComparison.Ordinal);
        Assert.Contains("TrimEnd([char[]]", standardizeTestProjectsScript, StringComparison.Ordinal);
        Assert.Contains("Get-RepoScopedTempReportPath", standardizeTestProjectsScript, StringComparison.Ordinal);
        Assert.Contains("$candidates = @(Get-ChildItem", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("Current dotnet-ef emits the JSON array directly", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("dotnet format $MigrationsProject whitespace --no-restore --include $migrationFileRelativePath", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("--diagnostics IDE0005 IDE0058 IDE0161", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("function Get-EfRelativePath", addEfMigrationScript, StringComparison.Ordinal);
        Assert.DoesNotContain("[System.IO.Path]::GetRelativePath", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("$dbContextListExitCode = $LASTEXITCODE", addEfMigrationScript, StringComparison.Ordinal);
        Assert.Contains("$migrationFileRelativePath", addEfMigrationScript, StringComparison.Ordinal);
        Assert.DoesNotContain('\u2014', addEfMigrationScript);
        Assert.Contains("packageIds = @($PackageId", nuGetUpgradeScript, StringComparison.Ordinal);
        Assert.Contains("projects = @($Project", nuGetUpgradeScript, StringComparison.Ordinal);
        Assert.Contains("[bool]$effectiveAllowPrerelease", nuGetUpgradeScript, StringComparison.Ordinal);
        Assert.Contains("Get-RepoScopedTempReportPath", nuGetUpgradeScript, StringComparison.Ordinal);
        Assert.Contains("allowPrerelease = $effectiveAllowPrerelease", nuGetUpgradeScript, StringComparison.Ordinal);
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
        string dynamicImageComponent = File.ReadAllText(Path.Combine(
            RepoRoot,
            "AspNetCore",
            "src",
            "Umbrella.AspNetCore.Blazor",
            "Components",
            "DynamicImage",
            "UmbrellaDynamicImage.razor.cs"));
        string dynamicImageTagHelperBase = File.ReadAllText(Path.Combine(
            RepoRoot,
            "AspNetCore",
            "src",
            "Umbrella.AspNetCore.WebUtilities.DynamicImage",
            "Mvc",
            "TagHelpers",
            "DynamicImageTagHelperBase.cs"));
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
        Assert.Contains("public double? FocalPointX { get; set; }", dynamicImageComponent, StringComparison.Ordinal);
        Assert.Contains("public double? FocalPointY { get; set; }", dynamicImageComponent, StringComparison.Ordinal);
        Assert.Contains("public double? FocalPointX { get; set; }", dynamicImageTagHelperBase, StringComparison.Ordinal);
        Assert.Contains("public double? FocalPointY { get; set; }", dynamicImageTagHelperBase, StringComparison.Ordinal);
        Assert.Contains("Focal coordinates are runtime inputs", skill, StringComparison.Ordinal);
        Assert.Contains("focal-point-x", reference, StringComparison.Ordinal);
        Assert.Contains("do not report UWDI004", reference, StringComparison.Ordinal);
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

    private static string? FindPowerShellPath()
    {
        string executableName = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
        string? path = Environment.GetEnvironmentVariable("PATH");

        foreach (string directory in (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim('"'), executableName);

            if (File.Exists(candidate))
                return candidate;
        }

        if (OperatingSystem.IsWindows())
        {
            string candidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotnet",
                "tools",
                executableName);

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

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
