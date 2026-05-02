## Umbrella dotnet Add EF Migration

When adding an EF Core migration in this repository:

1. Use the `dotnet-add-ef-migration` skill — do not run `dotnet ef migrations add` manually.
2. Provide a migration name following the repository naming convention (semantic version, e.g. `1.0.20`).
3. The script auto-detects the migrations project, startup project, and DbContext; pass explicit parameters only when auto-detection fails.
4. Review the generated migration file before committing — verify it is non-empty and reflects the intended schema changes.

### Skill

- `.github\skills\dotnet-add-ef-migration\SKILL.md`
- Shared script: `.ai-shared\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1`
