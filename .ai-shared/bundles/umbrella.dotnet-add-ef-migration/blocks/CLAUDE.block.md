## Umbrella dotnet Add EF Migration

Use the `dotnet-add-ef-migration` Claude agent when adding an EF Core database migration.

### Required behavior

1. Never run `dotnet ef migrations add` manually — always use `.claude\skills\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1`.
2. Pass the migration name as `-MigrationName`. Follow the repository naming convention (semantic version, e.g. `1.0.20`).
3. The script auto-detects the migrations project, startup project, and DbContext.
4. After the script completes, show the user the new and modified files.
5. Remind the user to review the migration file for correctness before committing.

### Shared implementation

The Copilot and Claude entrypoints share one implementation layer:

- `.ai-shared\dotnet-add-ef-migration\scripts\`
