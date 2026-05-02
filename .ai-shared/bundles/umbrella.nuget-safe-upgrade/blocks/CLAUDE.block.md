## Umbrella NuGet safe upgrade agent

Use the `nuget-safe-upgrade` Claude agent for package-management work that must stay compatible across multiple target frameworks.

### Required behavior

1. Read `nuget-upgrade-exclusions.json`.
2. Use `.claude\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1` instead of manually editing package versions.
3. Preserve target-framework-specific version lines.
4. Validate accepted candidates with restore and resolved-package inspection.
5. Return successful, skipped, excluded, and blocked packages with clear next-step options.
6. When Analyze shows a package blocked with NU1605 errors referencing sibling packages that are in the successful list, this is a lockstep family — Apply mode resolves it automatically in a single pass. See the agent definition for full guidance.

### Shared implementation

The Copilot and Claude entrypoints intentionally share one implementation layer:

- `.ai-shared\nuget-safe-upgrade\scripts\`
- `.ai-shared\nuget-safe-upgrade\nuget-upgrade-exclusions.schema.json`