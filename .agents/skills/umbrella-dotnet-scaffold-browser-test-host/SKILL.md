---
name: umbrella-dotnet-scaffold-browser-test-host
description: 'Scaffold a Playwright browser test project for an Umbrella ASP.NET Core app: a Kestrel mode in the WebApplicationFactory on a loopback port, a separate test project with an assembly fixture when the app hosts once per process, a PageTest base with a fixed context and off-host request blocking, axe and a per-OS visual snapshot helper, and the CI browser install and failure artifacts. Use before generating browser accessibility, visual or journey tests.'
---

# Scaffold Browser Test Host

## Purpose

Set up, once, everything browser tests need: the real app served by Kestrel on a loopback port, a Playwright test project sharing one host per assembly, a base class that makes every page deterministic and keeps the browser on the test site and the hosts the user chose to keep, an accessibility package and a visual snapshot helper, and the CI steps that install browsers and keep failure evidence.

Individual tests come later from `umbrella-dotnet-generate-browser-accessibility-visual-tests` (axe and visual baselines) and `umbrella-dotnet-generate-e2e-journey-tests` (multi-page journeys).

## Preflight

1. Read the repo's test README (for example `Test\README.md`) if it exists.
2. Find the factory deriving from `UmbrellaWebApplicationFactory<TProgram>` and confirm its isolation: overrides as the last configuration source, unreachable or stubbed external endpoints, an Error-level `ILoggerProvider` recorder, Docker-gated attributes and a shared host per assembly. If any is missing, run `umbrella-dotnet-configure-integration-test-isolation` first.
3. Search for existing browser helpers and reuse them: a Kestrel option on the factory, a class deriving from Playwright's `PageTest`, a visual snapshot comparer, an existing `*.Browser.Test` project.
4. Decide whether the app can host twice in one process. Apps with process-wide static state (service locators, static initialization modules, static caches written at startup) cannot. Then the Kestrel host needs its own test project; otherwise it can share the integration test project.
5. Check what the pages need to render: a front-end build (`wwwroot\dist`, scoped CSS), seeded data, and the external stubs from `umbrella-dotnet-scaffold-wiremock-stub`.
6. Read `Directory.Packages.props` for CPM and the test-only package group.
7. List the off-host resources the pages load in the browser, from the layout, the Content Security Policy and the page markup: CDNs, web fonts, analytics and tag managers, consent banners, maps, video and social embeds. Give a recommendation and reason for each, for example:

| Host | Used for | Recommendation | Reason |
|---|---|---|---|
| `analytics.example.com` | Analytics and tag manager | Block | Tests must not report to tracking; adds variable load |
| `cdn.example.net` | A required script library | Keep real, or serve it locally | Pages break without it |
| `fonts.example.com` | Web fonts | Keep real, or serve locally | Screenshots change without them |
| `video.example.com` | A video embed | Block, or a placeholder per test | Heavy, varies, not the app's markup |

   **Ask the user what to do with each host** (keep real, serve locally, or block) and record the answers in the `PageTest` base and the test README. Do not block a host the pages need without that answer.

   When the user keeps a file the layout pins to one version with an `integrity` hash (an icon font's stylesheet, a script library), allow that version's URL prefix (for example `https://cdn.example.net/icons@2.1.4/`) rather than the whole host: nothing else on the host loads, and the file can't change under the baselines.
8. Find the widths the layout changes at: the breakpoints the styles use (the framework's grid breakpoints, `media-breakpoint-up(...)` mixins, plain `@media` rules) and the widths the scripts switch at (`matchMedia`, `innerWidth` checks, a carousel's `breakpoints` option). Choose one width in each band, from the narrowest phone layout to the width where the containers stop growing (for example 375, 768, 992 and 1280), and confirm the list with the user. The visual tests run at every width and the accessibility tests at the narrowest and widest (Step 3).
9. List what every page's layout renders from site-wide settings: the header and its menu, the footer, a cookie or announcement banner. **Ask the user** whether the browser host should show them as the live site does, with realistic content, and recommend it: every page screenshot and accessibility baseline includes them, so a test site with an unused header variant or an empty footer approves baselines visitors never see, and changing it later means reviewing every baseline again. The integration tests can keep their own seed (Step 2).

## Step 1 -- Add a Kestrel mode to the factory

Browsers need a real socket; `TestServer` is in-memory only. Add a mode to the shared factory rather than a second factory hierarchy:

- `protected virtual bool ServeOverKestrel => false;`
- `public Uri ServerAddress { get; private set; }`, read from `IServerAddressesFeature` after the Kestrel host starts;
- `public Uri SiteUrl => ServeOverKestrel ? ServerAddress : new Uri("http://localhost/");`, to seed as the app's own base URL so absolute links point at the server under test;
- `public IServiceProvider HostServices`, the Kestrel host's services when enabled, because `Services` then belongs to a placeholder;
- a client method (`CreateSiteClient()`, or the factory's existing per-test-user `CreateClient` overload) that, in Kestrel mode, returns an `HttpClient` with `BaseAddress = ServerAddress`, because the base `CreateClient()` overloads then reach the placeholder.

Do not name the switch `UseKestrel`. On .NET 10, `Microsoft.AspNetCore.Mvc.Testing` has public `UseKestrel()`, `UseKestrel(int)`, `UseKestrel(Action<KestrelServerOptions>)` and `StartServer()`, and a derived `UseKestrel` property hides them (CS0108). A factory written earlier (for example on .NET 8) with a `UseKestrel` property must rename it, and its overrides, when it moves to .NET 10.

Use one mechanism, not both. On `Microsoft.AspNetCore.Mvc.Testing` 10 or later, first check whether the built-in `UseKestrel(0)` and `StartServer()` work with the app and the Umbrella base; prefer them when they do, and then add no `CreateHost` override. Otherwise override `CreateHost` as in `references\kestrel-factory.md`, and never call the built-in `UseKestrel()` on the same factory:

1. Configure the app's builder with `UseKestrel().UseUrls("http://127.0.0.1:0")` (the `IWebHostBuilder` extension: loopback, dynamic port), build it once, and start it.
2. Read the bound address from `IServer.Features.Get<IServerAddressesFeature>()`, and throw if none is reported.
3. When the factory derives from `UmbrellaSqlServerAzuriteWebApplicationFactory<TProgram, TDbContext>`, call `InitializeDatabase(_kestrelHost.Services)` after `Start()`. Its `CreateHost` runs the migrations, and the Kestrel branch does not call it, so without this the database is never migrated. A project-specific container factory that seeds in `CreateHost` needs the same call to its seeding.
4. Return an empty placeholder host on `UseTestServer()`, because `WebApplicationFactory` requires its host to run on `TestServer`. Nothing is served by it.
5. Stop and dispose the Kestrel host in `DisposeAsync` before `base.DisposeAsync()`.

Build the app's builder only once: apps that host once per process fail on a second build.

## Step 2 -- Create the browser test project

Use `umbrella-dotnet-scaffold-test-project` for the shell, named `<App>.Web.Browser.Test` (for example `IndyRecords.Web.Browser.Test`), then:

```xml
<ItemGroup>
	<PackageReference Include="Microsoft.Playwright.Xunit.v3" />
	<PackageReference Include="Deque.AxeCore.Playwright" />
	<PackageReference Include="Magick.NET-Q8-AnyCPU" />
	<!-- Real services, no mocks; Moq can also pull in a conflicting Castle.Core. -->
	<PackageReference Remove="Moq" />
</ItemGroup>

<ItemGroup>
	<ProjectReference Include="..\IndyRecords.Web.Server.Testing\IndyRecords.Web.Server.Testing.csproj" />
</ItemGroup>

<ItemGroup>
	<None Include="Visual\Snapshots\**" />
</ItemGroup>
```

- Add `PackageVersion` entries centrally in the test-only group; pick versions at least two weeks old that support the target framework. Any image library with per-pixel access works; `Magick.NET-Q8-AnyCPU` is the reference choice.
- Add a browser factory that only switches the mode:

```csharp
public sealed class IndyRecordsBrowserWebApplicationFactory : IndyRecordsWebApplicationFactory
{
	protected override bool ServeOverKestrel => true;
}
```

- When the user chose a realistic site-wide header and footer (Preflight step 9), publish them in this factory after the shared seed and keep them for the host's life, so the integration tests' seed is unchanged:

```csharp
public sealed class IndyRecordsBrowserWebApplicationFactory : IndyRecordsWebApplicationFactory
{
	protected override bool ServeOverKestrel => true;

	protected override async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		await base.SeedAsync(services, cancellationToken);

		// The live site's header and footer, so every page and its baselines show what visitors see.
		SiteHeaderAndFooter.ShowOnTheSite(services, Content);
	}
}
```

  Use the same content class the header and footer tests use, so the two never drift apart. Clear any cache that holds the rendered header (a `<cache>` tag helper, the output cache) after publishing, and say in the test README which seed each test project sees.
- Share one host per assembly, with console capture for startup logs:

```csharp
[assembly: AssemblyFixture(typeof(IndyRecordsBrowserWebApplicationFactory))]
[assembly: CaptureConsole]
```

- xUnit only honours a `[CollectionDefinition]` declared in the test assembly itself, so re-declare every shared collection the browser tests use (stub and data-mutation collections) with `DisableParallelization = true`.

## Step 3 -- Create the PageTest base

Derive one abstract base from Playwright's `PageTest`, so every test gets a fresh page with the same, fixed context:

```csharp
public abstract class IndyRecordsPageTest : PageTest
{
	protected IndyRecordsPageTest(IndyRecordsBrowserWebApplicationFactory factory) => Factory = factory;

	protected IndyRecordsBrowserWebApplicationFactory Factory { get; }

	protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

	public override BrowserNewContextOptions ContextOptions() => new()
	{
		BaseURL = Factory.ServerAddress.ToString(),
		ViewportSize = new() { Width = 1280, Height = 800 },
		DeviceScaleFactor = 1,
		Locale = "en-GB",
		TimezoneId = "Europe/London",
		ReducedMotion = ReducedMotion.Reduce,
	};

	// The hosts the user chose to keep real (Preflight step 7), and pinned files kept by their version's URL prefix.
	// Everything else off-host is aborted.
	private static readonly string[] _allowedOffHostAuthorities = ["fonts.example.com"];
	private static readonly string[] _allowedOffHostUrlPrefixes = ["https://cdn.example.net/icons@2.1.4/"];

	public override async ValueTask InitializeAsync()
	{
		await base.InitializeAsync();

		// An allowlist: the test site plus the hosts the user agreed to keep. Analytics and other blocked hosts are
		// aborted, so tests never report to tracking services and screenshots don't vary with third-party content.
		string siteHost = Factory.ServerAddress.Authority;
		_ = await Context.RouteAsync("**/*", route =>
		{
			string authority = new Uri(route.Request.Url).Authority;
			return authority == siteHost
				|| _allowedOffHostAuthorities.Contains(authority, StringComparer.OrdinalIgnoreCase)
				|| _allowedOffHostUrlPrefixes.Any(x => route.Request.Url.StartsWith(x, StringComparison.Ordinal))
				? route.ContinueAsync()
				: route.AbortAsync();
		});
	}
}
```

Leave the allowlist empty when the user keeps nothing real. A resource served locally (copied into the test project and fulfilled with `route.FulfillAsync(new() { Path = ... })`) is another option for a required asset whose host is unreliable. Allowed hosts make the tests depend on that host being up; say so in the test README.

Add `Shared\Viewports.cs` from `references\viewports.md` with the widths from Preflight step 8. The context's default viewport stays fixed for tests that don't depend on width; a test that does sets its width with `Viewports.SetAsync` before opening the page, and puts the width's name in its baseline names.

Add small helpers the app needs, such as resolving a seeded item's URL through `Factory.HostServices`. Use the app's real locale and time zone; keep them fixed so dates and formatting do not vary by machine.

## Step 4 -- Add the visual snapshot helper

Add a `VisualSnapshot` helper following `references\visual-snapshot.md`, with `CaptureFullPageAsync(IPage page, params ILocator[] mask)` and `AssertMatchesAsync(byte[] screenshot, string snapshotName)`. Its contract:

- baselines are per OS (`Snapshots\{name}.{windows|linux|macos}.png`), because fonts render differently on each;
- baselines live in the source tree, found with `[CallerFilePath]`, so approving one is a normal reviewed change;
- a pixel matches when each of its channels is within 2 levels of the baseline's: Chromium's anti-aliasing of rounded corners and scaled images varies by a level or so between runs of the same page, while a real change to text, layout or colour moves channels much further. Don't raise the tolerance to hide a difference;
- on a size difference both images are padded, not resized, before diffing, so the change stays visible;
- `CaptureFullPageAsync` takes a full-page screenshot with animations disabled and the caret hidden, and crops it to the window's width. A full-page screenshot is as wide as the page's content, so a carousel's off-screen slides, which the site clips (`overflow-x: hidden` on `html` or a wrapper), would otherwise widen it with content visitors never see;
- a missing or different baseline fails the test and writes `.received.png` (and `.expected.png` and `.diff.png` when a baseline exists) to `bin\...\visual-artifacts`;
- a baseline is never created or updated automatically. A person reviews the received image and copies it over the baseline.

The axe package needs no helper here; the accessibility gate (serious and critical violations) is written by `umbrella-dotnet-generate-browser-accessibility-visual-tests`.

## Step 5 -- Add one smoke test

Add one test proving the wiring, using the Docker-gated attribute: load the home page, assert a 200 response and a visible landmark or title. Leave accessibility, visual and journey tests to the generate skills.

## Step 6 -- Install browsers locally

After the first build:

```powershell
pwsh Test\IndyRecords.Web.Browser.Test\bin\Debug\<tfm>\playwright.ps1 install chromium
```

Document this, the front-end build, and Docker in the test README.

## Step 7 -- Add the CI steps

The pipeline must, in order:

1. Run on Linux agents with Docker (container tests and Linux baselines need them).
2. Build the front-end assets and the solution, including the browser test project.
3. Install the browsers with the first `playwright.ps1` found under a test project's `bin\<Configuration>` folder, adding `--with-deps` on Linux because hosted agents lack the browsers' system libraries:

```powershell
$installArguments = @('install')
if ($IsLinux) { $installArguments += '--with-deps' }
& $playwrightScript.FullName @installArguments chromium
if ($LASTEXITCODE -ne 0) { throw "Playwright browser install failed with exit code $LASTEXITCODE." }
```

4. Run `dotnet test` for each `*.Test.csproj`.
5. On failure, publish each test project's `bin\**\TestResults` and `bin\**\visual-artifacts` files as a `test-artifacts` artifact (excluding TRX and coverage files, published separately).

Approve a Linux baseline from that artifact: download it, review the received image and diff, then copy the image to `Visual\Snapshots\<name>.linux.png` and commit it. Do not create Linux baselines from WSL or a local container; the agent's fonts are what CI compares against.

If the pipeline templates are generated by a shared tool, change the template at its source as well and tell the user.

## Rules

- The browser reaches only the test site and the off-host hosts the user chose to keep; the route is an allowlist, and every other off-host request is aborted. Ask before blocking or allowing a host.
- Never name the Kestrel switch `UseKestrel`, and never mix the built-in `UseKestrel()` with a `CreateHost` override.
- Never auto-create or auto-update a visual baseline.
- Keep the browser host in its own test project when the app can host only once per process.
- Keep Kestrel on `127.0.0.1` with a dynamic port; never a fixed port.
- Paths and snapshot names are case-sensitive on Linux CI; keep them lowercase and exact.

## Validation

```powershell
dotnet build "<BrowserTestProject>"
pwsh "<BrowserTestProject>\bin\Debug\<tfm>\playwright.ps1" install chromium
dotnet test "<BrowserTestProject>" --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<BrowserTestProject>"`.

When the app has a Blazor WebAssembly client, build the browser test project with `--no-incremental` after changing the client, the server project or the configuration: an incremental build can leave stale fingerprinted `_framework\*.wasm` references in the static web assets, and every Blazor page then times out after a 404 for a `.wasm` file. Say so in the test README.

The smoke test passes with Docker running and is skipped without it locally. Confirm the existing integration tests still pass with the factory change (Kestrel mode off).

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## References

- `references\kestrel-factory.md`
- `references\viewports.md`
- `references\visual-snapshot.md`

## Output

Report:

- how Kestrel mode was added (built-in or `CreateHost` override) and the members exposed;
- the browser test project, packages and versions, assembly fixture and re-declared collections;
- the PageTest base, its context settings and the off-host allowlist (hosts and URL prefixes);
- the widths in `Viewports` and where each comes from;
- the site-wide header and footer decision, and where the browser host seeds them;
- the visual helper path and baseline folder;
- CI changes (browser install, artifacts) and anything changed in a generated template;
- validation results.
