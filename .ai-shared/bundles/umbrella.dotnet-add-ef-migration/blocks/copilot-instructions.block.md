## Umbrella dotnet Add EF Migration

### EF migration rules

- Never run `dotnet ef migrations add` manually. Use `.github\skills\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1`.
- Pass the migration name as `-MigrationName`. Follow the repository naming convention (semantic version, e.g. `1.0.20`).
- The script auto-detects the migrations project, startup project, and DbContext.
- After success, show the user the generated and modified files.
- Remind the user to review the migration file before committing.

### Relevant files

- `AGENTS.md`
- `.github\agents\dotnet-add-ef-migration.agent.md`
- `.github\skills\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1`
- `.ai-shared\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1`
