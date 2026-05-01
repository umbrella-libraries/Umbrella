# Copilot agent instructions

This repository contains a portable NuGet safe-upgrade workflow.

Use these instructions for Copilot agent-style tasks.

## NuGet safe upgrade

When upgrading NuGet packages:

1. Read `nuget-upgrade-exclusions.json` first.
2. Preserve target-framework-specific package version lines.
3. Treat `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, and `Microsoft.EntityFrameworkCore*` as framework-coupled package families unless the repository config says otherwise.
4. Prefer the shared upgrade workflow over manual package edits.
5. Validate accepted candidates with restore and resolved-package inspection before keeping changes.

## Entry points

- Repo-wide Copilot guidance: `.github\copilot-instructions.md`
- Copilot agent: `.github\agents\nuget-safe-upgrade.agent.md`
- Copilot skill wrapper: `.github\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
- Shared implementation: `.ai-shared\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
