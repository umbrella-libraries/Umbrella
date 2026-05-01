## Umbrella NuGet safe upgrade

### Package upgrade rules

- Read `nuget-upgrade-exclusions.json` before selecting upgrade candidates.
- Preserve target-framework-specific package version lines in multi-targeted projects.
- Treat `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, and `Microsoft.EntityFrameworkCore*` as framework-coupled package families unless the repo config says otherwise.
- Prefer the shared safe-upgrade workflow over ad hoc package edits.
- Keep only changes that pass restore and resolved-package inspection.
- If a package is blocked, explain why and offer a safe next step such as `skip` or rerun with `-OverrideBlockedPackageId`.

### Relevant files

- `AGENTS.md`
- `.github\agents\nuget-safe-upgrade.agent.md`
- `.github\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
- `.ai-shared\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`