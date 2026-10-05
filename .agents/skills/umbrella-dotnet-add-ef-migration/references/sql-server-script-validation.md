# SQL Server migration SQL and deployment-script validation

Read this reference when adding/reviewing custom SQL, repairing an idempotent-script failure, or assessing SQL Server migration test coverage. Apply it to `Up`, `Down`, and the actual deployment/rollback artifacts.

## Deferred parsing and column binding

EF's idempotent script checks migration history with `IF NOT EXISTS` guards. SQL Server can bind references in a guarded raw SQL statement before deciding to skip it. A migration that copies legacy columns into new tables and then drops those columns can apply successfully once yet fail on a later deployment with `Invalid column name`.

Use dynamic execution for custom SQL whose referenced columns/objects may be absent in a database state where the history guard should skip it, or whose references only become valid after earlier operations in the migration. Keep the dynamic call inside EF's generated guard so its inner SQL is parsed and bound only when executed. [EF Core's raw SQL guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#arbitrary-changes-via-raw-sql) documents `EXEC` for idempotent-script parser errors and statements that must start a batch.

For trusted, fixed migration SQL, a local helper avoids inconsistent escaping:

```csharp
private static string AsDynamicSql(string sql)
    => $"EXEC(N'{sql.Replace("'", "''", StringComparison.Ordinal)}')";

// In Up (and in Down when its SQL needs the same protection):
migrationBuilder.Sql(AsDynamicSql("""
    INSERT INTO [CustomerNames] ([CustomerId], [Name])
    SELECT [Id], COALESCE([LegacyName], N'Unknown') FROM [Customers];
    """));
```

The outer `N'...'` is a Unicode SQL literal. Double every embedded single quote exactly once for that outer literal: the inner `N'Unknown'` becomes `N''Unknown''` in the generated script, while already escaped quotes in the inner SQL need another level of escaping. Do not pre-escape text passed to the helper. Inspect the emitted SQL, including text containing apostrophes and non-ASCII characters.

`EXEC(N'...')` and `EXEC sys.sp_executesql N'...'` can both defer compilation. If SQL takes values from outside the fixed migration text, use typed `sp_executesql` parameters; literal escaping alone is not parameterization. Preserve inner Unicode literals where Unicode data is intended. See [sp_executesql](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-executesql-transact-sql) for separate-batch compilation and Unicode/parameter requirements.

For batch-first DDL such as `CREATE VIEW`, `CREATE PROCEDURE`, and `CREATE TRIGGER` (or applicable `ALTER`/`CREATE OR ALTER` forms), place each definition in its own dynamic batch. Keep prerequisite operations outside that batch so the definition starts it. `GO` is a client batch separator, not T-SQL; never embed it inside `migrationBuilder.Sql` or the dynamic SQL literal. See [SQL Server GO](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/sql-server-utilities-statements-go).

Wrapping changes compilation timing, not the intended data operation. Preserve statement order, predicates, joins, null handling, keys, values, constraints, and transaction/suppression settings. Copy and verify data before dropping its sources. Review `Down` for the same parsing/binding and batch-first hazards, with reverse data copies completed before their source tables/columns are removed. Dynamic execution does not make a destructive or irreversible rollback safe.

Dynamic batches have separate variable scope. Pass required outer values through typed `sp_executesql` parameters, and review temporary-object lifetime and batch boundaries before wrapping fragments of a larger operation.

## Repairing an older migration

An execution-wrapper repair may change an older migration's raw SQL solely to preserve its original behavior under the deployment script. Keep its migration ID/name, designer metadata, model snapshot, history entries, and `Up`/`Down` operation semantics unchanged. Explain the before/after SQL and why only compilation timing changes. Do not remove/re-add the migration or manipulate history to force it to run again.

A change to already deployed schema/data behavior normally belongs in a new corrective migration; it is a different task from a wrapper repair. A new migration alone cannot repair parser errors in an older block still included in a full idempotent deployment script. Repair that block and regenerate the artifact. Do not hide the issue by narrowing the script range unless the deployment contract itself calls for that range.

## Validate the deployment artifact on SQL Server

`MigrateAsync` executes pending migrations through a different path from the generated idempotent SQL. `EnsureCreated`, EF InMemory, SQLite, a successful build, and first-time migration success cannot establish SQL Server script rerun safety.

Use isolated disposable SQL Server databases, such as Testcontainers, with the deployment's relevant server version/compatibility and provider/tool versions. Confirm the target server and database before applying SQL; never use a shared or production database for these checks. Isolate startup configuration and disable automatic migration/seeding that would change the scenario before script execution.

Generate from the same source/build, context, projects, migration range, environment/provider selection, and options as the pipeline. For a pipeline deploying full history, generate from `0` rather than only the newest migration. Example (replace placeholders and use the pipeline's options):

```powershell
dotnet ef migrations script 0 "<TargetMigration>" --idempotent `
    --project "<MigrationsProject>" --startup-project "<StartupProject>" `
    --context "<DbContext>" --output "<IsolatedArtifacts>\migrations.sql"
```

Inspect the actual artifact's guards, dynamic calls, quote escaping, batch boundaries, and history writes. Execute that file with the deployment runner or an equivalent runner that understands `GO` and fails on SQL errors. Do not send the whole file as one `SqlCommand`, strip its guards, or split SQL naively on text/semicolons. Record execution failures even when generation succeeded.

| Scenario | Setup and execution | Required assertions |
| --- | --- | --- |
| Fresh database | Provision an empty disposable database and execute the full idempotent script to the target. | Intended schema exists; expected migration IDs occur once in the configured history table (default `__EFMigrationsHistory`). |
| Upgrade with preserved data | Prepare a separate database at the migration immediately before the affected change, seed representative legacy rows, then execute the deployment script. Include relationships, null/optional values, each transformed value category, apostrophes and Unicode where relevant. | Compare actual values, keys, relationships and row counts against expected copies/transforms; source columns disappear only after preservation. Prior history remains intact and newly applied IDs appear once. |
| Already migrated database | Prepare a separate database at the target, with the legacy columns/objects absent and representative destination data present; execute the same full script. | No parser/binding errors; schema, data, and history are unchanged. A pre-repair schema state is useful when validating an older wrapper repair. |
| Repeat execution | Execute the exact same script again against the fresh and upgraded databases after their first successful run. | No errors or duplicate/mutated data; schema and history remain unchanged. |
| Rollback, where supported | Generate a script from the newer migration to the supported earlier target; execute it on a separate migrated database, then reapply the forward script. If the provider/tooling and deployment support idempotent rollback, test its already-rolled-back state and repeat execution too. | Restored schema/data match the documented rollback contract; only reverted migration IDs are removed, earlier history is retained, and forward reapplication succeeds without duplicate data. |

Review rollback raw SQL even when rerunnable rollback is unsupported. Do not assume a non-idempotent rollback can be repeated. Document irreversible/lossy operations and the supported recovery path rather than claiming full data restoration.

Report the script path, generation command/range, runner, SQL Server/provider/tool versions, scenarios executed, and history/data assertions with their outcomes. If SQL Server or Docker is unavailable, perform static review and generation/build checks that are possible, explicitly mark database scenarios unexecuted, and leave runtime deployment validation outstanding. Never substitute a different provider or factory smoke test and report this matrix as passed.
