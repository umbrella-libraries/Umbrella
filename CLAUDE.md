# Claude Code instructions

For this repository, Claude should use:

- `CLAUDE.md` for repo-level guidance
- `.claude\agents\` for Claude agent entrypoints
- `.claude\skills\` for Claude skill documentation and wrappers
- `nuget-upgrade-exclusions.json` for repository-specific package policy

## NuGet safe upgrade agent

Use the `nuget-safe-upgrade` Claude agent for package-management work that must stay compatible across multiple target frameworks.

### Required behavior

1. Read `nuget-upgrade-exclusions.json`.
2. Use `.claude\skills\nuget-safe-upgrade\scripts\Invoke-NuGetSafeUpgrade.ps1` instead of manually editing package versions.
3. Preserve target-framework-specific version lines.
4. Validate accepted candidates with restore and resolved-package inspection.
5. Return successful, skipped, and blocked packages with clear next-step options.

## Skill authoring conventions

When writing or updating any skill file under `.claude/skills/` or `.github/skills/`:

- Use `IndyRecords` as the fictional placeholder for the client project name in all examples, descriptions, and code templates (e.g. `IndyRecordsGenericRepositoryApiController`, `IndyRecordsPageTitle`).
- Never embed real client names — ThriveForSend, WarwickUniversity, JustCalculators, CRUGroup, Just.DestinationRetirement, ProjectBole, or any other — anywhere in skill files.
- For multi-segment namespace examples, use `IndyRecords`, `VinylVault`, and `SpinCity.Marketing` as the fictional stand-ins.
- Both directories must remain identical: always copy a changed `.claude/skills/<skill>/SKILL.md` to `.github/skills/<skill>/SKILL.md`.

## Shared implementation

The Copilot and Claude entrypoints intentionally share one implementation layer:

- `.ai-shared\nuget-safe-upgrade\scripts\`
- `.ai-shared\nuget-safe-upgrade\nuget-upgrade-exclusions.schema.json`

Keep behavior in `.ai-shared\` and keep `.github\` / `.claude\` as thin adapters.
