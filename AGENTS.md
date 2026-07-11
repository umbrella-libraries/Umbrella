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
- Copilot agent: `.github\agents\umbrella-nuget-safe-upgrade.agent.md`
- Copilot skill wrapper: `.github\skills\umbrella-nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
- Shared implementation: `.ai-shared\skills\umbrella-nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`

## Skill and agent authoring

`.github\skills\` and `.github\agents\` are **generated outputs** — do not edit them directly.

- Edit skill files in `.ai-shared\skills\<name>\SKILL.md`
- Edit agent files in `.ai-shared\agents\claude\` or `.ai-shared\agents\github\`
- Run `umbrella-ai sync` to regenerate `.claude\skills\`, `.github\skills\`, `.claude\agents\`, and `.github\agents\`
- Commit both the canonical source and the regenerated files
