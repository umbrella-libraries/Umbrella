## Umbrella NuGet safe upgrade

When upgrading NuGet packages in this repository:

1. Read `nuget-upgrade-exclusions.json` first.
2. Preserve target-framework-specific package version lines.
3. Treat `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, and `Microsoft.EntityFrameworkCore*` as framework-coupled package families unless the repo config says otherwise.
4. Prefer the shared upgrade workflow over manual package edits.
5. Keep only changes that pass restore and resolved-package inspection.
6. If a package is blocked, explain why and offer a safe next step such as `skip` or rerun with `-OverrideBlockedPackageId`.

### Entry points

- Copilot agent: `.github\agents\nuget-safe-upgrade.agent.md`
- Copilot skill wrapper: `.github\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`
- Shared implementation: `.ai-shared\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1`