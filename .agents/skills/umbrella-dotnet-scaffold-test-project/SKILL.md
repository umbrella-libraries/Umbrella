---
name: umbrella-dotnet-scaffold-test-project
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
4. If central test configuration is missing or drifted, use `umbrella-dotnet-standardize-test-projects` first. Do not compensate by duplicating shared runner properties or shared runner packages in the new project.
5. Inspect existing runnable test projects to mirror:
   - `Test\` vs `test\` folder casing;
   - whether `TargetFramework` is inherited or local;
   - local `NoWarn` entries;
   - package-version style;
   - every inherited global using the new project's references cannot resolve, commonly but not exclusively `Humanizer`.
6. Check the worktree. Do not overwrite unrelated user changes.

## Project naming

- Use singular `.Test`, never `.Tests`.
- Prefer `Test\<ProjectName>.Test\<ProjectName>.Test.csproj`.
- For app-specific web tests, use the app namespace and layer in the name, for example `IndyRecords.Web.Server.Test`.
- For general tests, use a clear feature/slice name such as `<App>.Architecture.Test` or `<App>.Mapperly.Test`.

## Project layout

Every repository follows the same layout, so a test is found in the same place in each one. Name a project after the
production project it tests; a test project that starts the web host is never also the home of host-free unit tests.

```text
Test\
	<App>.Architecture.Test\          layer and visibility rules
	<App>.<Layer>.Test\               host-free unit tests for one production project, for example Core.Logic.Test or
	                                  Web.Server.ModelFactories.Test; lower layers it references go in folders (Domain\, Shared\)
	<App>.<Layer>.Testing\            IsTestProject=false: in-memory fakes and builders shared by unit test projects
	<App>.Web.Server.Unit.Test\       host-free unit tests for classes in the server project itself (tag helpers, rewrite
	                                  rules, middleware built by hand); only when there are such classes
	<App>.Web.Server.Test\            integration tests that start the host (containers, WebApplicationFactory)
	<App>.Web.Server.Testing\         IsTestProject=false: the factory, seeders, stubs and recorders shared by the server,
	                                  browser and live test projects
	<App>.Web.Browser.Test\           Playwright tests over the Kestrel host
	<App>.Web.Client.Test\            bUnit component tests
	<App>.Web.Live.Test\              opt-in checks against real external services
```

- A unit test project runs without Docker, a browser or the host. It references production projects and
  `<App>.<Layer>.Testing` libraries only: never the host helper libraries (`*.Web.Server.Testing`, or a
  framework-specific host helper library), Testcontainers, WireMock or Playwright.
- Shared fakes go in the `<App>.<Layer>.Testing` library for the lowest layer whose types they need (for example
  `<App>.Core.Logic.Testing` for a fake content repository), referenced by each unit test project that uses them. Never
  put them in a host helper library, and never share them as linked source files. Fakes used by one project stay in
  that project's folder.
- A test that needs the running host (services only the host registers, routing, rendering, the database) belongs in
  `<App>.Web.Server.Test`, even when it tests one class.
- Add each new project to the solution's `Test` folder and to the test README's project table and helper map.

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
- After the first build, inspect both inherited `<Using Include="..." />` items and the generated `obj\<Configuration>\<TargetFramework>\*.GlobalUsings.g.cs`. Remove every unresolved inherited global using explicitly rather than adding unrelated package/project references merely to satisfy it. `Humanizer` is only an example; consumer repositories can inherit application, logging, toolkit, or Umbrella namespaces too.
- Remove inherited global usings only when the new test project cannot resolve them:

```xml
<ItemGroup>
	<Using Remove="Humanizer" />
</ItemGroup>
```

### Razor SDK variant for bUnit projects

A Blazor component test project (bUnit) must compile `.razor` test files and the components' Razor output, so it uses the Razor SDK instead of the plain .NET SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

	<PropertyGroup>
		<IsTestProject>true</IsTestProject>
		<NoWarn>$(NoWarn);CA1515;CA1707</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="bunit" />
		<PackageReference Remove="Moq" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\Web\IndyRecords.Web.Client\IndyRecords.Web.Client.csproj" />
	</ItemGroup>

</Project>
```

Everything else in this skill applies unchanged. Remove a centrally injected Moq only when the project tests components with their real services, as the bUnit convention does. Use `umbrella-blazor-scaffold-bunit-test-project` for the full bUnit setup (package versions, interop conventions, an example component test); this skill only creates the shell.

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

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<ProjectFile>"`. Check `global.json` when creating the first test project in a repository, and add the runner setting if the repo uses Microsoft Testing Platform.

If the project intentionally contains zero tests, report that Microsoft Testing Platform may return a non-success zero-tests exit code. Prefer adding a real or wiring smoke test before committing.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report:

- created project path;
- packages and project references added;
- solution folder used;
- whether central test configuration was already valid;
- restore/build/test results and any warnings.
