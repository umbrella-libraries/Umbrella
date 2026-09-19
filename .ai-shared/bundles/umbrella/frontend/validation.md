# Front-end skill validation

The `frontend-multi-project` fixture label in skill-validation.json describes the scenarios below; the manifest itself is metadata, not an execution engine. Record executed versus planned cases accurately. Use disposable directories and existing supported toolchains. Do not install or modify consuming applications just to validate skill authoring.

## Authoring and portable inventory

Run the audit helper's Node test suite with Node 18 or later:

```powershell
node --test .ai-shared/skills/umbrella-frontend-audit-toolchain/scripts/audit-toolchain.test.cjs
```

Run the schema regression cases with PowerShell 7 or later:

```powershell
pwsh -File .ai-shared/bundles/umbrella/frontend/config.schema.test.ps1
```

These cases verify required copy destinations, valid direct imports and invalid destination paths. The Node suite also checks workspace engine policy, malformed workspace declarations and unsupported package-manager locks.
Run the skill-creator quick validator for each new skill when available and Umbrella.AI.Tools.Test for canonical metadata, assets and validation-manifest coverage. Run umbrella-ai sync from the Umbrella root and verify generated adapters contain resolved paths. Validate the starter against config.schema.json and check malformed baseline/exception/path examples are rejected.

## Behavioural scenarios for application-changing skills

1. **Empty/new host:** Set up an isolated .NET host with a supported toolchain. First asset build, typecheck, .NET build and publish succeed without a pre-existing dist/copied helper/scoped CSS directory. A second Apply leaves source/config/lock files unchanged.
2. **Hybrid/multiple entries:** Include site, marketing embed and PDF entries, Blazor Client/Server and an MVC _Layout view. Alignment preserves each entry's asset consumer, required chunks, framework version constraints and scoped stylesheet convention.
3. **Watcher dependencies:** Start one watcher. Change/add/delete a canonical helper and an isolated entry. Dependent output updates; obsolete generated files disappear; hand-authored isolated CSS survives. Stop the watcher and check child processes are gone.
4. **Failures:** Introduce invalid Sass, a TypeScript error and a failing copy/child process separately. Release builds return failure. Repair each error and confirm recovery. In watch mode a diagnostic may keep the process alive but must not pretend output was updated.
5. **Package graph:** Exercise independent locks and a root NPM workspace. Include a peer/engine incompatibility, excluded package and unreachable private registry. Analyze leaves source/locks unchanged; failed Apply restores only its own candidate changes; unknown queries do not become up-to-date results.
6. **Theme/migration:** Preserve a customized Bootstrap variable map, Font Awesome pseudo-element, font preference and ::deep rule through helper/module changes. Compare compiled CSS and representative rendered pages, keeping vendor migration boundaries explicit.
7. **Build integration:** A newly added scoped file works on the first .NET build. Published manifest references resolve to files, URLs work under the configured base path, and global/vendor/isolated cascade order is retained. No-change builds avoid unnecessary recompilation without missing imported-file changes.

Run only applicable scenarios for a change, but do not claim behavioural verification from metadata tests. Windows/POSIX and supported browsers require actual runs; otherwise list them as untested.
