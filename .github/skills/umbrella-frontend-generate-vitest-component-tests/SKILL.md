---
name: umbrella-frontend-generate-vitest-component-tests
description: 'Generate Vitest tests for one browser TypeScript module in an Umbrella front-end toolchain: a per-file jsdom environment, the markup the server renders, loading self-wiring modules with await import(), acting through DOM events and asserting on classes, attributes and ARIA, then npm test, lint and a break check. Use after umbrella-frontend-scaffold-vitest has set up Vitest.'
---

# Generate Vitest Component Tests

## Purpose

Add Vitest tests for **one browser TypeScript module** (a tab set, an accordion, a menu toggle, a form enhancement). The module runs against the markup the server renders, in jsdom, and the tests act as a visitor through DOM events.

This skill adds tests only. Vitest, jsdom, the `test` script and the lint configuration for test files are set up once by `umbrella-frontend-scaffold-vitest`.

## Preflight

1. Find the npm project that owns the module (the folder with `package.json` whose webpack or bundler entry imports it).
2. Check the setup exists:
   - `vitest` and `jsdom` in `devDependencies`;
   - a `test` script running `vitest run` (not the `npm init` placeholder);
   - if a `lint` script exists, an ESLint configuration that covers `*.test.ts`.

   If Vitest or jsdom is missing from `devDependencies`, stop and run `umbrella-frontend-scaffold-vitest`. Do not add packages from this skill.

   If they are declared but `node_modules` lacks them, `npm ci` is needed. First check for running watchers
   (Visual Studio Task Runner bindings, `webpack --watch`, `gulp watch`): `npm ci` deletes `node_modules` under them
   and leaves the build output inconsistent. Ask the user to stop them, or tell them to restart them afterwards, and
   rebuild the output (`npm run build`) once the tests are done.
3. When the user approved a batch of modules, list every script under the scripts folder and record each one as in or
   out of scope with a reason (no behaviour, only imports a stylesheet, a third-party analytics loader, unused). Check a
   view really renders each module's selectors; if none does, report the module as unused rather than inventing markup.
4. Read an existing `*.test.ts`, if any, and the repo's test README (for example `Test\README.md`), and mirror their conventions.
5. Read the module: how it finds its elements (selectors), whether it wires itself up when loaded (top-level `document.querySelectorAll(...).forEach(...)`) or exports a function or class, and which classes, attributes and ARIA state it changes.
6. List the network calls the module makes or triggers: `fetch` or `XMLHttpRequest` to the site's API or another host, third-party scripts it loads (maps, video players, analytics, consent tools), and iframes it inserts. jsdom makes no real requests for scripts and has no real network for the site's API.

## Step 1 -- Decide each network dependency with the user

If Preflight step 6 found none, skip this step. Otherwise give a recommendation and reason for each, then **ask the user what to do with each one** before writing tests:

| Call | Recommendation | Reason |
|---|---|---|
| `fetch("/api/catalogue/search")` | A browser journey (`umbrella-dotnet-generate-e2e-journey-tests`) | Exercises the real API and server together |
| A third-party video player script | A browser journey, or test only the module's own DOM changes | jsdom cannot run the vendor script |
| A small `fetch` whose response shape is stable | A hand-written `fetch` stand-in with `vi.stubGlobal`, if the user agrees | Keeps a fast test of the module's own logic |

Prefer a browser journey for anything whose value is the call itself. Never stub `fetch` or a script loader without the user's agreement, and when a stand-in is used, assert on the request the module made (URL, method, body), not only on the DOM.

When the user can't be asked (a delegated run), test only the paths without network, add a guard that fails the test on any `fetch` or `XMLHttpRequest`, and report the calls left out as questions.

## Step 2 -- Create the test file

Put the test beside the module, named `<module>.test.ts`. Start with the per-file environment docblock and explicit imports (no Vitest globals):

```ts
// @vitest-environment jsdom

import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
```

Import only the functions the file uses; the steps below use all of these between them, and an unused import fails lint. Use jsdom per file rather than globally, so plain logic tests stay in the faster Node environment.

## Step 3 -- Use the server's markup

Copy the markup the server renders for this component (from the Razor view, partial or component template), reduced to the elements and classes the module uses. Keep the real class names, attributes and nesting; a simplified structure the server never produces proves nothing.

```ts
// The markup the accordion partial renders: a button and a panel per item, all closed.
const markup = `
	<div class="accordion">
		<button class="accordion__toggle" aria-expanded="false" aria-controls="tracks-a">Side A</button>
		<div id="tracks-a" class="accordion__panel" hidden>Side A tracks</div>
		<button class="accordion__toggle" aria-expanded="false" aria-controls="tracks-b">Side B</button>
		<div id="tracks-b" class="accordion__panel" hidden>Side B tracks</div>
	</div>`;

const toggles = () => Array.from(document.querySelectorAll<HTMLButtonElement>(".accordion__toggle"));
const panels = () => Array.from(document.querySelectorAll<HTMLElement>(".accordion__panel"));
```

## Step 4 -- Load the module

**Self-wiring modules** (they query the document when loaded): set the markup first, then dynamically import the module, so it finds the elements as it does in the site bundle:

```ts
describe("accordion", () =>
{
	beforeAll(async () =>
	{
		document.body.innerHTML = markup;
		await import("./accordion");
	});
});
```

A module is evaluated once per test file. Either keep the tests in one file sharing that wired-up markup, ordering assertions so each test starts from a known state, or call `vi.resetModules()` and re-import after resetting `document.body.innerHTML` in `beforeEach` when each test needs a fresh start. Never use a static `import` for a self-wiring module: it runs before the markup exists.

**Modules that start on `DOMContentLoaded` or `load`**: jsdom is already at `readyState: "complete"`, so the event never fires by itself. Import the module once per file, then in each test set the markup and dispatch the event. Don't re-import these with `vi.resetModules()`: the old module's document and window listeners keep running.

**Exporting modules**: import the function or class statically and call it on the markup set in `beforeEach`.

**Modules that wrap a third-party SDK** (an analytics or tracking loader): replace the package with `vi.mock`, holding the
recording functions in `vi.hoisted` so the factory can reach them. When the module constructs an export with `new`,
make the mocked export a class: a `vi.fn` with an arrow implementation isn't a constructor. When a callback such as
`ready` is part of the flow, have the stand-in call it straight away. If each test needs a fresh load (different
settings on `document.body.dataset`), call `vi.resetModules()` and re-import, and remove the window or document
listeners each load added (record them with a spy on `addEventListener`), otherwise every earlier load's listener
still runs.

**Message listeners** (`window.addEventListener("message", ...)`): dispatch `new MessageEvent("message", { origin, data })`
and cover the origin check with a row per case: each allowed origin form (apex and subdomains), and `http://`, an
explicit port, a look-alike domain (`evilexample.com`, `example.com.attacker.test`), another top-level domain, the
vendor's own host, `"null"` (sandboxed frames) and an empty origin. Add rows for messages of another type, without a
type, a plain string and no data.

## Step 5 -- Act through DOM events, assert on the DOM

Act as a visitor: `element.click()`, `element.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }))`, setting `input.value` then dispatching `input` or `change` with `bubbles: true`. Do not call the module's internal methods.

Assert on what the visitor and assistive technology observe:

```ts
it("opens the clicked item's panel and marks its toggle expanded", () =>
{
	toggles()[1]!.click();

	expect(toggles().map(x => x.getAttribute("aria-expanded"))).toEqual(["false", "true"]);
	expect(panels().map(x => x.hidden)).toEqual([true, false]);
});
```

- Assert classes with `classList.contains`, attributes with `getAttribute`, and ARIA state (`aria-expanded`, `aria-selected`, `aria-hidden`, `aria-current`) explicitly.
- Map over all items and compare arrays, so a test shows which item went wrong.
- Use fake timers (`vi.useFakeTimers()`) for debounces and transitions; never wait on real time.
- When a library the code uses starts its own timers during a test (Bootstrap's fades when a modal opens, a slider's
  transitions), let them finish before the test ends: await the library's completion event (`shown.bs.modal`) or run
  the fake timers out. A timer left running can fire after the file's environment has been torn down; Vitest then
  reports an unhandled error and fails the run even though every test passed, and only on a slower machine such as
  the CI agent.
- jsdom has no layout: do not assert on sizes or positions. Cover those with a browser test (`umbrella-dotnet-generate-e2e-journey-tests`) if they matter.
- jsdom lacks some browser features. Faking these in the test file is allowed, with a comment saying why: element sizes and positions (`getBoundingClientRect`, `offsetWidth`), `innerText`, `matchMedia`, `IntersectionObserver`, `scrollIntoView`, `elementFromPoint`, page navigation, and webpack's `require`. Faking network calls or vendor code is different and needs the user's agreement (Step 1).
- A library that measures layout may take a different code path without it (a carousel jumps instead of transitioning). Model enough layout to follow the path the browser takes.
- jsdom fires `load` for a frame without `src`, not for one with an external `src`. Write a frame's document with `open`, `write` and `close`.
- Vitest clears mock calls before each test (`clearMocks`), so copy calls made in `beforeAll` before asserting on them.
- Test at each viewport the module branches on; a test that can't fail at one viewport proves nothing.

## Step 6 -- Run the checks

From the npm project folder:

```powershell
npm test
npm run lint      # only if package.json defines a lint script
npm run typecheck # only if package.json defines a typecheck script
```

Run both lint and typecheck when they exist: they can disagree, and CI may run only one. Follow `.gitattributes` for line endings (a lint `linebreak-style` rule may require CRLF). Fix lint and type errors in the test file rather than disabling rules. If lint reports an existing problem in the module itself, tell the user instead of changing unrelated code.

## Step 7 -- Break check

Prove the tests can fail. Change the module, not only the test, for each test: comment out the line that toggles a class or attribute, change a selector or a value it sets. Run `npm test`, confirm the expected test fails with a readable diff, then revert.

- For several modules, script the changes in batches, back up each file and restore it with a checksum check, and require every test to fail at least once.
- A running watcher rebuilds on every change, and a rebuild during a run can fail unrelated tests: rerun such a change on its own. Check the build output is consistent afterwards.

## Known issues

When the module behaves wrongly in a way the user confirms is out of scope (a missing ARIA update, a handler that never detaches, a broken keyboard path), log it in the repo's known-issues backlog (see `Test\README.md`) with the next ID, assert the **current** behaviour, and put the entry's ID in a comment on that assertion, so a later fix or regression is noticed. Skip a test with the ID as its reason only when the current behaviour can't be asserted reliably. Never fix it silently, and never weaken an assertion to hide it.

## Rules

- One module per invocation unless the user approved a batch; the test file sits beside the module.
- `// @vitest-environment jsdom` per file; explicit imports from `vitest`.
- Markup mirrors the server's real output.
- Set the markup before `await import()` for self-wiring modules.
- Act through DOM events; assert on classes, attributes and ARIA.
- Never add packages, change the Vitest configuration or relax lint rules from this skill.
- Ask before replacing `fetch` or a third-party script with a stand-in; prefer a browser journey for network behaviour.

## Validation

`npm test` passes, `npm run lint` passes when the script exists, and the break check failed as expected before being reverted. CI runs `npm test` when the `test` script exists; confirm the pipeline has that step (see `umbrella-frontend-scaffold-vitest`).

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow. For a front-end-only change, the npm lint and typecheck scripts are the relevant analyzers; build the owning .NET project too only when the npm build is part of its build (a csproj npm target).

## Output

Report:

- module tested and test file path;
- how the module is loaded (self-wiring with `await import()`, or exported API);
- network calls and scripts found, the user's choice for each, and any stand-in used;
- scenarios covered and the DOM state asserted;
- `npm test`, `npm run lint` and `npm run typecheck` results (or that a script does not exist);
- break check performed and its failure.
