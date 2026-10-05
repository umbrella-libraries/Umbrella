---
name: umbrella-dotnet-add-ef-migration
description: 'Add and validate EF Core migrations with project and DbContext auto-detection, or repair SQL Server raw SQL wrappers for idempotent deployment scripts while preserving migration semantics and history.'
---

# dotnet Add EF Core Migration

## Purpose

This skill adds EF Core migrations consistently through the shared script and validates their deployment SQL. For an existing SQL Server migration's execution-wrapper repair, inspect and fix the affected SQL without scaffolding a new migration.

## Assets

- `scripts\Invoke-AddEfMigration.ps1`
- `references\sql-server-script-validation.md`

## Inputs

These inputs apply to scaffolding a new migration. A wrapper repair uses the existing migration and does not require a new name or snapshot.

- `-MigrationName` (required) — name for the new migration; use semantic versioning to match existing migrations (e.g. `1.0.20`)
- `-RepoRoot` (optional) — defaults to git repository root
- `-MigrationsProject` (optional) — relative path to the `.Migrations.csproj`; auto-detected if omitted
- `-StartupProject` (optional) — relative path to the Web SDK startup `.csproj`; auto-detected if omitted
- `-Context` (optional) — DbContext class name; auto-detected via `dotnet ef dbcontext list` if omitted

## Migration naming convention

Migration names must use semantic versioning to match the existing migration history (e.g. `1.0.20`). Before running the script, inspect the existing files in the `Migrations` folder to find the current highest version and use the next increment.

## Workflow

1. Inspect the migrations, provider, and deployment pipeline. Determine whether the task adds a migration or repairs an existing SQL execution wrapper. For a wrapper repair, skip scaffolding and follow the review and validation steps below; otherwise determine the current highest version number.
2. Confirm the next migration name with the user if it is not already provided.
3. Run the wrapper script with `-MigrationName`.
4. The shared script auto-detects the migrations project, startup project, and DbContext from current `dotnet ef --json` output, then normalizes whitespace and applies the repository's IDE0005/IDE0058/IDE0161 generated-migration style.
5. Report the new and modified files returned by the script.
6. Investigate an empty-migration warning. If custom SQL for database objects or data operations is intended, populate and review it; otherwise check for saved model changes and unresolved project/context selection before proceeding.
7. Review `Up` and `Down` for intended schema changes and data preservation, including copying data before dropping source columns. For SQL Server, read [SQL Server script validation](references/sql-server-script-validation.md) before accepting custom SQL or claiming deployment readiness. Use `EXEC(N'...')` or `sp_executesql` where parsing/binding must be deferred, including batch-first DDL and rollback SQL.
8. Generate the deployment script using the pipeline's migration range and options. For SQL Server, execute the validation matrix in the reference, including an already migrated database and repeated execution of the same idempotent script. A build, `MigrateAsync`, or a successful first application does not establish rerun safety.
9. Report generated/modified files, whether this is a new migration or a wrapper repair, script commands/range, executed checks and unavailable checks. Leave the changes ready for review; commit or push only when requested.

## Command examples

Add a migration (auto-detected projects):

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\umbrella-dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1 -MigrationName 1.0.20
```

Add a migration (explicit projects):

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\umbrella-dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1 `
    -MigrationName 1.0.20 `
    -MigrationsProject "Core\MyApp.Core.Data.Migrations\MyApp.Core.Data.Migrations.csproj" `
    -StartupProject "Web\MyApp.Web.Server\MyApp.Web.Server.csproj" `
    -Context MyAppDbContext
```

## Output expectations

- `New files:` — the `.cs` and `.Designer.cs` migration files
- `Modified files:` — the `*ModelSnapshot.cs` file
- Warning if the migration body contains no `migrationBuilder` calls
- For a wrapper-only repair: only the affected migration SQL/helper should change; migration IDs, designer metadata, history, operation semantics, and model snapshot stay intact.
- SQL Server script-validation results by scenario, including history/data assertions and any limitations when SQL Server is unavailable.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Safety rules

- Never run `dotnet ef migrations add` directly.
- Resolve empty-migration warnings and review any intentional custom SQL before accepting a new migration.
- Do not delete/recreate deployed migrations or edit history to bypass an error. A wrapper-only repair preserves behavior; a schema/data behavior correction normally requires a new corrective migration. Adding a new migration alone cannot fix an older SQL block that still fails parsing in the full deployment script.
- Keep reusable logic in `.ai-shared\` and keep this folder as a thin wrapper.
