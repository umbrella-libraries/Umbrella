using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace Umbrella.CodingStandards.Tools.Test;

public class CodingStandardsToolTest
{
    private static string RepoRoot => GetRepoRoot();
    private static string ToolRelativePath => Path.Combine("Tools", "src", "Umbrella.CodingStandards.Tools", "bin", GetBuildConfiguration(), "net10.0", "umbrella-coding-standards.dll");

    [Fact]
    public void ToolCopiesExpectedFiles()
    {
        using var workspace = new TemporaryWorkspace();

        ProcessResult result = RunTool(workspace.RootPath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".editorconfig")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".filenesting.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "Umbrella.CodingStandards.props")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "Umbrella.CodingStandards.cmd")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "Directory.Build.props")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "commands.json")));
        Assert.Contains("Umbrella.CodingStandards.props", File.ReadAllText(Path.Combine(workspace.RootPath, "Directory.Build.props")), StringComparison.Ordinal);

        string commandText = File.ReadAllText(Path.Combine(workspace.RootPath, "Umbrella.CodingStandards.cmd"));
        Assert.Contains("umbrella.codingstandards.tools", commandText, StringComparison.Ordinal);
        Assert.Contains("umbrella-coding-standards", commandText, StringComparison.Ordinal);

        using var commands = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.RootPath, "commands.json")));
        var root = commands.RootElement;
        Assert.Equal("/c Umbrella.CodingStandards.cmd", root.GetProperty("commands").GetProperty("UmbrellaCodingStandards").GetProperty("arguments").GetString());
        Assert.Equal("UmbrellaCodingStandards", root.GetProperty("-vs-binding").GetProperty("ProjectOpened")[0].GetString());
    }

    [Fact]
    public void ToolAddsImportOnlyOnceToExistingDirectoryBuildProps()
    {
        using var workspace = new TemporaryWorkspace();
        string directoryBuildPropsPath = Path.Combine(workspace.RootPath, "Directory.Build.props");
        File.WriteAllText(directoryBuildPropsPath, "<Project><PropertyGroup><Test>true</Test></PropertyGroup></Project>");

        ProcessResult firstRun = RunTool(workspace.RootPath);
        ProcessResult secondRun = RunTool(workspace.RootPath);

        Assert.Equal(0, firstRun.ExitCode);
        Assert.Equal(0, secondRun.ExitCode);

        var document = XDocument.Parse(File.ReadAllText(directoryBuildPropsPath));
        int importCount = document.Root!.Elements("Import")
            .Count(x => x.Attribute("Project")?.Value == "Umbrella.CodingStandards.props");

        Assert.Equal(1, importCount);
    }

    [Fact]
    public void ToolReturnsNonZeroForMissingRootDirectory()
    {
        string missingRoot = Path.Combine(Path.GetTempPath(), "Umbrella.CodingStandards.Tools.Test", Guid.NewGuid().ToString("N"), "missing");

        ProcessResult result = RunTool(missingRoot);

        Assert.NotEqual(0, result.ExitCode);
    }

    private static ProcessResult RunTool(string rootDir)
    {
        string toolPath = Path.Combine(RepoRoot, ToolRelativePath);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add("--root-dir");
        startInfo.ArgumentList.Add(rootDir);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GetBuildConfiguration()
    {
        var targetFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        return targetFrameworkDirectory.Parent?.Name
            ?? throw new InvalidOperationException("Failed to determine the build configuration.");
    }

    private static string GetRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Umbrella Code Libraries.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Failed to locate the repository root for tests.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Umbrella.CodingStandards.Tools.Test", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(RootPath);
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
