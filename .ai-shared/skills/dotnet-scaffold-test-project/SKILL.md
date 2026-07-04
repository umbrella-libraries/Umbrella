---
name: dotnet-scaffold-test-project
description: 'Create a repo-standard .NET runnable test project shell using the shared Umbrella IsTestProject=true pattern, singular .Test naming, solution folder registration, minimal project-specific package references, and restore/build/test validation. Use when adding a new architecture, integration, analyzer, Mapperly, or other test project before adding specialized test infrastructure.'
---

# Scaffold Test Project

## Purpose

Create one runnable test project that follows the shared Umbrella test-project convention. This skill creates the project shell only; use a more specific skill afterward for architecture tests, ASP.NET integration factories, or domain-specific test classes.

## Preflight

Before writing files:

1. Locate the solution file (`.sln` or `.slnx`) and repo root.
2. Read `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props`.
3. Confirm the repo uses the shared test setup:
   - runnable test projects opt in with `<IsTestProject>true</IsTestProject>`;
   - `Directory.Build.props` provides the xUnit global using for test projects;
   - `Directory.Build.targets` provides output type, MTP runner, packing, warning, and compilation-context settings;
   - shared test packages are injected centrally or managed through true CPM.
4. If central test configuration is missing or drifted, use `dotnet-standardize-test-projects` first. Do not compensate by duplicating shared runner properties or shared runner packages in the new project.
5. Inspect existing runnable test projects to mirror:
   - `Test\` vs `test\` folder casing;
   - whether `TargetFramework` is inherited or local;
   - local `NoWarn` entries;
   - package-version style;
   - global usings that need removal, commonly `Humanizer`.
6. Check the worktree. Do not overwrite unrelated user changes.

## Project naming

- Use singular `.Test`, never `.Tests`.
- Prefer `Test\<ProjectName>.Test\<ProjectName>.Test.csproj`.
- For app-specific web tests, use the app namespace and layer in the name, for example `ThriveForSend.Web.Server.Test`.
- For general tests, use a clear feature/slice name such as `<App>.Architecture.Test` or `<App>.Mapperly.Test`.

## Project file pattern

Create the minimal project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<IsTestProject>true</IsTestProject>
		<NoWarn>$(NoWarn);CA1515;CA1707</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Project.Specific.Package" Version="1.2.3" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\Path\To\Project.csproj" />
	</ItemGroup>

</Project>
```

Rules:

- Add `TargetFramework` only when existing runnable test projects declare it locally.
- Add only project-specific package references. Do not add shared xUnit/MTP runner, coverage, TRX, or Moq packages when central config already injects them.
- If true CPM is enabled, omit `Version` attributes and add/update `<PackageVersion>` centrally.
- Append warning suppressions with `$(NoWarn);...`.
- Keep `CA1515` for public xUnit v3 test classes and `CA1707` for underscore-style test names when the repo uses them.
- Remove inherited global usings only when the new test project cannot resolve them:

```xml
<ItemGroup>
	<Using Remove="Humanizer" />
</ItemGroup>
```

## Optional smoke test

Add a smoke test when the Microsoft Testing Platform runner would otherwise return a zero-tests failure, or when the project shell should be immediately runnable.

Use a low-risk test that validates wiring only:

```csharp
namespace <ProjectName>.Test;

public sealed class TestProjectSmokeTests
{
	[Fact]
	public void Test_project_is_discoverable()
	{
		Assert.True(true);
	}
}
```

Do not add a meaningless smoke test if the next skill will immediately add real tests before validation.

## Solution registration

Add the project to the solution using the existing test solution folder:

```powershell
dotnet sln "<SolutionFile>" add "Test\<ProjectName>.Test\<ProjectName>.Test.csproj" --solution-folder Test
```

Adjust `--solution-folder` to match the solution's existing structure.

## Validation

Run:

```powershell
dotnet restore "<ProjectFile>"
dotnet build "<ProjectFile>" --no-restore
dotnet test "<ProjectFile>" --no-restore --no-build
```

If the project intentionally contains zero tests, report that Microsoft Testing Platform may return a non-success zero-tests exit code. Prefer adding a real or wiring smoke test before committing.

## Output

Report:

- created project path;
- packages and project references added;
- solution folder used;
- whether central test configuration was already valid;
- restore/build/test results and any warnings.
