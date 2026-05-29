# Copilot instructions

This repository contains a portable NuGet safe-upgrade workflow.

## Package upgrade rules

- Read `nuget-upgrade-exclusions.json` before selecting upgrade candidates.
- Preserve target-framework-specific package version lines in multi-targeted projects.
- Treat `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, and `Microsoft.EntityFrameworkCore*` as framework-coupled package families unless the repo config says otherwise.
- Prefer the shared safe-upgrade workflow over ad hoc package edits.
- Keep only changes that pass restore and resolved-package inspection.

## Relevant files

- `AGENTS.md`
- `.github\agents\nuget-safe-upgrade.agent.md`
- `.github\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
- `.ai-shared\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`

## Skill and agent authoring

`.github\skills\` and `.github\agents\` are **generated outputs** — do not edit them directly.

- Edit skill files in `.ai-shared\skills\<name>\SKILL.md`
- Edit agent files in `.ai-shared\agents\github\`
- Run `umbrella-ai sync` to regenerate all adapter directories
- Commit both the canonical source and the regenerated files
