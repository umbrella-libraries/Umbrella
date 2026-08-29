using System.Diagnostics;

namespace Umbrella.Generators.DynamicImage.Test;

public class DynamicImagePackageRazorSdkBuildTests
{
	[Fact]
	public async Task PackagedTargetsSupplyRazorSourceWithoutDisablingRazorSourceGenerator()
	{
		string repositoryRoot = FindRepositoryRoot();
		string buildConfiguration = GetBuildConfiguration();
		string testRoot = Path.Combine(Path.GetTempPath(), $"UmbrellaDynamicImagePackageTest-{Guid.NewGuid():N}");
		string packagesPath = Path.Combine(testRoot, "packages");
		string consumerPath = Path.Combine(testRoot, "consumer");
		string clientSourcePath = Path.Combine(testRoot, "client");
		string secondaryClientSourcePath = Path.Combine(testRoot, "client-secondary");
		string packageVersion = $"0.0.0-package-test.{Guid.NewGuid():N}";
		_ = Directory.CreateDirectory(packagesPath);
		_ = Directory.CreateDirectory(consumerPath);
		_ = Directory.CreateDirectory(clientSourcePath);
		_ = Directory.CreateDirectory(secondaryClientSourcePath);
		_ = Directory.CreateDirectory(Path.Combine(clientSourcePath, "bin"));
		_ = Directory.CreateDirectory(Path.Combine(clientSourcePath, "obj"));
		_ = Directory.CreateDirectory(Path.Combine(clientSourcePath, "node_modules"));
		string nestedClientSourcePath = Path.Combine(
			clientSourcePath,
			"Pages",
			"Admin",
			"LearningProviderAdministratorManagement");
		_ = Directory.CreateDirectory(nestedClientSourcePath);

		try
		{
			string generatorProject = Path.Combine(
				repositoryRoot,
				"Generators",
				"src",
				"Umbrella.Generators.DynamicImage",
				"Umbrella.Generators.DynamicImage.csproj");

			await RunDotNetAsync(
				repositoryRoot,
				"pack",
				generatorProject,
				"--configuration",
				buildConfiguration,
				"--no-build",
				"--no-restore",
				"--output",
				packagesPath,
				$"-p:PackageVersion={packageVersion}");

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "PackageConsumer.csproj"),
				$"""
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UmbrellaDynamicImageCatalogName>Server</UmbrellaDynamicImageCatalogName>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>obj\Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Umbrella.Generators.DynamicImage"
                      Version="{packageVersion}"
                      PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <UmbrellaDynamicImageSourceRoot Include="..\client"
                                    CatalogName="Client" />
    <UmbrellaDynamicImageSourceRoot Include="..\client-secondary"
                                    CatalogName="Client" />
  </ItemGroup>
</Project>
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "DynamicImageTypes.cs"),
				"""
using Microsoft.AspNetCore.Components;

namespace Umbrella.DynamicImage.Abstractions
{
    public enum DynamicResizeMode
    {
        Crop = 4
    }

    public enum DynamicImageFormat
    {
		Jpeg = 2,
		Png = 3,
		WebP = 4,
		Avif = 5
    }

    public readonly record struct DynamicImageVariant(
        int Width,
        int Height,
        DynamicResizeMode ResizeMode,
        DynamicImageFormat Format);
}

namespace Umbrella.AspNetCore.Blazor.Components.DynamicImage
{
    public sealed class UmbrellaDynamicImage : ComponentBase
    {
        [Parameter]
        public string? Url { get; set; }

        [Parameter]
        public int WidthRequest { get; set; }

        [Parameter]
        public int HeightRequest { get; set; }

        [Parameter]
        public int MaxPixelDensity { get; set; }

		[Parameter]
		public Umbrella.DynamicImage.Abstractions.DynamicResizeMode ResizeMode { get; set; }

		[Parameter]
		public Umbrella.DynamicImage.Abstractions.DynamicImageFormat ImageFormat { get; set; }
    }
}

namespace Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
{
    public sealed class UmbrellaFileImagePreviewUpload : ComponentBase
    {
        [Parameter]
        public string? Url { get; set; }

        [Parameter]
        public string? VersionToken { get; set; }

        [Parameter]
        public int WidthRequest { get; set; }

        [Parameter]
        public int HeightRequest { get; set; }

        [Parameter]
        public int MaxPixelDensity { get; set; }
    }
}

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers
{
    public sealed class DynamicImageTagHelper
    {
    }

    public sealed class DynamicImagePictureSourceTagHelper
    {
    }
}
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "_Imports.razor"),
				"@using Umbrella.AspNetCore.Blazor.Components.DynamicImage",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "Test.razor"),
				"""
<UmbrellaDynamicImage Url="/images/test.jpg"
                      WidthRequest="321"
                      HeightRequest="123"
                      MaxPixelDensity="1" />
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(clientSourcePath, "_Imports.razor"),
				"""
@using Umbrella.AspNetCore.Blazor.Components.DynamicImage
@using Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload
@using Umbrella.DynamicImage.Abstractions
@using    static    DynamicResizeMode
@using static DynamicImageFormat
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(clientSourcePath, "ClientTest.razor"),
				"""
<UmbrellaDynamicImage Url="/images/client.jpg"
                      WidthRequest="400"
                      HeightRequest="200"
                      MaxPixelDensity="1"
                      ResizeMode="Crop"
                      ImageFormat="Png" />
<UmbrellaFileImagePreviewUpload Url="/images/client-preview.jpg"
                                VersionToken="abc123"
                                WidthRequest="450"
                                HeightRequest="225"
                                MaxPixelDensity="1" />
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(nestedClientSourcePath, "Index.razor"),
				"""
<UmbrellaDynamicImage Url="/images/nested-client.jpg"
                      WidthRequest="700"
                      HeightRequest="350"
                      MaxPixelDensity="1" />
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(clientSourcePath, "_ViewImports.cshtml"),
				"@addTagHelper *, Umbrella.AspNetCore.WebUtilities.DynamicImage",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(clientSourcePath, "ClientView.cshtml"),
				"""
<dynamic-image src="/images/client-view.jpg"
               width-request="500"
               height-request="250"
               image-density="1" />
""",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(secondaryClientSourcePath, "_Imports.razor"),
				"@using Umbrella.AspNetCore.Blazor.Components.DynamicImage",
				TestContext.Current.CancellationToken);

			await File.WriteAllTextAsync(
				Path.Combine(secondaryClientSourcePath, "ClientTest.razor"),
				"""
<UmbrellaDynamicImage Url="/images/client-secondary.jpg"
                      WidthRequest="600"
                      HeightRequest="300"
                      MaxPixelDensity="1" />
""",
				TestContext.Current.CancellationToken);

			foreach (string excludedDirectory in new[] { "bin", "obj", "node_modules" })
			{
				await File.WriteAllTextAsync(
					Path.Combine(clientSourcePath, excludedDirectory, "Ignored.razor"),
					"""
<UmbrellaDynamicImage WidthRequest="999"
                      HeightRequest="999"
                      MaxPixelDensity="1" />
""",
					TestContext.Current.CancellationToken);
			}

			await File.WriteAllTextAsync(
				Path.Combine(consumerPath, "Verification.cs"),
				"""
using Umbrella.Generated.DynamicImage;
using Umbrella.DynamicImage.Abstractions;

namespace PackageConsumer;

public static class Verification
{
    public static bool HasExpectedServerVariant =>
        ServerDynamicImageVariantCatalog.All.Any(x => x.Width is 321 && x.Height is 123);

    public static bool HasExpectedClientVariant =>
        ClientDynamicImageVariantCatalog.All.Any(x => x.Width is 400 && x.Height is 200 && x.Format is DynamicImageFormat.Png) &&
        ClientDynamicImageVariantCatalog.All.Any(x => x.Width is 450 && x.Height is 225) &&
        ClientDynamicImageVariantCatalog.All.Any(x => x.Width is 500 && x.Height is 250) &&
        ClientDynamicImageVariantCatalog.All.Any(x => x.Width is 600 && x.Height is 300);

    public static bool HasExpectedAggregateVariants =>
        DynamicImageVariantCatalog.All.Count is 6;
}
""",
				TestContext.Current.CancellationToken);

			string consumerProject = Path.Combine(consumerPath, "PackageConsumer.csproj");
			await RunDotNetAsync(
				consumerPath,
				"restore",
				consumerProject,
				"--source",
				packagesPath);
			await RunDotNetAsync(
				consumerPath,
				"build",
				consumerProject,
				"--configuration",
				"Debug",
				"--no-restore",
				"-p:TreatWarningsAsErrors=true");

			string generatedPath = Path.Combine(consumerPath, "obj", "Generated");
			string[] generatedFiles = Directory.GetFiles(generatedPath, "*.cs", SearchOption.AllDirectories);
			string preparedSourcesPath = Path.Combine(consumerPath, "obj", "Debug", "net10.0", "udi");
			string[] preparedSources = Directory.GetFiles(
				preparedSourcesPath,
				"*.umbrella-dynamic-image",
				SearchOption.AllDirectories);

			Assert.Contains(generatedFiles, x => x.EndsWith("DynamicImageVariantCatalog.g.cs", StringComparison.Ordinal));
			Assert.Contains(generatedFiles, x => x.Contains("RazorSourceGenerator", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(
				preparedSources,
				x => x.EndsWith(
					Path.Combine(
						"Pages",
						"Admin",
						"LearningProviderAdministratorManagement",
						"Index.razor.umbrella-dynamic-image"),
					StringComparison.Ordinal));
			Assert.All(preparedSources, x => Assert.True(x.Length < 260, $"Prepared source path is too long: {x}"));

			string catalogSource = await File.ReadAllTextAsync(
				generatedFiles.Single(x => x.EndsWith("DynamicImageVariantCatalog.g.cs", StringComparison.Ordinal)),
				TestContext.Current.CancellationToken);
			Assert.Contains("class ServerDynamicImageVariantCatalog", catalogSource, StringComparison.Ordinal);
			Assert.Contains("class ClientDynamicImageVariantCatalog", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(321, 123", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(400, 200", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(450, 225", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(500, 250", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(600, 300", catalogSource, StringComparison.Ordinal);
			Assert.Contains("DynamicImageVariant(700, 350", catalogSource, StringComparison.Ordinal);
			Assert.DoesNotContain("DynamicImageVariant(999, 999", catalogSource, StringComparison.Ordinal);
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
		DirectoryInfo? current = new(AppContext.BaseDirectory);

		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
				Directory.Exists(Path.Combine(current.FullName, "Generators")))
			{
				return current.FullName;
			}

			current = current.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate the Umbrella repository root.");
	}

	private static string GetBuildConfiguration()
	{
		var targetFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
		return targetFrameworkDirectory.Parent?.Name
			?? throw new InvalidOperationException("Failed to determine the build configuration.");
	}

	private static async Task RunDotNetAsync(string workingDirectory, params string[] arguments)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "dotnet",
				WorkingDirectory = workingDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			}
		};

		foreach (string argument in arguments)
			process.StartInfo.ArgumentList.Add(argument);

		_ = process.Start();
		Task<string> outputTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
		Task<string> errorTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
		await process.WaitForExitAsync(TestContext.Current.CancellationToken);
		string output = await outputTask;
		string error = await errorTask;

		Assert.True(
			process.ExitCode is 0,
			$"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
	}
}
