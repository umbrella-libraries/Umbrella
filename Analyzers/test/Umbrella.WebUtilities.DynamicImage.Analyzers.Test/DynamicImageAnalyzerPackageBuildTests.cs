using System.Diagnostics;

namespace Umbrella.WebUtilities.DynamicImage.Analyzers.Test;

public class DynamicImageAnalyzerPackageBuildTests
{
	[Fact]
	public async Task PackagedTargetsExposeFingerprintingBuildPropertyToAnalyzer()
	{
		string repositoryRoot = FindRepositoryRoot();
		string buildConfiguration = GetBuildConfiguration();
		string testRoot = Path.Combine(Path.GetTempPath(), $"UmbrellaDynamicImageAnalyzerPackageTest-{Guid.NewGuid():N}");
		string packagesPath = Path.Combine(testRoot, "packages");
		string consumerPath = Path.Combine(testRoot, "consumer");
		string packageVersion = $"0.0.0-package-test.{Guid.NewGuid():N}";
		_ = Directory.CreateDirectory(packagesPath);
		_ = Directory.CreateDirectory(consumerPath);

		try
		{
			string analyzerProject = Path.Combine(
				repositoryRoot,
				"Analyzers",
				"src",
				"Umbrella.WebUtilities.DynamicImage.Analyzers",
				"Umbrella.WebUtilities.DynamicImage.Analyzers.csproj");

			ProcessResult packResult = await RunDotNetAsync(
				repositoryRoot,
				"pack",
				analyzerProject,
				"--configuration",
				buildConfiguration,
				"--no-build",
				"--no-restore",
				"--output",
				packagesPath,
				$"-p:PackageVersion={packageVersion}");
			Assert.True(
				packResult.ExitCode is 0,
				$"dotnet pack failed with exit code {packResult.ExitCode}.{Environment.NewLine}{packResult.Output}");

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "PackageConsumer.csproj"),
				$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <UmbrellaDynamicImageEnableUrlFingerprinting>true</UmbrellaDynamicImageEnableUrlFingerprinting>
    <RestoreSources>{packagesPath}</RestoreSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Umbrella.WebUtilities.DynamicImage.Analyzers"
                      Version="{packageVersion}"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "ProductModel.cs"),
				"public record ProductModel { public string? ImageUrl { get; init; } }",
				TestContext.Current.CancellationToken);

			ProcessResult buildResult = await RunDotNetAsync(consumerPath, "build", "--nologo");

			Assert.NotEqual(0, buildResult.ExitCode);
			Assert.Contains("UWDI001", buildResult.Output, StringComparison.Ordinal);
		}
		finally
		{
			try
			{
				Directory.Delete(testRoot, recursive: true);
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
				Directory.Exists(Path.Combine(directory.FullName, "Analyzers")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate the Umbrella repository root.");
	}

	private static string GetBuildConfiguration()
	{
		var targetFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
		return targetFrameworkDirectory.Parent?.Name
			?? throw new InvalidOperationException("Failed to determine the build configuration.");
	}

	private static async Task<ProcessResult> RunDotNetAsync(string workingDirectory, params string[] arguments)
	{
		var startInfo = new ProcessStartInfo("dotnet")
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};

		foreach (string argument in arguments)
			startInfo.ArgumentList.Add(argument);

		using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
		string standardOutput = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
		string standardError = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
		await process.WaitForExitAsync(TestContext.Current.CancellationToken);

		return new ProcessResult(process.ExitCode, standardOutput + Environment.NewLine + standardError);
	}

	private sealed record ProcessResult(int ExitCode, string Output);
}
