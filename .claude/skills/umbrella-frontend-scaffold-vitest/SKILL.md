---
name: umbrella-frontend-scaffold-vitest
description: 'Add Vitest to an Umbrella npm project with browser TypeScript: a vitest run test script, pinned vitest, jsdom and vite versions, no config file unless needed, per-file jsdom opt-in with explicit imports and no globals, a first passing test, CI detection of a real test script, and the shared Umbrella ESLint config pointed at the test files. Use before generating Vitest component tests.'
---

# Scaffold Vitest

## Purpose

Make `npm test` run real unit tests for a project's browser TypeScript, with the smallest setup that works: Vitest with jsdom, opted into per test file, and no configuration file unless the project needs one. Add one passing test, make CI run it, and lint the test files.

This skill builds on `umbrella-npm-configure` (manifest, lockfile, command contracts) and `umbrella-typescript-configure` (tsconfig). Further tests come from `umbrella-frontend-generate-vitest-component-tests`.

## Preflight

1. Find each `package.json` that owns browser TypeScript (often under a server or client web project). Work on one project at a time.
2. Read its scripts. If `test` already runs a real test runner, stop and use `umbrella-frontend-generate-vitest-component-tests`. The npm placeholder (`echo "Error: no test specified" && exit 1`) counts as no test script.
3. Check the manifest and tsconfig follow the Umbrella conventions (exact versions, committed lockfile, `.npmrc` registries, `strict`, `moduleResolution`). If they have drifted, run `umbrella-npm-configure` and `umbrella-typescript-configure` first rather than working around them here.
4. Record the Node version used locally and in CI (`node --version`, the pipeline's Node install step, `engines`).
5. Look for an existing `vite.config.*` or `vitest.config.*` (Vitest reads either), webpack aliases the scripts import through, and the ESLint config (`eslint.config.*`).
6. Pick a first module to test: a small script that changes the DOM in response to a user action (tabs, an accordion, a toggle).

## Step 1 -- Install pinned versions

Choose the latest stable versions at least two weeks old whose `engines.node` covers the project's Node version:

```powershell
npm view vitest@<version> engines peerDependencies time --json
npm install --save-dev --save-exact vitest@<version> jsdom@<version>
npm ls vite
```

- Pin exact versions, following the manifest's style.
- Check which `vite` npm resolved for Vitest. Pin `vite` explicitly as a dev dependency, inside Vitest's supported range, when the resolved version requires a newer Node than local or CI, or conflicts with a version another package needs. Re-run `npm ls vite` to confirm a single version.
- Commit the updated lockfile. Use the registries `.npmrc` already defines; do not add new ones.

## Step 2 -- Add the test script

```json
"scripts": {
	"test": "vitest run"
}
```

`vitest run` runs once and exits with a failure code; plain `vitest` watches, which hangs CI. Add `"test-watch": "vitest"` only if the user wants it. Keep existing scripts unchanged.

## Step 3 -- Avoid a config file unless it is needed

Vitest's defaults find `**/*.{test,spec}.?(c|m)[jt]s?(x)` and skip `node_modules`. Without a config file:

- Opt into jsdom per file with a first-line comment, so tests that need no DOM run in plain Node and the environment is visible where it matters:

```ts
// @vitest-environment jsdom
```

- Import test functions explicitly (`import { describe, expect, it } from "vitest";`). Do not enable `globals`.
- Keep the tsconfig `types` array minimal. Do not add `vitest/globals` or `jsdom`; explicit imports carry the types.

Add a `vitest.config.ts` only for a concrete need, and say which: path aliases the scripts import through, a setup file shared by many tests, test files outside the default globs, or excluding build output (such as `wwwroot\dist`) that contains matching file names. If a `vite.config.*` already exists, check it suits tests, because Vitest reads it.

## Step 4 -- Write the first test

Place it beside the module, named `<module>.test.ts`:

```ts
// @vitest-environment jsdom

import { beforeAll, describe, expect, it } from "vitest";

// The markup the tracklist partial renders: a button per side, the first side open.
const markup = `
	<div class="tracklist">
		<button class="tracklist__side">Side A</button>
		<button class="tracklist__side">Side B</button>
		<ol class="tracklist__tracks tracklist__tracks--open">A tracks</ol>
		<ol class="tracklist__tracks">B tracks</ol>
	</div>`;

const sides = () => Array.from(document.querySelectorAll<HTMLElement>(".tracklist__side"));
const tracks = () => Array.from(document.querySelectorAll<HTMLElement>(".tracklist__tracks"));

describe("tracklist", () =>
{
	beforeAll(async () =>
	{
		// The module wires up every .tracklist on the page when it loads, as it does in the bundle.
		document.body.innerHTML = markup;
		await import("./tracklist");
	});

	it("opens the clicked side's tracks", () =>
	{
		sides()[1]!.click();

		expect(tracks().map(x => x.classList.contains("tracklist__tracks--open"))).toEqual([false, true]);
	});
});
```

- Build the DOM the server really renders, from the Razor view or component markup.
- For modules that wire themselves up when loaded, set the DOM first, then `await import()` the module. Each test file runs in its own module context, so the import runs once per file.
- Act as a user would (click, type, key presses) and assert visible state: classes, attributes, text, focus.
- Satisfy `noUncheckedIndexedAccess` with an explicit `!` on indexed elements in tests, rather than relaxing the tsconfig.

## Step 5 -- Run it

```powershell
npm test
```

If the project has a `typecheck` script (`tsc --noEmit`), run it too and confirm the test file compiles under the project's tsconfig.

## Step 6 -- Lint the test files with the shared config

If the project uses the shared Umbrella ESLint config (`@umbrella-libraries/eslint-config`), point it at the test files first and widen it later, so lint fixes to existing scripts can be checked by tests once they exist. The snippet uses CommonJS (`require`, `module.exports`), so name the file `eslint.config.cjs`; in a project whose `package.json` sets `"type": "module"`, a `.js` file would be loaded as an ES module and fail. Keep the existing file name if the config is already written as an ES module (`import`/`export default`).

`eslint.config.cjs`:

```js
const umbrella = require("@umbrella-libraries/eslint-config");

module.exports = umbrella({
	tsconfigRootDir: __dirname,
	// Only the Vitest tests are linted for now. Widen to "Static/scripts/**/*.ts" as the scripts gain tests.
	files: ["Static/scripts/**/*.test.ts"],
	ignores: ["gulpfile.js", "webpack.config.js"]
});
```

If the config enforces a line-break style, make sure `.gitattributes` checks TypeScript out with that style on every OS, or Linux CI fails lint. If the project has no ESLint setup, report that instead of adding one.

## Step 7 -- Make CI run the tests

The pipeline should run `npm test` after `npm ci` (and after the front-end build when the tests need its output), only when the project defines a real test script, and fail on a non-zero exit:

```powershell
# npm init writes a placeholder test script that always fails, so treat it as no tests.
$testScript = (Get-Content package.json -Raw | ConvertFrom-Json).scripts.test
if (-not $testScript -or $testScript -match 'no test specified') {
	Write-Host 'package.json has no test script; skipping.'
	return
}
npm test
if ($LASTEXITCODE -ne 0) { throw "npm test failed with exit code $LASTEXITCODE." }
```

Do the same for `npm run lint` when a `lint` script exists. If the pipeline templates are generated by a shared tool, change the template at its source as well and tell the user.

## Rules

- No `vitest.config` without a stated need.
- No globals; explicit imports in every test file.
- jsdom only in files that need a DOM, via the per-file comment.
- Exact versions and a committed lockfile.
- Do not change production scripts to make them testable unless the user asks.

## Validation

```powershell
npm ci
npm test
npm run typecheck   # when the script exists
npm run lint        # when the script exists
```

All must pass. Confirm `npm test` exits non-zero when the test is deliberately broken, then restore it.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow. When only npm files changed, build the .NET project that hosts them if its build runs npm steps.

## Output

Report:

- the project, versions installed (vitest, jsdom, vite and why vite was pinned, if it was);
- whether a config file was added, and why;
- the first test file and the module it covers;
- ESLint and CI changes;
- `npm test`, typecheck and lint results.
