---
name: dotnet-scaffold-architecture-tests
description: 'Add a layer-dependency and implementation-visibility architecture test project to a repo using the Umbrella.Testing.Architecture NuGet package and xunit.v3.'
---

# Scaffold Architecture Tests

## Purpose

Create an architecture test project that enforces layer dependency direction and `internal sealed` visibility rules using `Umbrella.Testing.Architecture` and xunit.v3.

## Prerequisites

- `Umbrella.Testing.Architecture` NuGet package must be published and available in the repo's NuGet feed.
- The target repo must follow the standard Umbrella layer structure (Core.Domain → Core.Data → Core.Logic → Web.*).

---

## Step 1 — Discovery

Before writing anything, read the solution to answer:

1. **Namespace prefix** — the root namespace of the project (e.g., `IndyRecords`, `VinylVault`, `SpinCity.Marketing`). Read any existing `.csproj` to find it.
2. **Existing test project conventions** — check if a `Tests\` or `test\` folder exists. Read 1–2 existing test project `.csproj` files to learn the TFM, NoWarn suppressions, and whether global usings (e.g., Humanizer) need removing.
3. **Assembly anchor types** — for each layer assembly, find a concrete public type to use as `typeof(T).Assembly`. The table below shows what to look for:

| Layer | Property | Where to find the anchor type |
|---|---|---|
| `Core.Domain` | `CoreDomain` | Any entity or exception class in the `Core.Domain` project |
| `Core.Data` | `CoreData` | `IServiceCollectionExtensions` or a repository interface in `Core.Data` (skip if no Core.Data layer) |
| `Core.Logic` | `CoreLogic` | `IServiceCollectionExtensions` in `Core.Logic` |
| `Web.Server.Models` | `WebServerModels` | `ResetPasswordModel` or any view/request model in `Web.Server.Models` (skip if absent) |
| `Web.Server.ModelFactories` | `WebServerModelFactories` | `IServiceCollectionExtensions` in `Web.Server.ModelFactories` (skip if absent) |
| `Web.Shared` | `WebShared` | Any shared model class in `Web.Shared` (skip if absent) |
| `Web.Client.Data` | `WebClientData` | Any class or exception in `Web.Client.Data` (skip if absent) |

4. **Solution file path** — locate the `.sln` or `.slnx` file.
5. **Central Package Management** — check if a `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` exists. If so, `Version` attributes belong in that file, not the csproj.

---

## Step 2 — Create the test project csproj

**File location:** `Tests\<AppNamespace>.Architecture.Tests\<AppNamespace>.Architecture.Tests.csproj`

(Mirror the existing test project folder convention; use `Tests\` if the solution has a `Tests\` solution folder, otherwise place at the same level as other test projects.)

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<TargetFramework><!-- match TFM of other test projects in this repo --></TargetFramework>
		<IsPackable>false</IsPackable>
		<IsTestProject>true</IsTestProject>
		<NoWarn>$(NoWarn);CA1515;CA1707</NoWarn>
		<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
		<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
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
		<PackageReference Include="Microsoft.NET.Test.Sdk" Version="<!-- copy from another test project -->" />
		<PackageReference Include="Umbrella.Testing.Architecture" Version="<!-- latest published version -->" />
		<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
			<PrivateAssets>all</PrivateAssets>
			<IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
		</PackageReference>
		<PackageReference Include="xunit.v3" Version="3.2.2" />
	</ItemGroup>

	<!-- Project references to each layer assembly so anchor types can be resolved -->
	<ItemGroup>
		<ProjectReference Include="..\..\<path-to-Core.Domain>\<AppNamespace>.Core.Domain.csproj" />
		<ProjectReference Include="..\..\<path-to-Core.Logic>\<AppNamespace>.Core.Logic.csproj" />
		<!-- Add Core.Data, Web.* project references only for layers that exist in this repo -->
	</ItemGroup>

</Project>
```

**Rules:**
- `CA1515` — must be suppressed because xunit.v3's MTP runner requires test classes to be `public`, which triggers CA1515 warnings in executable test projects.
- `CA1707` — must be suppressed because xunit test methods use underscores.
- Use the same `Microsoft.NET.Test.Sdk` version as other test projects in the repo.
- `xunit.v3` comes from the meta-package; `Umbrella.Testing.Architecture` brings in `xunit.v3.extensibility.core` and `xunit.v3.assert` as transitive dependencies.
- If CPM is active, omit `Version=""` from csproj `<PackageReference>` entries and add the versions to `Directory.Packages.props` instead.

---

## Step 3 — Create the layer dependency test class

**File location:** `Tests\<AppNamespace>.Architecture.Tests\ArchitectureLayerDependencyTests.cs`

```csharp
using System.Reflection;
using <Namespace.Of.CoreDomainAnchor>;
using <Namespace.Of.CoreLogicAnchor>;
// add using for each anchor type namespace

namespace <AppNamespace>.Architecture.Tests;

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
- `public sealed class` — required so xunit.v3's MTP runner discovers the inherited `[Fact]` methods.
- Override only properties for layers that genuinely exist. Omitting a property leaves it `null`, which causes the corresponding tests to be reported as **skipped** (not failed).
- `NamespacePrefix` must match the root namespace exactly (e.g., `"IndyRecords"` not `"IndyRecords."`).

---

## Step 4 — Create the visibility test class

**File location:** `Tests\<AppNamespace>.Architecture.Tests\ArchitectureVisibilityTests.cs`

```csharp
using System.Reflection;
using <Namespace.Of.CoreLogicAnchor>;
// add using for CoreData anchor if it exists

namespace <AppNamespace>.Architecture.Tests;

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

## Step 5 — Add to solution

```
dotnet sln "<SolutionFile.sln>" add "Tests\<AppNamespace>.Architecture.Tests\<AppNamespace>.Architecture.Tests.csproj" --solution-folder "Tests"
```

Adjust `--solution-folder` to match how other test projects are organised in the solution (e.g., `"Tests"`, `"test"`, or a nested path).

---

## Step 6 — Build and run

```
dotnet test "Tests\<AppNamespace>.Architecture.Tests\<AppNamespace>.Architecture.Tests.csproj"
```

**Expected outcome:**
- Tests for layers that exist: **Passed**
- Tests for layers that were omitted (null): **Skipped**
- **No failed tests** — if any fail, read the failing type names and fix the underlying violation before committing.

**Common failure causes:**
- A repository / service / file-handler / auth-handler implementation is `public` or non-`sealed`.
- A `Core.Logic` type imports a type from `Web.*` (forbidden upward dependency).
- A `Core.Domain` type transitively references `Core.Data` (usually via a wrong project reference).

---

## Verification checklist

1. `dotnet build` produces no errors and only expected warnings.
2. `dotnet test` reports no failures; skipped tests correspond exactly to the layers not wired up.
3. The test project is visible in the solution under the correct solution folder.
4. Anchor types (`typeof(T).Assembly`) are in the correct layer assembly — verify each `using` resolves to the intended csproj.
