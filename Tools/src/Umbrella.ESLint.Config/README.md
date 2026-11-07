# @umbrella-libraries/eslint-config

Umbrella's shared type-aware TypeScript linting and formatting standards. Requires Node
22.22.3+, 24.15.0+, or 26+, ESLint 10.12+ within major 10, and a TypeScript compiler API
supported by typescript-eslint (currently >=4.8.4 <6.1.0).

## Use in another repository

Connect NPM to the Umbrella Azure Artifacts development feed using **Connect to feed**
in Azure DevOps. Keep authentication in your user configuration or CI credential task.
Map the Umbrella scope to that feed in the consuming project's `.npmrc`, leaving other
packages on the project's usual registry. Keep credentials out of this file:

```ini
@umbrella-libraries:registry=<feed-npm-registry-url>
```

```sh
npm install --save-dev @umbrella-libraries/eslint-config@next
npm install --save-dev eslint@^10.12.0 typescript@npm:@typescript/typescript6@6.0.2
```

Use an explicit published version for controlled upgrades. The `next` tag contains
prereleases; stable releases use `latest`. TypeScript 7 users still need the TypeScript 6
API compatibility alias shown above for linting; their compiler can remain separately installed.

Place this `eslint.config.cjs` beside your `package.json` and `tsconfig.json`:

```javascript
const umbrella = require("@umbrella-libraries/eslint-config");

module.exports = [
	...umbrella({
		tsconfigRootDir: __dirname,
		files: ["Content/scripts/**/*.ts", "FrontendTest/**/*.ts"],
		ignores: ["gulpfile.js", "webpack.config.js", "webpack.paths.js"]
	}),
	{
		rules: {
			// Project-specific overrides go here.
		}
	}
];
```

ES modules can use `import umbrella from "@umbrella-libraries/eslint-config"` and
`tsconfigRootDir: import.meta.dirname` in `eslint.config.mjs`.
The required `files` patterns are relative to the consuming ESLint config; their sources
must belong to the consumer's TypeScript project. `ignores` adds global exclusions to
`**/bin/**`, `**/obj/**`, `**/wwwroot/**`, `**/node_modules/**`, and `**/*.d.ts`.
Append ESLint configuration objects to override rules or narrow overrides with `files`.

Add `"lint": "eslint . --max-warnings 0"` and `"lint-fix": "eslint . --fix"` scripts.
Rules enforce Allman braces, tabs, double quotes, semicolons, final newlines, CRLF,
no trailing spaces, no BOM, and the recommended type-aware TypeScript checks.

## Work on Umbrella

Run `npm ci` at the repository root (NPM 11.13.0; CI uses Node 24.16.0).
`npm run lint` uses this workspace's source immediately after edits. No feed access or
package version bump is needed for local changes. Run `npm run test:eslint` for config
and publishing tests, `npm run pack:eslint` to stage a release, and
`npm run test:eslint-package` to validate that tarball in an independent consumer.
The latter requires access to public NPM dependencies and PowerShell 7.

The source manifest is private at `0.0.0`. Packing reads `Version` from the repository's
`Directory.Build.props`, stamps only a temporary copy, and writes `artifacts/npm`.
Neither source manifests nor lockfiles are rewritten. Only `index.cjs`, this README,
the MIT licence and the release manifest enter the tarball.

The existing Azure pipeline tests and packs on PRs; successful master builds publish
the tested tarball to the existing development feed. Its build-service identity needs
Feed Publisher (Contributor) permissions. Authentication is provided by the NPM pipeline
task using the existing `feedId`; no feed credentials are included in the artifact.
An identical published version is skipped. Changed contents with the same version fail:
bump the shared Umbrella version before releasing them. Auth/network failures also fail
the publishing job. Feed publication is not part of local validation.

New Umbrella-owned published NPM packages use the `@umbrella-libraries` scope and the existing category/src
and category/test folder layout. Third-party packages keep their upstream names.

The earlier `umbrella-eslint-config` package is a separate package identity; consumers
switching from it must update their dependency and ESLint imports to the scoped name.
The scope does not change versioning: releases still use the shared .NET version.
Publishing to Azure Artifacts does not reserve the scope on public npm; the matching npm
organization must be owned before public publication is configured.
