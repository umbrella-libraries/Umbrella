---
name: umbrella-blazor-generate-bunit-component-tests
description: 'Generate bUnit tests for one Blazor component: choose the component with the user, render it with the parameters its real parents pass and its real services, set up strict JavaScript interop, drive it through InvokeAsync and DOM events, query by label, text and ARIA, assert on raised EventCallbacks, parameter objects and interop calls, and prove the tests can fail. Use after umbrella-blazor-scaffold-bunit-test-project has created the bUnit test project.'
---

# Generate bUnit Component Tests

## Purpose

Add in-memory tests for **one Blazor component** with bUnit. The component runs with its real services; bUnit stands in only for the browser (JavaScript interop and navigation). These tests are fast and need no Docker or browser.

This skill adds tests only. The bUnit test project is created once by `umbrella-blazor-scaffold-bunit-test-project`.

## Preflight

1. Check the setup exists: a runnable test project using `Microsoft.NET.Sdk.Razor`, referencing `bunit` and the Blazor client project, with Moq removed (`<PackageReference Remove="Moq" />` when the repo injects it centrally). If it is missing, stop and run `umbrella-blazor-scaffold-bunit-test-project`.
2. Check the bUnit major version in the project or `Directory.Packages.props`. bUnit 2 uses `BunitContext` and `Render<T>`; bUnit 1 uses `TestContext` and `RenderComponent<T>`. Use the installed version's API.
3. Read the repo's test README (for example `Test\README.md`) and an existing component test, if any, to mirror naming, folders (mirror the component's folder under `Components\`) and shared service registration helpers.
4. Build the test project and run its existing tests as a baseline. Record the counts; if anything already fails, report it before adding tests, so new failures are not confused with old ones.

## Step 1 -- Choose the component

If the user named a component, use it. If the user approved a batch (for example every component that renders without the server), enumerate every `.razor` file, decide in or out of scope for each with a reason, and apply the steps below per component. Otherwise propose two to four candidates with a one-line reason each, preferring components that are interactive (parameters, callbacks, user actions that change the markup) and have no server dependency (no HTTP data service, authentication state or prerendered state). Ask the user to pick one.

## Step 2 -- Read the component, its children and its parents

- **The component**: its `[Parameter]`s, `EventCallback`s, `@typeparam`, injected services, `IJSRuntime` calls (directly or through an interop service), public methods called by parent components, and the markup it renders.
- **Its children**: read every component it renders, including ones from NuGet packages. The installed package version is what runs, and it may differ from any local source or documentation, so confirm the real markup: add a temporary probe test that renders the component and writes `cut.Markup` to the test output, run it, read the markup, then delete the probe.
- **Its parents**: find where the app uses it. Learn the parameter values and generic type they really pass, and how they read results back (an `EventCallback`, a parameter object the component mutates, a public method called through `@ref`). The tests should use the component the same way.

## Step 3 -- Decide whether bUnit fits

bUnit fits components whose behaviour is visible in their own markup, parameters, callbacks and interop calls. Prefer an end-to-end journey (`umbrella-dotnet-generate-e2e-journey-tests`) when the component:

- injects an HTTP data service or derives from a deep base class that loads data from the server or authentication state, so a bUnit test would mostly be arranging fakes;
- opens dialogs, modals or toasts through a service that renders outside the component tree;
- relies on behaviour that only a root component has: bUnit renders the component as a child inside a render batch, so a component the app renders as a root (a prerendered `<component>` tag) that calls `StateHasChanged` and then uses an `@ref` child during a synchronous `OnInitializedAsync` behaves differently. Check how the parent hosts it; drive that state through an asynchronous path where you can, and leave root-only behaviour to a journey;
- depends on real browser layout, focus, scrolling or third-party JavaScript to behave.

Persisted or prerendered state alone is not a reason to route to a journey: add it with `this.AddBunitPersistentComponentState()` and `.Persist(key, value)`, computing the key with the component's own key utility.

Tell the user which route you chose and why. Do not replace real services with mocks to force a component into bUnit.

## Step 4 -- Register real services

Register the services the component injects, using their real implementations. Reuse the client project's own registration extension when it has one (for example `services.AddIndyRecordsClientServices()`), otherwise register each one:

```csharp
public sealed class TrackPagerTests : BunitContext
{
	private const string ScrollToTracks = "IndyRecordsInterop.scrollToTracks";

	private readonly List<TrackPagerChangedEventArgs> _raised = [];

	public TrackPagerTests()
	{
		_ = Services.AddLogging();
		_ = Services.AddScoped<IInteropService, InteropService>();

		_ = JSInterop.SetupVoid(ScrollToTracks).SetVoidResult();
	}
}
```

- When the component injects nothing, register nothing; do not add services in case they are needed.
- A service that would leave the process (an HTTP data service calling the server) is a reason to route to a journey test (Step 3). Do not substitute it on your own: ask the user first, explaining that a fake tests the component against your assumptions about the server. If they agree, prefer replacing only the network: register the client's real services as its `Program.cs` does and give the `HttpClient` a stub primary handler (`ConfigurePrimaryHttpMessageHandler`) that records requests and queues answers, including held answers for loading states. Otherwise use a small hand-written fake, not a mocking library; Moq is removed from this project.
- Never weaken the component or its services to make them testable.

## Step 5 -- Set up strict JavaScript interop

bUnit's interop runs in strict mode by default: any call that is not set up fails the test. Keep it strict.

- `JSInterop.SetupVoid("module.fn").SetVoidResult()` for `InvokeVoidAsync` calls. In bUnit 2, a `SetupVoid` without `.SetVoidResult()` leaves the call pending or failing; when an interop wrapper catches and logs, the test then passes without the call ever completing. Register a log recorder and assert nothing was logged.
- `JSInterop.Setup<T>("module.fn", args...).SetResult(value)` for calls returning a value.
- For JavaScript modules, `JSInterop.SetupModule("./_content/...js")` and set up calls on the returned module interop. Libraries can import a module on first render (for example Blazored.Modal's `CascadingBlazoredModal`); set it up even when nothing in it is called.
- When the component (and its children) make no interop calls, set nothing up. Strict mode still fails the tests if a later change adds an unplanned call.
- Do not switch to `JSRuntimeMode.Loose` to hide unexpected calls; an unexpected call is a finding.

## Step 6 -- Render with the parents' parameters and drive the component

Render with the parameters a parent passes, including `EventCallback`s that record what they receive:

```csharp
private async Task<IRenderedComponent<TrackPager>> RenderWithTracksAsync(int totalCount, int pageNumber)
{
	var pager = Render<TrackPager>(parameters => parameters
		.Add(x => x.MaxPagesToShow, 5)
		.Add(x => x.OnPageChanged, args => _raised.Add(args)));

	// The album page passes the totals in once the tracks arrive.
	await pager.InvokeAsync(() => pager.Instance.UpdateAsync(totalCount, pageNumber, pageSize: 12));

	return pager;
}
```

- For a generic (`@typeparam`) component, render it with the type argument the real parents use: `Render<OptionFilter<FormatOption>>(...)`.
- Record a parameterless `EventCallback` with a counter: `.Add(x => x.OnChanged, () => _changedCount++)`, then `Assert.Equal(1, _changedCount)`.
- When the parent reads results from a parameter object the component mutates, pass a real instance and keep a reference to assert on later (Step 8).
- Call the component's public methods inside `InvokeAsync`, so they run on the renderer's dispatcher, as a parent component's calls do.
- Drive user actions through bUnit's DOM event helpers on the rendered markup: `ClickAsync(new())`, `ChangeAsync(new() { Value = ... })`, `InputAsync(...)`, `KeyDownAsync(...)`.
- bUnit raises events on disabled elements, which a browser never does. Assert a control is enabled before driving it (`Assert.False(button.HasAttribute("disabled"))`), so a test cannot pass by clicking something a visitor could not.
- Use `WaitForState` or `WaitForAssertion` when the component updates asynchronously; never `Task.Delay`.
- For `[SupplyParameterFromQuery]` parameters, navigate the `NavigationManager` to the page URL before rendering, rather than passing the parameters.

## Step 7 -- Query as a visitor would

Find elements by what a visitor or assistive technology perceives, which also checks the accessible markup. bUnit's AngleSharp DOM has no role or accessible-name query, so add small helpers:

```csharp
// Finds an input through its <label for="...">, as a visitor reading the label would.
private static IElement InputLabelled<T>(IRenderedComponent<T> cut, string labelText)
	where T : IComponent
{
	IElement label = Assert.Single(cut.FindAll("label"), x => x.TextContent.Trim() == labelText);
	string id = label.GetAttribute("for") ?? throw new InvalidOperationException($"The label '{labelText}' has no for attribute.");

	// Not Find($"#{id}"): generated ids (Guid.ToString("N")) can start with a digit, which is not a valid CSS id
	// selector, so "#id" throws DomException for some runs and not others.
	return cut.Find($"[id='{id}']");
}

// Finds a button by its accessible name: its text, or its title when it shows only an icon.
private static IElement ButtonNamed<T>(IRenderedComponent<T> cut, string name)
	where T : IComponent
	=> Assert.Single(cut.FindAll("button"), x => x.TextContent.Trim() == name || x.GetAttribute("title") == name);
```

- Prefer labels, button text and `title`, `aria-label`, `aria-current`, `aria-disabled`, `aria-expanded` and `role` over class names.
- Assert ARIA state explicitly: `Assert.Equal("page", PageLink(pager, 1).GetAttribute("aria-current"))`, and `Assert.Null(...)` where the attribute must be absent.
- Compare localised text against the resource properties the component uses (for example `Translations.ClearFilters`), not a copied string. `CurrentCulture` is async-local: set it in the test method or a synchronous helper, not the constructor or an async helper, and restore it afterwards. Test each site language for dates, ordinals and translated labels.
- Include image `alt` text in accessible-name helpers for links and cards.

## Step 8 -- Assert on what the parent and visitor see

```csharp
[Fact]
public async Task ClickPage_RaisesTheNewPage_AndScrollsToTheTracks()
{
	var pager = await RenderWithTracksAsync(totalCount: 100, pageNumber: 1);

	await PageLink(pager, 3).ClickAsync(new());

	var raised = Assert.Single(_raised);
	Assert.Equal(new TrackPagerChangedEventArgs(PageSize: 12, PageNumber: 3), raised);
	Assert.Equal("page", PageLink(pager, 3).GetAttribute("aria-current"));

	_ = JSInterop.VerifyInvoke(ScrollToTracks, calledTimes: 1);
}
```

- Assert each raised `EventCallback`'s arguments, and that nothing is raised when nothing should be (`Assert.Empty(_raised)`, or a counter still at 0).
- When the component mutates a parameter object the parent reads afterwards, assert on that object, because that is what the parent sees.
- `JSInterop.VerifyInvoke(...)` returns the invocations: assign it to `_` (or inspect the arguments). `JSInterop.VerifyNotInvoke(...)` returns `void`: call it as a statement, never assign it.
- Cover the initial render (what is offered, what is disabled) and each user action that changes state.
- Children from a library: test what the visitor sees through this component (the option it offers, the value it passes back), not the library's whole contract.
- If testing reveals behaviour that looks like a bug, follow the repo's known-issues convention when it has one (see Known issues: assert the current behaviour and cite the ID). Otherwise report it to the user with the evidence and don't write a test asserting either behaviour until they decide.

## Step 9 -- Break check

Prove the tests can fail. Make one deliberate change, run the tests, confirm the expected tests fail with a useful message, then revert:

- Prefer breaks that mimic a regression, made in the test code: pass a wrong parameter value from the render helper, or stop recording a callback (an empty handler instead of the recording one).
- Test-side breaks alone can't prove markup and format assertions. With the user's approval, mutate production code in batches: back up the files, build, restore them byte-identical before running the tests against the mutated binaries, then rebuild clean. Never edit package code.
- The broken code must still compile with analyzers on; change a value rather than leaving an unused variable or unreachable code.
- Note which tests did **not** fail and whether that is expected; a test that survives every break is not testing anything.

After reverting, run the whole suite several times (for example three) to catch flakiness such as random ids or ordering. Use `--show-live-output on` to see probe output, because passing tests' output is hidden.

## Known issues

When the component behaves wrongly in a way the user confirms is out of scope (a control enabled when it shouldn't be, a missing label, a wrong event payload), log it in the repo's known-issues backlog (see `Test\README.md`) with the next ID, assert the **current** behaviour, and put the entry's ID in a comment on that assertion, so a later fix or regression is noticed. Skip a test with the ID as its reason only when the current behaviour can't be asserted reliably. Never fix it silently, and never weaken an assertion to hide it.

## Rules

- One component per invocation unless the user approved a batch; one test class per component, deriving from the bUnit context class. When several components share a pattern (listers, cards), write one abstract base with the shared facts and one derived class per component.
- Real services; no mocking library; strict interop.
- Name initial-render tests `Render_<Outcome>` and action tests `<Action>_<Outcome>`, extended as `<Action>_<Outcome>_<And...>` when one action has several visible results (`ClickClear_UnchecksEveryOption_AndRaisesChanged`).
- Query by label, text, title and ARIA where the markup allows; do not couple tests to styling classes.
- Do not change production markup only to make a selector easier unless it also improves accessibility, and ask the user first.

## Validation

```powershell
dotnet build "<BunitTestProject>"
dotnet test "<BunitTestProject>" --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<BunitTestProject>"`. The new tests pass on repeated runs, and the baseline tests still pass.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow. That file is written for production code; for test code, keep the test project's existing analyzer suppressions and do not add new ones without a reason.

## Output

Report:

- component tested and test class path, how it was chosen, or the reason it was routed to an end-to-end journey instead;
- the parents read and the parameters and type argument used;
- services registered (real, or the fake used with the user's agreement);
- interop calls set up and verified, or that there were none;
- scenarios covered (initial render, each action, callbacks raised, parameter objects);
- suspected behaviour bugs reported, and the user's decision;
- break checks performed, which tests failed and which did not;
- baseline and final build and test results, including the repeated runs.
