---
name: nuget-safe-upgrade
description: 'Safely analyze and apply NuGet package upgrades in multi-targeted .NET repositories using exclusions, framework-aware version selection, restore checks, and transitive dependency graph validation.'
---

# NuGet Safe Upgrade

## Purpose

This skill wraps the shared NuGet upgrade implementation so Claude Code can use the same behavior as GitHub Copilot without duplicating package logic.

## Assets

- `scripts\Invoke-NuGetSafeUpgrade.ps1`
- `..\..\..\..\.ai-shared\nuget-safe-upgrade\scripts\`
- `..\..\..\..\.ai-shared\nuget-safe-upgrade\nuget-upgrade-exclusions.schema.json`

## Inputs

- optional package IDs to limit scope
- optional project paths to limit scope
- repository root or current working directory
- root-level `nuget-upgrade-exclusions.json`

## Workflow

1. Read `nuget-upgrade-exclusions.json`.
2. Run the wrapper script in `.claude\skills\nuget-safe-upgrade\scripts\`.
3. Let the shared implementation inventory package references, exclusions, target frameworks, and candidate versions.
4. Keep only changes that pass restore and transitive graph checks.
5. Return successful, skipped, and blocked packages with override guidance.
6. If Analyze reports a package as blocked due to NU1605 errors that reference sibling packages from the successful list (lockstep family), proceed to Apply — it resolves the whole family in a single pass because changes are written to disk before each subsequent package is tested.

## Command examples

Analyze:

```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Analyze
```

Apply:

```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply
```

Override a blocked package after review:

```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1 -Mode Apply -PackageId Microsoft.Extensions.Logging.Abstractions -OverrideBlockedPackageId Microsoft.Extensions.Logging.Abstractions
```

## Output expectations

Return a report with:

- `successful`: packages that were safely upgraded or are safe upgrade candidates in analyze mode
- `skipped`: packages skipped because of exclusions or lack of newer safe versions
- `blocked`: packages that failed compatibility or graph checks
- `options`: safe follow-up commands for blocked items

## Safety rules

- Never ignore `nuget-upgrade-exclusions.json`.
- Never flatten framework-specific versions into a single package version.
- Never keep a candidate that fails restore.
- Never keep a candidate that resolves framework-coupled package families beyond the target framework major unless the user explicitly overrides that package.
- Keep reusable logic in `.ai-shared\` and keep the Claude skill folder as a thin wrapper.
