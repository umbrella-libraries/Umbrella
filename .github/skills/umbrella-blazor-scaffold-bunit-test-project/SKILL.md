---
name: umbrella-blazor-scaffold-bunit-test-project
description: 'Create a bUnit component test project for an Umbrella Blazor client: Microsoft.NET.Sdk.Razor with IsTestProject, a centrally versioned bunit 2.x reference chosen by age and target framework support, Moq removed, a reference to the Blazor client project, solution registration, and a first BunitContext smoke test with strict JavaScript interop and real services. Use before generating bUnit component tests.'
---

# Scaffold bUnit Test Project

## Purpose

Create one runnable test project that renders the Blazor client's components in memory with bUnit, using their real services. bUnit stands in only for the browser: JavaScript interop and navigation. No Docker or browser is needed.

This skill builds on `umbrella-dotnet-scaffold-test-project` (naming, shared test configuration, solution registration) and creates the shell plus one smoke test. Component tests come from `umbrella-blazor-generate-bunit-component-tests`.

## Preflight

1. Read the repo's test README (for example `Test\README.md`) if it exists.
2. Search for an existing project referencing `bunit`. If one exists, stop and use `umbrella-blazor-generate-bunit-component-tests` instead.
3. Find the Blazor client project (`Microsoft.NET.Sdk.BlazorWebAssembly`, or a Razor class library holding the shared components) and its target framework.
4. Read `Directory.Build.props`, `Directory.Build.targets` and `Directory.Packages.props`. Confirm runnable test projects opt in with `<IsTestProject>true</IsTestProject>` and that shared runner packages (xUnit v3, MTP, coverage, TRX, Moq) are injected centrally. If the shared configuration is missing or drifted, run `umbrella-dotnet-standardize-test-projects` first.
5. Mirror existing test projects: `Test\` folder casing, local `TargetFramework` or inherited, `NoWarn` entries, CPM style.
6. Pick a smoke-test component: a leaf component with no server calls, whose services can be registered for real (for example pagination, a toggle, a formatted date).

## Step 1 -- Choose the bunit version

Pick the latest stable `bunit` 2.x that was published at least two weeks ago and supports the target framework:

1. List versions and publish dates from the NuGet registration API (`https://api.nuget.org/v3/registration5-gz-semver2/bunit/index.json`) or `dotnet package search bunit --exact-match`.
2. Discard prereleases, versions outside 2.x, and versions published less than 14 days ago.
3. Confirm the chosen version's dependency groups include the client's target framework (or a compatible lower one).
4. If the repo already pins a `bunit` version, keep it unless the user asks to upgrade.

Add the version centrally, in the test-only package group:

```xml
<PackageVersion Include="bunit" Version="2.x.y" />
```

## Step 2 -- Create the project file

Create `Test\<App>.Web.Client.Test\<App>.Web.Client.Test.csproj` (for example `IndyRecords.Web.Client.Test`). Use the Razor SDK, so tests can be written as `.razor` files as well as `.cs`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

	<PropertyGroup>
		<IsTestProject>true</IsTestProject>
		<!-- CA1515: xUnit v3 test classes must be public. CA1707: underscore-style test names. -->
		<NoWarn>$(NoWarn);CA1515;CA1707</NoWarn>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="bunit" />
		<!-- The components are tested with their real services, not mocks. -->
		<PackageReference Remove="Moq" />
	</ItemGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\Web\IndyRecords.Web.Client\IndyRecords.Web.Client.csproj" />
	</ItemGroup>

</Project>
```

Rules:

- Omit `Version` under CPM; never declare `PackageVersion` in the project.
- Add `TargetFramework` only when other runnable test projects declare it locally.
- Remove the centrally injected Moq: the tests use real services, and Moq can pull in a conflicting Castle.Core.
- Do not add shared runner packages that central configuration already injects.
- After the first build, remove inherited global usings the project cannot resolve (`<Using Remove="..." />`), rather than adding unrelated references.

## Step 3 -- Register the project in the solution

```powershell
dotnet sln "<SolutionFile>" add "Test\IndyRecords.Web.Client.Test\IndyRecords.Web.Client.Test.csproj" --solution-folder Test
```

Match the solution's existing test folder. For `.slnx`, check the entry lands in the same `<Folder>` as the other test projects.

## Step 4 -- Add the smoke test

Derive the test class from `BunitContext` (bUnit 2's name for the old `TestContext`), register the component's real services, and set up only the JavaScript calls the test expects:

```csharp
using Bunit;
using IndyRecords.Web.Client.Components.Shared;
using IndyRecords.Web.Client.Services;
using IndyRecords.Web.Client.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace IndyRecords.Web.Client.Test.Components.Shared;

/// <summary>
/// The release pagination, with its real interop service. bUnit's JavaScript interop is strict, so any call other
/// than the one set up here fails the test.
/// </summary>
public sealed class ReleasePaginationTests : BunitContext
{
	private const string ScrollToResults = "IndyRecordsInterop.scrollToResults";

	public ReleasePaginationTests()
	{
		_ = Services.AddLogging();
		_ = Services.AddScoped<IBrowserInteropService, BrowserInteropService>();

		_ = JSInterop.SetupVoid(ScrollToResults);
	}

	[Fact]
	public async Task ClickingAPage_MarksItCurrent_AndScrollsToTheResults()
	{
		var pagination = Render<ReleasePagination>(parameters => parameters.Add(x => x.TotalCount, 100));

		await pagination.Find("a[aria-label='Page 3']").ClickAsync(new());

		Assert.Equal("page", pagination.Find("a[aria-label='Page 3']").GetAttribute("aria-current"));
		_ = JSInterop.VerifyInvoke(ScrollToResults, calledTimes: 1);
	}
}
```

Rules for the smoke test:

- Keep JavaScript interop in its default strict mode. Set up each expected call (`SetupVoid`, `Setup<T>`), and verify it with `VerifyInvoke` or `VerifyNotInvoke`. Do not switch to loose mode to make a test pass.
- Register real services. If a component needs the server (HTTP data services, authentication round trips), choose another component for the smoke test; those components are covered by browser journey tests (`umbrella-dotnet-generate-e2e-journey-tests`).
- Assert what a user perceives: text, ARIA attributes, links, raised callbacks.
- Use `Render<T>` (bUnit 2), not the removed `RenderComponent<T>`.

Then fix the culture for the whole test assembly. Linux build agents often have no locale, so .NET runs with the
invariant culture, whose name is empty; a browser always gives WebAssembly a culture. Components, and libraries such as
an HTTP service that sends the culture, then behave differently on CI than in the browser or on Windows (one HTTP
service rejected the empty name before sending, so every search in the component tests failed on CI only). Set the
site's main culture once, in a module initializer, and let tests that check another culture set their own:

```csharp
using System.Globalization;
using System.Runtime.CompilerServices;

namespace IndyRecords.Web.Client.Test.Shared;

internal static class TestCulture
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		var culture = CultureInfo.GetCultureInfo("en-GB");

		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		CultureInfo.CurrentCulture = culture;
		CultureInfo.CurrentUICulture = culture;
	}
}
```

## Rules

- One bUnit project per Blazor client.
- No mocking library in the project.
- Versions only in `Directory.Packages.props`.
- Do not change the client project to make components testable unless the user asks.

## Validation

```powershell
dotnet restore "<TestProject>"
dotnet build "<TestProject>" --no-restore
dotnet test "<TestProject>" --no-restore --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<TestProject>"`. Check the project appears in the solution and that the smoke test runs (not zero tests discovered).

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report:

- the project path, SDK and solution folder;
- the bunit version chosen, its publish date and the target framework it supports;
- references added and packages removed (Moq) and any global usings removed;
- the smoke test component, services registered and interop calls set up;
- restore/build/test results.
