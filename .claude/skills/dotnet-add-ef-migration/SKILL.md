---
name: dotnet-add-ef-migration
description: 'Add an EF Core database migration with auto-detection of the migrations project, startup project, and DbContext. Reports generated and modified files and warns on empty migrations.'
---

# dotnet Add EF Core Migration

## Purpose

This skill wraps the shared EF migration script so Claude Code can add migrations consistently without running `dotnet ef migrations add` by hand.

## Assets

- `scripts\Invoke-AddEfMigration.ps1`
- `..\..\..\..\.ai-shared\dotnet-add-ef-migration\scripts\`

## Inputs

- `-MigrationName` (required) — name for the new migration; use semantic versioning to match existing migrations
- `-RepoRoot` (optional) — defaults to git repository root
- `-MigrationsProject` (optional) — relative path to the `.Migrations.csproj`; auto-detected if omitted
- `-StartupProject` (optional) — relative path to the Web SDK startup `.csproj`; auto-detected if omitted
- `-Context` (optional) — DbContext class name; auto-detected via `dotnet ef dbcontext list` if omitted

## Workflow

1. Inspect existing migrations to determine the next version number.
2. Run the wrapper script with `-MigrationName`.
3. The shared script auto-detects migrations project, startup project, and DbContext.
4. Report the new and modified files.
5. Warn the user if the migration is empty.

## Command examples

Add a migration (auto-detected projects):

```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1 -MigrationName 1.0.20
```

Add a migration (explicit projects):

```powershell
powershell -ExecutionPolicy Bypass -File .claude\skills\dotnet-add-ef-migration\scripts\Invoke-AddEfMigration.ps1 `
    -MigrationName 1.0.20 `
    -MigrationsProject "Core\MyApp.Core.Data.Migrations\MyApp.Core.Data.Migrations.csproj" `
    -StartupProject "Web\MyApp.Web.Server\MyApp.Web.Server.csproj" `
    -Context MyAppDbContext
```

## Output expectations

- `New files:` — the `.cs` and `.Designer.cs` migration files
- `Modified files:` — the `*ModelSnapshot.cs` file
- Warning if the migration body contains no `migrationBuilder` calls

## Safety rules

- Never run `dotnet ef migrations add` directly.
- Always verify the migration is non-empty before committing.
- Keep reusable logic in `.ai-shared\` and keep this folder as a thin wrapper.
