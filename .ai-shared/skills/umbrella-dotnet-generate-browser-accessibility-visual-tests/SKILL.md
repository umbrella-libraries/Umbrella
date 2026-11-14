---
name: umbrella-dotnet-generate-browser-accessibility-visual-tests
description: 'Generate Playwright browser tests for one page of an ASP.NET Core or Blazor site: a guarded page load, an axe accessibility gate that fails on serious and critical violations or, for sites with existing violations, on any not in an approved baseline of known violations, and a masked, animation-free visual regression test with per-OS baselines and a reviewed approval workflow. Use after umbrella-dotnet-scaffold-browser-test-host has set up the browser test project.'
---

# Generate Browser Accessibility and Visual Tests

## Purpose

Add browser tests for **one page**, or for every page type when the user asks for that (see "Covering many page types"): an accessibility gate and a visual regression test, both run in Chromium against the real site served by Kestrel. This skill adds tests only. The browser test project, the Kestrel host, the `PageTest` base class and the visual snapshot helper are set up once by `umbrella-dotnet-scaffold-browser-test-host`.

## Preflight

Check the setup exists before writing anything. Look for:

1. A runnable test project referencing `Microsoft.Playwright.Xunit.v3` and `Deque.AxeCore.Playwright`.
2. A factory serving the site with Kestrel on a loopback port (it exposes a server address, usually derived from the repo's `UmbrellaWebApplicationFactory<TProgram>`-based factory), registered as an assembly fixture.
3. An abstract class deriving from Playwright's `PageTest` that sets fixed context options (base URL, viewport, device scale factor, locale, time zone, reduced motion) and aborts off-host requests except to an allowlist of hosts the user chose to keep.
4. A visual snapshot helper that compares a screenshot with a per-OS baseline, within a small per-channel tolerance, and writes received, expected and diff images to an artifacts folder, with a `CaptureFullPageAsync` that crops full-page screenshots to the window's width.
5. A `Viewports` helper with the widths the layout changes at (`umbrella-dotnet-scaffold-browser-test-host`, `references/viewports.md`). If it or `CaptureFullPageAsync` is missing, add them from that skill's references before writing tests.
6. In **baseline mode** (Step 4), an accessibility baseline helper beside it. If the user chooses baseline mode and it is missing, create it from `references/accessibility-baseline.md`.

If any of these is missing, stop and run `umbrella-dotnet-scaffold-browser-test-host` first. Do not create a second base class, host or snapshot helper beside existing ones.

Then read the repo's test README (for example `Test\README.md`) for the map of helpers, seeders and collections, and read one existing browser test to mirror its naming, namespaces and attributes (for example the Docker-gated `[IndyRecordsFact]` attribute when the host needs containers).

## Step 1 -- Arrange the page

Decide how the page gets its content:

- **Seeded data**: reuse the repo's seeder (a class the factory calls after the host starts, or a scoped `DbContext` helper). Add the records this page needs to the shared seed when they are read-only; tests that change data go in the repo's non-parallel mutation collection.
- **Static or route-only pages**: no seed is needed; resolve the URL the way the app does (a route constant, a URL helper or a resolver service from the host's services), not a hard-coded guess.

If the page depends on an external service (a search API, a video platform, a payment widget), list each dependency with a recommendation (real service, emulator, inert configuration, stub) and the reason, and **ask the user what to do for each one**. Never stub a dependency without confirmation. This covers both sides: calls the server makes, and resources the browser loads from other hosts. The base class only lets through the hosts the user chose to keep when the browser host was scaffolded; if this page loads an off-host resource that was not decided then (a new embed, font or script), list it with a recommendation and ask before blocking it, allowing it or replacing it with a placeholder.

## Step 2 -- Guard the page load

Write one private navigation helper shared by both tests. It must prove this is the right page, because an error page can also be accessible and can match a baseline approved by mistake:

```csharp
private async Task GotoTrackListingAsync(string viewport)
{
	// Before navigating: pages choose a layout, image sources and carousel slide counts as they load.
	await Viewports.SetAsync(Page, viewport);

	IResponse? response = await Page.GotoAsync(TrackListingPath);

	Assert.NotNull(response);
	Assert.Equal(200, response.Status);
	await Expect(Page.Locator("table.track-listing[data-album-id]")).ToBeVisibleAsync();
}
```

Choose a page-specific locator: an element only this page renders, ideally with a distinguishing attribute. A generic heading or the layout's footer is not enough.

- Don't use an iframe as the locator: a third-party frame the base class blocks can be 0px high, and a lazy or hidden frame (a map) isn't visible. Use an element of the app's own markup next to it.
- When the locator can match several elements, use `.First`, because `Expect` is strict.
- Choose an element with visible content. A wrapper around an empty region (a seeded page with no blocks in a content area) has no size, so a visibility check never passes.
- Pass the expected status per page rather than hard-coding 200. Open a not-found page at a missing URL and expect 404.

### Blazor components rendered on the server first

WebAssembly renders a prerendered component again and replaces its elements, so axe or a screenshot taken too early sees elements that are about to disappear. Reuse the repo's helper for this if one exists (the journey tests use the same wait); otherwise create one in the shared folder:

1. Before navigating, add an init script that marks the page's elements (for example `main *`) with `data-prerendered` on `DOMContentLoaded` (`_ = await Page.AddInitScriptAsync(...)`).
2. After navigating, wait for `document.readyState !== 'loading'`, then for `<locator>:not([data-prerendered])` with a generous timeout (60 seconds on a slow CI agent), then for network idle.
3. Run axe and take the screenshot only after that.

## Step 3 -- Replace heavy embedded content

When the page embeds heavy or third-party content (a PDF in an iframe, a video player, a map), recommend replacing it with placeholder HTML so the checks cover the app's markup and styles, not the embedded viewer, and **ask the user first**: a placeholder hides the embed's own accessibility problems and visual changes. Once agreed, fulfil that request through a page route in `InitializeAsync`:

```csharp
public override async ValueTask InitializeAsync()
{
	await base.InitializeAsync();

	_ = await Page.RouteAsync("**/media/liner-notes.pdf", route => route.FulfillAsync(new()
	{
		ContentType = "text/html",
		Body = "<!doctype html><title>Liner notes</title><p>Liner notes placeholder</p>",
	}));
}
```

Keep the embedding element itself (the `iframe`, its `title`, its size) so axe still checks it. Record each placeholder and the user's agreement in a comment.

## Step 4 -- Accessibility gate

Mark every test with the repo's Docker-gated attributes (`[IndyRecordsFact]` and `[IndyRecordsTheory]` here), not plain `[Fact]` or `[Theory]`: without Docker the host never starts and `ServerAddress` is null, so a plain `[Fact]` fails instead of skipping.

The gate counts `serious` and `critical` violations. Choose its mode once per repo and record it in the test README; if the README doesn't say, **ask the user**:

- **Strict mode** fails on any serious or critical violation. Use it when the site is expected to be clean.
- **Baseline mode** fails only on a violation that isn't in the page's approved baseline of known violations. Use it when the site has existing violations that are out of scope (for example during a framework upgrade), so a change can't make accessibility worse while the backlog waits.

**Strict mode.** List each violation's impact, rule id, help text, help URL and targets in the failure message, so the report is actionable without rerunning:

```csharp
private static readonly string[] _blockingImpacts = ["serious", "critical"];

[IndyRecordsFact]
public async Task TrackListingPage_HasNoSeriousAccessibilityViolations()
{
	await GotoTrackListingAsync(Viewports.Desktop);

	AxeResult result = await Page.RunAxe();

	AxeResultItem[] blocking = [.. result.Violations.Where(x => _blockingImpacts.Contains(x.Impact))];
	Assert.True(blocking.Length is 0, string.Join(Environment.NewLine, blocking.Select(x =>
		$"{x.Impact}: {x.Id} - {x.Help} {x.HelpUrl}{Environment.NewLine}    {string.Join(", ", x.Nodes.Select(n => n.Target.ToString()))}")));
}
```

**Baseline mode.** Reuse the repo's baseline helper (create it from `references/accessibility-baseline.md` if missing):

```csharp
public static TheoryData<string> AccessibilityViewports => [.. Viewports.Accessibility];

[IndyRecordsTheory]
[MemberData(nameof(AccessibilityViewports))]
public async Task TrackListingPage_HasNoNewAccessibilityViolations(string viewport)
{
	await GotoTrackListingAsync(viewport);

	AxeResult result = await Page.RunAxe();

	await AccessibilityBaseline.AssertNoNewViolationsAsync(result, $"track-listing-page.{viewport}");
}
```

Run the gate at `Viewports.Accessibility`, the narrowest and widest layouts, where the DOM and styles differ most (the phone menu, stacked columns, elements shown at one width only). Each width has its own baseline (`track-listing-page.phone.json`). The strict-mode test takes the same theory data.

- Each known violation is stored as rule id plus element selector in `Accessibility\Baselines\<name>.json`, with scoped-CSS attributes and generated IDs normalised so builds don't churn it. Axe evaluates the DOM, not pixels, so one baseline serves every OS.
- Axe reports colour contrast over background images and gradients as *incomplete* (needs review), not as a violation, so a baseline never records it. Look at a screenshot of each page, not only the visual baseline pages, for text on gradients and images, and log anything hard to read in the known-issues backlog with the contrast measured from the screenshot.
- The first run fails with a missing baseline and writes `<name>.received.json` to the artifacts folder. Review every entry with the user before approving it (Step 6): the baseline is the accessibility backlog, so each entry is a known defect, not noise.
- A violation that is no longer found is reported in the test output as fixed; approve the received file to drop it. In a theory, look for that note in the whole run's output, because it can appear under another row.
- Never add a violation the change under test introduced to the baseline to get a green run.

In either mode, when the gate fails on a new defect (missing labels, low contrast, invalid ARIA, missing landmarks), **report the violations to the user and ask before fixing them in the app**. Fix the app's markup or styles; do not exclude rules, disable checks or narrow the scanned region to make the test pass. Only exclude a rule or region when the user confirms the violation belongs to content the app does not control, and record why in a comment.

## Step 5 -- Visual regression test

Take a full-page screenshot with animations disabled and the caret hidden, masking content that legitimately changes between runs (copyright years, dates, relative times, random or rotating content, counters):

```csharp
public static TheoryData<string> AllViewports => [.. Viewports.All];

[IndyRecordsTheory]
[MemberData(nameof(AllViewports))]
public async Task TrackListingPage_MatchesVisualBaseline(string viewport)
{
	await GotoTrackListingAsync(viewport);
	await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
	_ = await Page.EvaluateAsync("async () => { await document.fonts.ready; }");

	// The footer shows the current year, so it is masked rather than failing the baseline every January.
	byte[] screenshot = await VisualSnapshot.CaptureFullPageAsync(Page, Page.Locator(".footer__copyright"));

	await VisualSnapshot.AssertMatchesAsync(screenshot, $"track-listing-page.{viewport}");
}
```

Rules:

- Use a kebab-case snapshot name unique in the project, followed by the width's name. The helper appends the platform (`track-listing-page.phone.windows.png`, `track-listing-page.phone.linux.png`).
- Take a baseline at every width in `Viewports.All`: layouts change at each breakpoint, and a defect often shows at one width only.
- Mask by the narrowest locator that covers the dynamic content; never mask the whole page or large layout regions. Mask embedded third-party frames whose content the tests can't control (a marketing form's iframe) too.
- Rely on the base class's fixed scale factor, locale, time zone and reduced motion, and take widths only from `Viewports`. Do not set them per test otherwise.
- Wait for a specific state (network idle, a loaded image, a rendered element), never a fixed delay.
- Wait for web fonts before the screenshot: `_ = await Page.EvaluateAsync("async () => { await document.fonts.ready; }");`.
- Wait for the images in the first screen too (each `img` in the viewport is `complete` with a `naturalWidth`). A lazily loaded image, such as a header logo, can otherwise be missing from the screenshot.

## Step 6 -- Approve the baselines

The first run fails with a missing baseline and writes `<name>.<viewport>.<platform>.received.png` to the artifacts folder (for example `bin\Debug\<tfm>\visual-artifacts`). A changed page writes `received`, `expected` and `diff` images. Baselines are never updated automatically.

Approval workflow, to describe to the user rather than perform silently:

1. Review the received image against the expected image and the diff. Confirm every difference is intended.
2. Copy the received image over the baseline in the source tree (for example `Visual\Snapshots\<name>.<platform>.png`) and commit it as a normal, reviewed change.
3. Each OS has its own baseline, because fonts render differently. The local baseline (`windows` or `macos`) comes from a local run.
4. The `linux` baseline is the one CI checks. Take it from the CI run's published test artifacts (for example `test-artifacts`), not from WSL or a local container, because the agent's fonts are what CI compares against. Push the test, let CI fail, download the artifact, review, then copy.

Do not copy a received image you have not looked at, and do not commit a baseline of an error page.

### Reviewing a shared change

A change to something every page shows (the header, the footer, a shared stylesheet, the browser version) changes most baselines at once. Review it as a whole, then in detail:

- Run the whole suite once to collect every received file, and confirm only baseline tests failed.
- For screenshots, compare each pair with a script that finds the vertical offset at which the old image's rows reappear in the new one, so you can see which band changed. Then look at pairs yourself at every width, including those whose rows matched least (a background gradient that stretches with the page defeats row matching), and confirm the page bodies are unchanged.
- For accessibility baselines, element selectors change when the markup around an element changes, so a file diff overstates the change. Compare the count of each rule per file instead, and for each rule whose count changed, run axe on one affected page and read each node's HTML and message (for colour contrast, the colours and the ratio).
- Axe reports colour contrast it can't measure (text over an image or gradient, or under an overlapping element) as incomplete. Moving an overlapping element, such as a sticky header, can turn those into violations that were always there. Approve them as existing defects, and say so in the report.
- Log any new defect the change shows in the known-issues backlog before approving.

Accessibility baselines (baseline mode) follow the same review: compare `<name>.<viewport>.received.json` with the baseline, confirm each added entry is existing behaviour and each removed one is really fixed, then copy it to `Accessibility\Baselines\<name>.json`. They aren't per OS, so a local run is enough.

## Covering many page types

When the user wants every page type covered, don't write a class per page. Write one class with:

- a private table, keyed by page `Type` (rows serialise and show in results), saying for each page type how to open it: the seeded page, any stub arrangement, a path suffix, the expected status, the page-specific locator, the expected `h1`, and whether it is a Blazor page;
- one accessibility theory over every row and `Viewports.Accessibility`, each with its own baseline name;
- one visual theory over a few representative templates (home, a content page, a lister, a details page, a landing page) at every width, to keep baseline upkeep small. When the user wants full visual coverage, cover every row at every width instead, and tell them the number of baselines first (rows x widths x platforms);
- the shared guarded navigation helper for both.

Skip only what isn't a page (settings content types, for example) and pages another class already covers, and say why in the report.

When the app serves another site on its own host name (for example `portal.localhost` on the same port), allow that host in the base class's off-host allowlist, add a helper that builds an absolute URL on it, and give the table a column for the host. Seed what that site's layout needs (logos, a page of each type) so the baselines don't record gaps that exist only in the test seed.

## Site-wide header, menu and footer

Page baselines show the header and footer closed and at rest, so they never check a menu's panels, the phone menu or a language list. When the user wants these covered, write separate classes for them (for example in `Navigation\`):

- **Content.** One class fills the header and footer as an editor of the live site would: every panel layout the menu's view supports, optional fields chosen pairwise (description, call to action, style, link target), long titles, nested items, and every footer field. The browser host shows the same content on every page (`umbrella-dotnet-scaffold-browser-test-host`, Preflight step 9).
- **Changes.** A test that needs other content (an optional field switched off, a minimal header) publishes a changed copy and restores the original afterwards, in the non-parallel mutation collection. Where publishing is slow, publish once per class through a class fixture. Clear any cache holding the rendered header (a `<cache>` tag helper's `MemoryCache`, the output cache) before and after each change.
- **Visual.** Screenshot the component only (`Page.Locator(...).ScreenshotAsync`, or a clip to the header, the open panel or the footer), so a change elsewhere on the page doesn't fail them: the header closed at every width; each desktop panel open, and drilled into its nested items; the phone menu at its top level and in each panel, at the widths that show it; the search box and language list open; scrolled, when the header is sticky; and the footer over each background the layouts give it.
- **Accessibility.** Run axe on the component alone (`await Page.Locator(".site-header").RunAxe()`) with each panel or menu level open, at the phone and desktop widths, against its own baselines; the page baselines cover the rest of the page.
- **Behaviour.** Opening and closing with the pointer and the keyboard (Enter, Space, Escape and the Tab order); resizing across the width where the desktop menu becomes the phone menu, both ways; links going where the editor pointed them, with their targets; the search box submitting; any analytics events the script pushes.
- Read the menu's view, script and stylesheet first: the widths where behaviour switches, what is hidden with `hidden` or `aria-hidden`, and what the stylesheet overrides (a `display: ... !important` on a `[hidden]` element leaves it in the tab order).

## Rules

- One page per invocation, or one table of page types when the user asked for every page type; test classes derive from the repo's `PageTest` base class.
- Both tests share the guarded navigation helper (status code plus page-specific locator).
- Keep Moq out of the browser project; tests run against real services.
- Never add fixed delays, `Thread.Sleep` or `WaitForTimeoutAsync`.
- Never suppress axe rules or mask regions to get a green run without the user's agreement.
- In baseline mode, never baseline a violation the current change introduced.
- Log defects you find that are out of scope (other than accessibility violations, which the baseline records) in the repo's known-issues backlog (see `Test\README.md`), and assert the current behaviour with the entry's ID in a comment, rather than fixing them silently.
- Baselines are reviewed source files: never regenerate or overwrite them in test code.
- Ask the user before stubbing any external dependency, or blocking or replacing an off-host browser resource with a placeholder; prefer the real service or an emulator. Where a stub is chosen, `umbrella-dotnet-scaffold-live-service-tests` can verify the real service separately.

## Validation

```powershell
dotnet build "<BrowserTestProject>"
dotnet test "<BrowserTestProject>" --no-build --filter-class "IndyRecords.Web.Browser.Test.Pages.TrackListingPageBrowserTests"
```

xUnit v3 on Microsoft Testing Platform accepts `--filter-class`; confirm the options with `--help`. A method filter such as `--filter-method "*Page_HasNoNewAccessibilityViolations"` also matches other classes' methods with that suffix, so use `--filter-class` for one class. On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<BrowserTestProject>" --no-build -- --filter-class ...`. Chromium must be installed once with the project's generated `playwright.ps1 install chromium`. Expect the visual test to fail on its first run until the baseline is approved; the accessibility test must pass. Confirm the guard works by temporarily pointing the helper at a non-existent path and checking both tests fail, then revert.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report:

- test class path and the page covered;
- how the page is arranged (seed, route) and the page-specific locator used;
- external dependencies, the user's choice for each, and any routed placeholders;
- the gate mode, axe violations found, baselined entries approved, and the app fixes made with the user's agreement;
- the widths covered by each test, and masked regions and why;
- baselines written or still awaiting approval (local and Linux), with the artifact paths;
- build and test results.
