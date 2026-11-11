---
name: umbrella-dotnet-generate-e2e-journey-tests
description: 'Generate one Playwright end-to-end journey test that follows a visitor across server-rendered pages and Blazor WebAssembly components, waiting for WebAssembly to replace prerendered elements, asserting on the API calls the browser makes, browser errors, server error logs and (when a stub was chosen) what reached the downstream dependency, plus a break check. Use after umbrella-dotnet-scaffold-browser-test-host.'
---

# Generate End-to-End Journey Tests

## Purpose

Add **one user journey** as a browser test: a visitor moving through several pages, with server-rendered HTML first and Blazor WebAssembly taking over in the browser. The journey runs every layer together (routing, prerendering, WebAssembly, the browser calling the app's API, and the API calling its dependencies), so it catches faults no single-layer test sees.

This skill adds tests only. The browser test project, Kestrel host and `PageTest` base class come from `umbrella-dotnet-scaffold-browser-test-host`.

## Preflight

1. Check the setup exists:
   - a test project referencing `Microsoft.Playwright.Xunit.v3`;
   - a Kestrel-hosted factory registered as an assembly fixture, exposing the server address and host services;
   - an abstract class deriving from Playwright's `PageTest` with fixed context options and an off-host allowlist (the site plus the hosts the user chose to keep; everything else aborted);
   - an `ILoggerProvider` that records Error-level entries the host logs (often exposed as `factory.Errors`).

   If the host or base class is missing, stop and run `umbrella-dotnet-scaffold-browser-test-host`. If only the error recorder is missing, add it to the shared host (sketch below) rather than to the test.
2. Read the repo's test README (for example `Test\README.md`) for the helper map, seeders, stubs and collection names. Read an existing journey test, if any, and mirror it.
3. Agree the journey with the user in one sentence: the starting page, the actions, and the observable outcome (for example "a visitor searches the catalogue from the header, then filters by format").

## Step 1 -- Decide each external dependency

List every external service the journey reaches server-side, with a recommendation and reason:

| Option | When |
| --- | --- |
| Real service | It has a reliable test environment with stable data and no side effects |
| Emulator or container | One exists (Azurite, SQL Server via Testcontainers) |
| Inert configuration | The journey never needs it to respond |
| Existing or extended stub | No reliable test environment, rate limits, non-deterministic data, side effects or cost |

Do the same for any off-host resource the journey's pages load in the browser (an embed, a widget script, a font) that the browser host's allowlist does not already cover: recommend keeping it real, serving it locally, or blocking it, with the reason.

A test class that opens a page whose server-rendered component calls a stubbed service must arrange the stub even when the journey doesn't search yet: if the call fails while the server renders the page, the page may never respond.

**Ask the user explicitly what to do for each one.** Never stub a dependency, or block a browser resource, without confirmation. If a stub is chosen and none exists, run `umbrella-dotnet-scaffold-wiremock-stub` first; if one exists, reuse it and add only the arrangement this journey needs. Note that `umbrella-dotnet-scaffold-live-service-tests` can verify the real service separately.

## Step 2 -- Test class shape

```csharp
[Collection(CatalogueStubCollection.Name)]
public sealed partial class CatalogueSearchJourneyTests : IndyRecordsPageTest
{
	private readonly ConcurrentQueue<string> _browserErrors = new();

	public CatalogueSearchJourneyTests(IndyRecordsBrowserWebApplicationFactory factory)
		: base(factory)
	{
	}

	[IndyRecordsFact]
	public async Task SearchFromTheHeader_ThenFilterByFormat_ShowsOnlyVinyl()
	{
		// Steps 3 to 7.
	}
}
```

- When several pages share one journey (listers of different content types, for example), write an abstract base class holding the `[Fact]` and the journey, and one small class per page that supplies the page, its controls and its expected query. Don't copy the journey into each class.
- Use the repo's Docker-gated attribute (`[IndyRecordsFact]` here), not plain `[Fact]`: without Docker the host never starts and `ServerAddress` is null, so a plain `[Fact]` fails instead of skipping.
- When the journey shares a stub (or any shared mutable host state) with other tests, put it in the stub's **non-parallel** collection (`DisableParallelization = true`), and reset the stub and the error recorder in `InitializeAsync`.
- A journey that publishes or changes content (a menu, site settings) goes in the repo's content mutation collection and restores what it changed in `finally` or `DisposeAsync`. Prefer target pages that call no stubbed service, so the collection doesn't also need the stub's. Non-parallel collections run one after another, after the parallel ones.
- xUnit only honours a `[CollectionDefinition]` declared in the test assembly itself. If the collection's name constant lives in a shared testing library, re-declare the definition in this test project.

## Step 3 -- Wait for WebAssembly with the prerender marker

Blazor does not take over prerendered markup. Once WebAssembly starts it renders each component again and **replaces** the prerendered elements; only the new elements have event handlers. Clicking a prerendered element can do nothing, or be lost when it is replaced.

Do not use a fixed delay (it is flaky on a cold CI agent and slow everywhere), and do not hook `Blazor.start`: with autostart the page assigns `Blazor.start` after `window.Blazor` exists, so an init script cannot reliably wrap it, and its promise resolving does not mean a given component has re-rendered.

Instead, mark the server-rendered elements before Blazor starts, then wait for an element **without** the marker:

```csharp
// Marks the inputs as the server rendered them; WebAssembly's replacements don't carry the attribute.
private const string MarkPrerenderedInputs = """
	document.addEventListener("DOMContentLoaded", () =>
		document.querySelectorAll("input").forEach(x => x.dataset.prerendered = "true"));
	""";

public override async ValueTask InitializeAsync()
{
	await base.InitializeAsync();

	Factory.CatalogueApi.Reset();
	Factory.Errors.Clear();

	_ = await Page.AddInitScriptAsync(MarkPrerenderedInputs);
}
```

In the test, after the prerendered page has been checked, **wait for the document to finish parsing first**, then for an element without the marker:

```csharp
// The marker is set at DOMContentLoaded. On a slow agent the elements are parsed and visible well before that, while
// deferred scripts run, and until then a prerendered element has no marker and passes for a WebAssembly one.
_ = await Page.WaitForFunctionAsync("() => document.readyState !== 'loading'", null, new() { Timeout = 60_000 });

ILocator vinyl = Page.GetByLabel("Vinyl", new() { Exact = true });

// Starting WebAssembly takes a few seconds, longer on a cold CI agent.
await Expect(vinyl.And(Page.Locator("input:not([data-prerendered])"))).ToBeEnabledAsync(new() { Timeout = 60_000 });
```

Without the `readyState` wait the test is flaky only on slow machines: the check can pass during the gap between parsing and `DOMContentLoaded`, and the click lands on the prerendered element with no handler.

- Wait with `ToBeEnabledAsync`, not `ToBeVisibleAsync`. Styled checkboxes and radios are often `visibility: hidden`, so a visibility wait never resolves.
- Widen the init script's selector (`button`, `select`, `[data-component]`, or `main *`) to whatever the journey interacts with. Keep the 60 second timeout on this one wait only.
- When two or more classes need the marker (journeys, page tests), put it in one shared helper in the browser project's shared folder rather than copying the script: a method that adds the init script, and one that waits for `readyState` and returns `element.And(page.Locator(":not([data-prerendered])"))`.

To prove a journey is robust, run it under Chromium CPU throttling several times in one host before relying on it in CI. Temporarily add this to the base class's `InitializeAsync`, run the journeys about 10 times, then remove it:

```csharp
ICDPSession cdp = await Page.Context.NewCDPSessionAsync(Page);
_ = await cdp.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 6 });
```

## Step 4 -- Act as a visitor

- Fill and press keys on real inputs: `FillAsync`, `PressAsync("Enter")`.
- Prefer role and label locators (`GetByRole`, `GetByLabel`) over CSS. If a role-and-name locator finds nothing, check the element's accessible name (`ToHaveAccessibleNameAsync`) before falling back to CSS: an icon button whose text is hidden often gets an icon font's private-use glyph as its name. Assert that name, log it as a known issue, and locate the button by a stable attribute. Playwright runs a .NET `Regex` passed as a name in the browser's JavaScript engine, so Unicode classes such as `\p{Co}` don't work there; use explicit ranges such as `[\uE000-\uF8FF]`.
- Read link or menu text with `textContent` (`TextContentAsync`), not `innerText`, inside a panel that may still be opening: `innerText` depends on layout and returns nothing for an element not yet rendered visibly. Check the panel is open first.
- When a styled control hides the native input (a custom checkbox or radio), **click the label**, as a mouse user does. Do not force-click the hidden input or dispatch events with JavaScript. Also check the control is reachable without a mouse: `Expect(Page.GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(n)` and a focus check. A native input hidden with `visibility: hidden` or `display: none` locks out keyboard and screen reader users; assert the current behaviour and log it as a known issue.
- To check a control's state while a request is in flight (disabled while loading, a spinner), hold the response with a route and two `SemaphoreSlim`s, assert, then release it. Release in `finally` too, so a failed assertion doesn't leave the route waiting:

```csharp
using SemaphoreSlim requested = new(0);
using SemaphoreSlim answer = new(0);
bool answered = false;

_ = await Page.RouteAsync(IsSearch, async route =>
{
	_ = requested.Release();
	await answer.WaitAsync(CancellationToken);
	await route.ContinueAsync();
});

try
{
	await vinylLabel.ClickAsync();
	Assert.True(await requested.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken), "The browser never called the search API.");

	await Expect(cdCheckbox).ToBeDisabledAsync();

	_ = answer.Release();
	answered = true;
}
finally
{
	if (!answered)
		_ = answer.Release();

	await Page.UnrouteAsync(IsSearch);
}
```

  Use semaphores rather than awaiting a `TaskCompletionSource.Task`, which raises VSTHRD003. `RouteAsync` needs a discard (IDE0058); `UnrouteAsync` returns no value, so a discard there is CS8209.
- Assert the prerendered state before WebAssembly loads, when the page prerenders (the results show without WebAssembly), then the interactive state after.

## Step 5 -- Assert on the API call

Wrap the action that triggers the browser's API call in `RunAndWaitForResponseAsync`, then assert on the status and on what the client sent in the query string:

```csharp
IResponse filtered = await Page.RunAndWaitForResponseAsync(
	() => Page.Locator("label.u-checkbox__label", new() { HasText = "Vinyl" }).ClickAsync(),
	response => response.Url.Contains("/api/catalogue/search", StringComparison.OrdinalIgnoreCase));

Assert.Equal(200, filtered.Status);

var query = QueryHelpers.ParseQuery(new Uri(filtered.Url).Query);
Assert.Equal("vinyl", query["format"].ToString()); // ParseQuery returns StringValues
```

Then assert the page updated (`ToHaveTextAsync`, `ToBeCheckedAsync`, `ToHaveURLAsync`).

Check whether the address bar should change after filtering or paging, and compare it with the page's canonical link: they can disagree (the canonical link moves to `?page=2` while the address bar stays put, so reloading, Back and shared links lose the state). Assert what happens and log a mismatch as a known issue.

Use readable lookups (`query.GetValueOrDefault("format")`, `Assert.Contains`) rather than indexers, so a missing parameter fails with Expected and Actual values instead of a `KeyNotFoundException`.

## Step 6 -- Assert on the downstream request (only when a stub was chosen)

When the user chose a stub, the stub's results may not vary with the request, so assert on **what reached the dependency** from the stub's request log rather than expecting different results. Clear the log just before the action so earlier page loads don't count:

```csharp
Factory.CatalogueApi.ClearRequests();

// ... the filtering action ...

StubRequest request = Assert.Single(Factory.CatalogueApi.Requests, x => x.Path == "/v1/releases");
Assert.Equal("vinyl", request.Query["format"]);
```

`Requests` and `ClearRequests()` are the stub's request log and log reset from `umbrella-dotnet-scaffold-wiremock-stub`; reuse the names the repo's stub already has.

If the journey must show the results changing, add a second arrangement for the filtered or paged request at a higher priority (WireMock `AtPriority(-1)`), answering with part of a recorded fixture. Match the request body after parsing it (a `WithBody((string? body) => ...)` predicate that deserialises the JSON and checks the query text), not with a `WildcardMatcher` on the raw body: text containing quotes is escaped (`\"`) in a JSON body, so a raw match silently falls through to the general arrangement.

Skip this step for real services and emulators; assert on their observable state instead (a blob written, a row saved).

## Step 7 -- Collect browser and server errors

Subscribe in `InitializeAsync`. Ignore only the noise the test setup causes: requests to off-host resources the user chose to block are aborted by the base class (reported as failed resources), and the site's Content Security Policy may block other-host resources first. Anything else, including a failure of an allowed host, is a fault to report:

```csharp
Page.Console += (_, message) =>
{
	if (message.Type == "error"
		&& !message.Text.StartsWith("Failed to load resource", StringComparison.Ordinal)
		&& !IsBlockedByPolicyFromAnotherHost(message.Text))
		_browserErrors.Enqueue(message.Text);
};
Page.PageError += (_, error) => _browserErrors.Enqueue(error);
```

`IsBlockedByPolicyFromAnotherHost` matches the CSP console message (`Loading the ... '<url>' violates the following Content Security Policy directive`) with a `[GeneratedRegex]` and returns true only when the URL's authority differs from the page's. A CSP block for the site's own host is a real fault and must fail.

Put this collector, with the policy filter, in one shared class in the browser project's shared folder (for example `BrowserErrors`, attached to the page in `InitializeAsync`) rather than a handler per test class.

End the test with both assertions, listing the errors:

```csharp
Assert.True(_browserErrors.IsEmpty, $"The browser reported errors:{Environment.NewLine}{string.Join(Environment.NewLine, _browserErrors)}");
Assert.True(Factory.Errors.Logged.Count is 0, $"The site logged errors:{Environment.NewLine}{string.Join(Environment.NewLine, Factory.Errors.Logged)}");
```

The server assertion matters because many services log and swallow failures behind a `200`.

If the host has no error recorder, add one to the shared factory's logging: an `ILoggerProvider` whose loggers are enabled for `LogLevel.Error` and above and enqueue `"{category}: {message or exception type and message}"` into a `ConcurrentQueue<string>`, with `Logged` and `Clear()`.

## Keyboard and ARIA journeys

For navigation such as a mega menu or disclosure, add a keyboard journey beside the mouse one:

- Move focus with Tab in a bounded loop (for example at most 50 presses) until `document.activeElement` is the target, recording each stop, so a missing target fails with the list of stops rather than hanging.
- Assert `aria-expanded` on the trigger, `ToBeFocusedAsync` after each key, what Escape does, and that only one panel is open.
- Playwright treats an `opacity: 0` element as visible, so `ToBeVisibleAsync` and `ToBeHiddenAsync` are wrong for fading panels. Assert the `hidden` attribute, `ToHaveCSSAsync("opacity", "1")` and `ToBeInViewportAsync` instead.
- Log invisible focus stops (links in a closed panel that stay in the tab order) and a focus order that leaves an open panel behind as known issues, asserting the current behaviour.

## Step 8 -- Break check

Prove the test can fail. Make one deliberate change, run the test, confirm it fails with a useful message, then revert:

- remove or change the stub arrangement (the journey should fail on results or the downstream assertion);
- or change the behaviour under test (drop the filter from the client's API call, rename the query parameter);
- or point a real-service setting at an unreachable address and confirm the server-error assertion reports it.

Make each break safely: back up the file to a uniquely named scratch folder, mutate it, build, restore it straight after the build (compare hashes to prove it is byte-identical), touch it, run the test against the mutated build, then rebuild. If a front-end watcher is running, it rebuilds on save, so a script mutation must stay in place for the run; then restore it and wait for the bundle manifest to change. Bundle content hashes may differ from the earlier build even though the source matches.

Report which break you used and the failure message.

## Known issues

When the journey exposes behaviour the user confirms is a defect but out of scope (a control enabled while loading, a console error, a wrong URL after navigation), log it in the repo's known-issues backlog (see `Test\README.md`) with the next ID, assert the **current** behaviour, and put the entry's ID in a comment on that assertion, so a later fix or regression is noticed. Skip a test with the ID as its reason only when the current behaviour can't be asserted reliably. Never fix it silently, and never weaken an assertion to hide it.

## Rules

- One journey per invocation, or one shared journey run by a small class per page, named for the visitor's goal and outcome: `<Action>_Then<Action>_<Outcome>`.
- No fixed delays, `WaitForTimeoutAsync` or `Blazor.start` hooks; wait on the prerender marker or a specific signal.
- Interact only after WebAssembly has replaced the element.
- Keep Moq out of the browser project.
- Ask before stubbing any dependency; reset shared stubs and error recorders in `InitializeAsync`; use the stub's non-parallel collection.
- Linux CI is case-sensitive: match file, route and asset casing exactly.

## Validation

```powershell
dotnet build "<BrowserTestProject>"
dotnet test "<BrowserTestProject>" --no-build --filter-class "IndyRecords.Web.Browser.Test.Journeys.CatalogueSearchJourneyTests"
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<BrowserTestProject>" --no-build -- --filter-class ...`. Confirm the filter options with `--help`. `--filter-class` can be repeated to run several classes, and its wildcard works only at the start or end of the name.

Run the test at least twice to check it is stable, then do the break check. Docker must be running if the host uses containers.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report:

- the journey (pages, actions, outcome) and the test path;
- each external dependency and the user's choice (real, emulator, inert, stub);
- the prerender marker selector and the element waited for;
- API and downstream assertions made;
- browser-error filters applied;
- collection used and why;
- break check performed and its failure message;
- build and test results.
