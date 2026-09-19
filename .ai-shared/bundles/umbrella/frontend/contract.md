# Umbrella front-end contract

Baseline: **1**. This is a behaviour/configuration baseline, not a frozen package-version list. The companion skills configure NPM, Webpack, TypeScript, Gulp and Sass in .NET applications. Do not copy an existing application's configuration wholesale.

## Discovery and profiles

Support `blazor` (Razor components), `mvc-razor` (views/pages), and `hybrid`. Discover actual projects: Server/Client names are hints, not constraints. Preserve multi-entry builds, marketing embeds, PDF scripts, library outputs, custom plugins, CDN/base paths and runtime JS interop contracts. Other bundlers/package managers are unsupported by this baseline; report them without migration.

Use optional root `umbrella-frontend.json`, validated against [config.schema.json](config.schema.json). [config.starter.json](config.starter.json) is an illustrative Server/Client starting point, not an automatically installed config. Adapt paths and entries to the target. No config is required for discovery/audit. Existing user requirements override baseline defaults; document intentional differences. Do not invent browser support or upgrade policy to fill missing information.

A shared-helper copy strategy requires an explicit destination; direct imports do not. Workspace children may use the owning root's Node/NPM engine policy, with declared child values taking precedence in the inventory. Unsupported workspace declarations and non-NPM locks are reported as unknown, without suggesting migration.

If an ancestor workspace declaration is unsupported, descendant lock ownership and effective engine policy remain unknown. The inventory emits unknown FE003/FE001 findings and null lockOwner, lockFile and effectiveEngines values rather than inventing standalone ownership. Declared engine values remain visible. Resolve membership before assessing drift; null does not establish that a lock or engine policy is absent.

Configuration is descriptive input to agent workflows, not executable build configuration or an automatic rewrite engine. The inventory helper validates only baseline/exception fields it consumes; validate the complete file with the schema before Apply. All paths are repository-relative, use forward slashes and stay within the repository. Package roots may be `.`. An exception identifies one rule and one exact repository-relative finding path (or `*` for a deliberate repository-wide exception), with a reason. Exceptions remain visible; they do not turn failed checks into passes. Unknown rule IDs or obsolete exceptions require review.

## Rule catalogue

| ID | Required behaviour / decision |
| --- | --- |
| FE001 | Runtime requirements and selected local/CI Node/NPM agree with the chosen toolchain engines. |
| FE002 | Dependency commands have explicit ordering and failure propagation across supported shells. |
| FE003 | Each package uses its actual lock owner; reproducible CI restores use that lock. |
| FE004 | Initial/watch/release tasks have reliable completion, and release compilation errors fail the build. |
| FE005 | Changes to shared helpers update dependent global/scoped outputs during watch. |
| FE006 | Manifest entries, chunk dependencies, public URLs and Razor asset consumers agree. |
| FE007 | Scoped CSS is generated/discovered before .NET isolation on the first clean build and included in publish. |
| FE008 | Type checking is explicit and release builds cannot silently ignore type errors. |
| FE009 | Browser policy, syntax target, runtime APIs and prefixing are compatible and deliberate. |
| FE010 | Generated files have known owners; clean/unlink cannot delete hand-authored assets. |
| FE011 | Shared framework/helper versions are API-compatible; differences are assessed rather than blindly synchronized. |
| FE012 | Build modes, caching, source maps, hashing and minification implement the declared development/deployment policy. |
| FE013 | Watch tasks have one owner, no duplicate startup and clean shutdown. |
| FE014 | Canonical helper sources and generated copies are distinguished; originals drive copying/compilation. |

The audit helper statically checks a subset of FE001/FE002/FE003/FE008. Other rules require semantic inspection and/or execution. Its rule findings are leads with confidence/limitations, not a substitute for understanding a dynamic config. Report unreadable or unsupported inputs as unknown; never claim a clean bill of health on partial evidence.

## Setup and maintenance

- **Analyze:** inspect without modifying application/config/lock files. Registry queries only for requested upgrade analysis; resolve candidates in an isolated copy when necessary.
- **Apply:** existing setup/fix authorization is sufficient within scope. Inspect and preserve unrelated changes. Patch known settings, preserve custom behaviour and record exceptions. Upgrades, source migrations, rebranding and bundler changes are not implicit in alignment.
- **Verify:** run relevant observable checks. Verification can create outputs; use isolated directories/checkouts for negative tests, clean builds and production output where the current application may be running.

Use current official package metadata/documentation when selecting versions. Never downgrade compatible newer versions just to match a snapshot. Observe exclusions, workspace ownership, peer constraints, private feeds and lifecycle-script policy. A private-feed failure is a blocker/unknown, not evidence that a package is unavailable or current. Never print tokens or dump .npmrc contents.

Standard command semantics: `build` produces development assets; `build-release` produces production assets and fails on compiler errors; `typecheck` checks browser TypeScript without emission; `watch` owns the applicable long-running asset tasks. Add scoped commands only where scoped sources exist and preserve externally used aliases. Package restore is separate from routine watcher startup.

Keep global asset compilation separate from .NET isolation. Neither Webpack nor Sass rewrites Blazor selectors. Shared helpers normally contain tokens/mixins/functions, not framework/reset selectors. Preserve actual application branding and accessibility choices. Align processing policy, not necessarily identical output settings for every pipeline.

## Reporting and completion

Use `baselineVersion`, `repositories`, `projects`, `findings`, `exceptions`, `changedFiles`, `checks` and `limitations` for a structured workflow report. Findings contain `rule`, `classification` (defect, drift, intentional-exception or unknown), `path`, optional `line`, evidence, consequence and recommendation. Checks record the actual command/mode, exit/result and any untested environment. Do not include credentials or full environment dumps.

The inventory helper's JSON is input to this report, not the final semantic audit. Across repositories compare observed behaviour/version groups and preserve per-repository outcomes. Completion means relevant checks passed, exceptions were explained and a second Apply would be a no-op. Unperformed checks remain explicit. See [validation.md](validation.md) for behavioural scenarios.

## Maintainer references

Use documentation matching installed versions; these URLs are entry points, not pinned compatibility guarantees:

- NPM reproducible installs: https://docs.npmjs.com/cli/commands/npm-ci/
- Webpack caching/config dependencies: https://webpack.js.org/configuration/cache/
- TypeScript options: https://www.typescriptlang.org/tsconfig/
- Gulp completion: https://gulpjs.com/docs/en/getting-started/async-completion/
- Sass modules/migration: https://sass-lang.com/documentation/breaking-changes/import/
- Blazor isolation: https://learn.microsoft.com/aspnet/core/blazor/components/css-isolation
- MVC/Razor isolation: https://learn.microsoft.com/aspnet/core/mvc/views/overview#css-isolation
- Bootstrap Sass configuration: https://getbootstrap.com/docs/5.3/customize/sass/
