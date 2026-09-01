---
name: umbrella-dotnet-scaffold-architecture-tests
description: 'Add a layer-dependency and implementation-visibility architecture test project that follows the shared Umbrella IsTestProject=true test configuration pattern.'
---

# Scaffold Architecture Tests

## Purpose

Create one runnable architecture test project that enforces layer dependency direction and `internal sealed` visibility rules for concrete implementations using `Umbrella.Testing.Architecture`. This skill assumes the solution uses the shared Umbrella `IsTestProject=true` test configuration pattern and singular `.Test` project naming; use `umbrella-dotnet-standardize-test-projects` first when the solution-level test config is missing or drifted.

## Prerequisites

- `Umbrella.Testing.Architecture` NuGet package must be published and available in the repo's NuGet feed.
- The target repo must follow the standard Umbrella layer structure (Core.Domain -> Core.Data -> Core.Logic -> Web.*).
- The solution-level test configuration must already provide the shared runnable test setup for projects with `<IsTestProject>true</IsTestProject>`.

---

## Step 1 - Preflight and discovery

Before writing anything, read the solution and central build files to answer:

1. **Central test configuration** - read `Directory.Build.props`, `Directory.Build.targets`, and `Directory.Packages.props` before choosing the new project XML. Confirm the solution follows the standard Umbrella pattern:
   - `Directory.Build.props` adds the `Xunit` global using for `IsTestProject=true`.
   - `Directory.Build.targets` provides the runnable test output/packaging behavior, Microsoft Testing Platform runner setup, and common test warning policy for `IsTestProject=true`.
   - `Directory.Packages.props` provides the shared test package stack for `IsTestProject=true`, including the xUnit v3 MTP runner package, coverage, TRX reporting, and Moq when that is the solution standard.
2. **If central config is missing or drifted** - run/use `umbrella-dotnet-standardize-test-projects` first. If that skill is not available, apply the documented central Umbrella test pattern before scaffolding. Do not compensate by adding runner properties or shared test packages to the architecture test project.
3. **Namespace prefix** - the root namespace of the project (e.g., `IndyRecords`, `VinylVault`, `SpinCity.Marketing`). Read any existing `.csproj` to find it.
4. **Existing test project conventions** - check if a `Test\` or `test\` folder exists. Read 1-2 runnable test project `.csproj` files to learn whether the TFM is local or inherited, local `NoWarn` suppressions, package reference style, and whether global usings (e.g., Humanizer) need removing. Test project names must use singular `.Test`, not `.Tests`.
5. **Package management style** - distinguish these cases:
   - True CPM: `Directory.Packages.props` contains `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` and `<PackageVersion>` entries. Put versions there and omit `Version` attributes in the project.
   - Umbrella shared package injection: central files add `<PackageReference>` entries under `Condition="'$(IsTestProject)'=='true'"`. Do not duplicate those packages in the new project.
   - No central test package setup: standardize the solution-level test configuration before scaffolding.
6. **Assembly anchor types** - for each layer assembly, find a concrete public type to use as `typeof(T).Assembly`. The table below shows what to look for:

| Layer | Property | Where to find the anchor type |
|---|---|---|
| `Core.Domain` | `CoreDomain` | Any entity or exception class in the `Core.Domain` project |
| `Core.Data` | `CoreData` | `IServiceCollectionExtensions` or a repository interface in `Core.Data` (skip if no Core.Data layer) |
| `Core.Logic` | `CoreLogic` | `IServiceCollectionExtensions` in `Core.Logic` |
| `Web.Server.Models` | `WebServerModels` | `ResetPasswordModel` or any view/request model in `Web.Server.Models` (skip if absent) |
| `Web.Server.ModelFactories` | `WebServerModelFactories` | `IServiceCollectionExtensions` in `Web.Server.ModelFactories` (skip if absent) |
| `Web.Shared` | `WebShared` | Any shared model class in `Web.Shared` (skip if absent) |
| `Web.Client.Data` | `WebClientData` | Any class or exception in `Web.Client.Data` (skip if absent) |

7. **Layer shape check** - confirm the repo can be represented by `UmbrellaLayerDependencyTests`, which supports one required domain assembly, one optional data assembly, one required logic assembly, and fixed forbidden namespace checks under `<NamespacePrefix>.Core.Data`, `<NamespacePrefix>.Core.Logic`, and `<NamespacePrefix>.Web`. If the repo has multiple domain/data pairs or non-standard data layer names (for example both `Core.Data.Domain`/`Core.Data` and `Core.Dataverse.Domain`/`Core.Dataverse`), do not force all assemblies into one generated test class. Scaffold only the unambiguous standard pair, and report the additional slices as requiring custom architecture tests or an enhancement to `Umbrella.Testing.Architecture`.
8. **Solution file path** - locate the `.sln` or `.slnx` file.

---

## Step 2 - Create the test project csproj

**File location:** `Test\<AppNamespace>.Architecture.Test\<AppNamespace>.Architecture.Test.csproj`

Mirror the existing test project folder convention, but always use singular naming for the project name: `.Architecture.Test`, never `.Architecture.Tests`. Use `Test\` if the solution has a `Test\` solution folder, otherwise place the project at the same level as other runnable test projects.

Use this minimal test-project pattern after the central test configuration preflight passes:

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<!-- Include TargetFramework only if runnable test projects declare it locally.
		     Omit it when the repo inherits TargetFramework from Directory.Build.props. -->
		<!-- <TargetFramework>net10.0</TargetFramework> -->
		<IsTestProject>true</IsTestProject>
		<NoWarn>$(NoWarn);CA1515;CA1707</NoWarn>
	</PropertyGroup>

	<!-- Only include this block if the repo adds global usings that are unavailable
	     in a project that doesn't transitively reference the source package.
	     Example: Humanizer is commonly added globally but may not resolve here. -->
	<!--
	<ItemGroup>
		<Using Remove="Humanizer" />
	</ItemGroup>
	-->

	<ItemGroup>
		<PackageReference Include="Umbrella.Testing.Architecture" Version="<!-- latest published version, or omit Version under true CPM -->" />
	</ItemGroup>

	<!-- Project references to each layer assembly so anchor types can be resolved. -->
	<ItemGroup>
		<ProjectReference Include="..\..\<path-to-Core.Domain>\<AppNamespace>.Core.Domain.csproj" />
		<ProjectReference Include="..\..\<path-to-Core.Logic>\<AppNamespace>.Core.Logic.csproj" />
		<!-- Add Core.Data, Web.* project references only for layers that exist in this repo. -->
	</ItemGroup>

</Project>
```

**Rules:**

- `IsTestProject=true` is required for a runnable architecture test project.
- Keep the project file minimal: only add a local target framework when existing runnable tests do, `IsTestProject=true`, architecture-specific warning suppressions, the `Umbrella.Testing.Architecture` package, and project references.
- Do not add `TargetFramework` when existing runnable tests inherit it from `Directory.Build.props`.
- Do not add runner/output/packaging properties or shared runner/coverage/report/Moq package references. Those belong in the shared solution-level test configuration.
- `Umbrella.Testing.Architecture` is a project-specific package and should stay in this architecture test project.
- `NoWarn` must append with `$(NoWarn);...` so central warning suppressions are preserved.
- `CA1515` must be suppressed because xunit.v3's MTP runner requires test classes to be `public`, which triggers CA1515 warnings in executable test projects.
- `CA1707` must be suppressed because xunit test methods use underscores.
- Do not add central `CS1591` locally; shared test targets append it.
- If true CPM is active, omit `Version=""` from project `<PackageReference>` entries and add/update the corresponding `<PackageVersion>` entries in `Directory.Packages.props`.
- If the shared test package injection is absent, stop and standardize the solution-level test configuration before creating this project.

---

## Step 3 - Create the layer dependency test class

**File location:** `Test\<AppNamespace>.Architecture.Test\ArchitectureLayerDependencyTests.cs`

```csharp
using System.Reflection;
using <Namespace.Of.CoreDomainAnchor>;
using <Namespace.Of.CoreLogicAnchor>;
// add using for each anchor type namespace

namespace <AppNamespace>.Architecture.Test;

public sealed class ArchitectureLayerDependencyTests : UmbrellaLayerDependencyTests
{
    protected override string NamespacePrefix => "<AppNamespace>";
    protected override Assembly CoreDomain => typeof(<CoreDomainAnchorType>).Assembly;
    protected override Assembly CoreLogic => typeof(<CoreLogicAnchorType>).Assembly;

    // Only override these if the layer exists in this repo:
    // protected override Assembly CoreData => typeof(<CoreDataAnchorType>).Assembly;
    // protected override Assembly WebServerModels => typeof(<WebServerModelsAnchorType>).Assembly;
    // protected override Assembly WebServerModelFactories => typeof(<WebServerModelFactoriesAnchorType>).Assembly;
    // protected override Assembly WebShared => typeof(<WebSharedAnchorType>).Assembly;
    // protected override Assembly WebClientData => typeof(<WebClientDataAnchorType>).Assembly;
}
```

**Rules:**

- `public sealed class` is required so xunit.v3's MTP runner discovers the inherited `[Fact]` methods.
- Override only properties for layers that genuinely exist. Omitting a property leaves it `null`, which causes the corresponding tests to be reported as **skipped**, not failed.
- `NamespacePrefix` must match the root namespace exactly (e.g., `"IndyRecords"` not `"IndyRecords."`).
- Do not wire multiple domain assemblies into the single `CoreDomain` property. If more than one domain/data slice exists, scaffold the standard slice only and report the rest as custom work.

---

## Step 4 - Create the visibility test class

**File location:** `Test\<AppNamespace>.Architecture.Test\ArchitectureVisibilityTests.cs`

```csharp
using System.Reflection;
using <Namespace.Of.CoreLogicAnchor>;
// add using for CoreData anchor if it exists

namespace <AppNamespace>.Architecture.Test;

public sealed class ArchitectureVisibilityTests : UmbrellaImplementationVisibilityTests
{
    protected override Assembly CoreLogic => typeof(<CoreLogicAnchorType>).Assembly;

    // Only override if Core.Data layer exists:
    // protected override Assembly CoreData => typeof(<CoreDataAnchorType>).Assembly;
}
```

**Rules:**

- Same `public sealed class` requirement as Step 3.

---

## Step 5 - Add to solution

```powershell
dotnet sln "<SolutionFile.sln>" add "Test\<AppNamespace>.Architecture.Test\<AppNamespace>.Architecture.Test.csproj" --solution-folder "Test"
```

Adjust `--solution-folder` to match how other test projects are organised in the solution (e.g., `"Test"`, `"test"`, or a nested path). Keep the project and folder names singular even when an existing solution folder is lower-case.

---

## Step 6 - Build and run

Run the new project first:

```powershell
dotnet test "Test\<AppNamespace>.Architecture.Test\<AppNamespace>.Architecture.Test.csproj"
```

Then run the full solution so central test conventions and helper projects are validated together:

```powershell
dotnet test "<SolutionFile.sln>" --no-restore --verbosity minimal
```

**Expected outcome:**

- Tests for layers that exist: **Passed**
- Tests for layers that were omitted (null): **Skipped**
- **No failed tests** - if any fail, read the failing type names and fix the underlying violation before committing.

**Common failure causes:**

- A repository / service / file-handler / auth-handler implementation is `public` or non-`sealed`.
- A reusable implementation base should be `internal abstract` and use an explicit `*Base` suffix such as `FileHandlerBase`; only concrete DI-resolved implementations should use the bare `*FileHandler`, `*Service`, `*Repository`, or `*AuthorizationHandler` suffix. Current `Umbrella.Testing.Architecture` releases exclude abstract types from the sealed-implementation rule. If an older package reports an abstract base, upgrade the package rather than sealing the abstract type.
- A `Core.Logic` type imports a type from `Web.*` (forbidden upward dependency).
- A `Core.Domain` type transitively references `Core.Data` (usually via a wrong project reference).
- A repo uses multiple or non-standard domain/data slices; those may need custom tests because `Umbrella.Testing.Architecture` currently models one standard `Core.Domain` / `Core.Data` pair.
- A helper/shared test support project is accidentally configured as runnable (`IsTestProject=true`) but has no tests, which can cause the MTP runner to return a failure exit code. Helper projects should use `IsTestProject=false`, avoid runner setup and shared runner packages, and reference `xunit.v3.assert` only if assertion APIs are needed.

---

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Verification checklist

1. `dotnet build` produces no errors and only expected warnings.
2. `dotnet test` for the architecture test project reports no failures; skipped tests correspond exactly to the layers not wired up.
3. `dotnet test` for the full solution reports no failures.
4. The test project is visible in the solution under the correct solution folder.
5. Anchor types (`typeof(T).Assembly`) are in the correct layer assembly - verify each `using` resolves to the intended csproj.
