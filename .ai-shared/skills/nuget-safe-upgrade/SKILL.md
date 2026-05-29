---
name: nuget-safe-upgrade
description: 'Safely analyze and apply NuGet package upgrades in multi-targeted .NET repositories using exclusions, framework-aware version selection, restore checks, and transitive dependency graph validation.'
---

# NuGet Safe Upgrade

## Purpose

This skill safely analyzes and applies NuGet package upgrades while respecting exclusions, framework-coupling rules, and transitive dependency graph constraints.

## Assets

- `scripts\Invoke-NuGetSafeUpgrade.ps1`
- `scripts\NuGetSafeUpgrade.Common.ps1`
- `nuget-upgrade-exclusions.schema.json`

## Inputs

- optional package IDs to limit scope
- optional project paths to limit scope
- repository root or current working directory
- root-level `nuget-upgrade-exclusions.json`

## Workflow

1. Read `nuget-upgrade-exclusions.json`.
2. Run the wrapper script in `{{skill_dir}}\nuget-safe-upgrade\scripts\`.
3. Start in Analyze mode unless the user explicitly requested direct application.
4. Filter scope by package IDs or project paths if provided.
5. Let the shared implementation select the highest safe candidate version for each package.
6. Keep only changes that pass restore and transitive graph checks.
7. Return successful, skipped, and blocked packages with override guidance.
8. After each Apply pass, check the blocked list for lockstep family packages and run a second Apply pass if any are present.

## Legacy TFM version pinning

Packages listed in `frameworkCoupledFamilies` (in `nuget-upgrade-exclusions.json`) are capped at the target framework's major version for net5+ TFMs. For legacy TFMs (`netstandard*`, `net4*`), the cap is the package's *current* major version — preventing inadvertent upgrades that pull in transitive dependencies from a newer runtime generation.

`System.Text.Json` and `System.Net.Http.Json` are included in `frameworkCoupledFamilies` for this reason: on `netstandard2.0`/`net462` they must stay at their current 6.x baseline and must not be upgraded to 10.x.

## Framework-split upgrades

When a package upgrade is blocked on some TFMs but would be safe on others, the script automatically splits the single unconditional `<PackageReference>` into per-framework conditional `<ItemGroup>` blocks:

- **Blocked TFMs** keep the current version (e.g., `net8.0`/`net9.0` when the new version pulls v10 transitive deps).
- **Allowed TFMs** receive the upgraded version (e.g., `net10.0`).

Split candidates appear in the `successful` list with action `Analyzed (split candidate)` or `Applied (split by framework)` and include `upgradeFrameworks`/`keepFrameworks` fields.

Split only applies to unconditional `PackageReference` items directly in `.csproj` files. `PackageVersion` entries in `Directory.Packages.props` and already-conditional references are not split.

## Lockstep package families

Some packages must be upgraded together — for example `System.Composition.AttributedModel`, `System.Composition.Runtime`, and `System.Composition.TypedParts` all share the same version number and each member pulls the others transitively.

The script processes packages independently, so when it tests a family member in isolation the siblings have already been reverted (Analyze) or not yet written (Apply). This produces a NU1605 blocked result:

```
Candidate X.Y.Z failed restore: Detected package downgrade: <SiblingPackage> from X.Y.Z to <OldVersion>
```

**How to identify:** a blocked package's NU1605 error references sibling packages that appear in the *successful* list of the same report, upgraded to the same candidate version.

**How to resolve:** in Apply mode no action is needed — siblings are written to disk before each subsequent package is tested, so the family resolves in a single Apply pass. In Analyze mode the blocked entry is expected; proceed directly to Apply.

## Command examples

Analyze:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Analyze
```

Apply:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply
```

Override a blocked package after review:

```powershell
powershell -ExecutionPolicy Bypass -File {{skill_dir}}\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply -PackageId Microsoft.Extensions.Logging.Abstractions -OverrideBlockedPackageId Microsoft.Extensions.Logging.Abstractions
```

## Output expectations

- `successful`: packages safely upgraded or safe upgrade candidates in Analyze mode
- `skipped`: packages excluded or with no newer safe version
- `blocked`: packages that failed compatibility or graph checks
- `options`: safe follow-up commands for blocked items

## Safety rules

- Never ignore `nuget-upgrade-exclusions.json`.
- Never flatten framework-specific versions into a single package version.
- Never keep a candidate that fails restore.
- Never keep a candidate that resolves framework-coupled package families beyond the target framework major unless explicitly overridden.
- Keep reusable logic in `.ai-shared\` and keep this folder as a thin wrapper.
