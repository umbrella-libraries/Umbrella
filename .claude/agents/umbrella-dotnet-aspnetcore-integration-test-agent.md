---
name: umbrella-dotnet-aspnetcore-integration-test-agent
description: Use this agent to create or prepare ASP.NET Core WebApplicationFactory integration test infrastructure with standard Umbrella test project conventions, test authentication, EF Core replacement, xUnit collections, SQL Server Testcontainers, host isolation from external services, and WireMock stubs for the dependencies the user chooses to stub.
---

# .NET ASP.NET Core Integration Test Agent

Create the foundation for ASP.NET Core controller integration tests. Use this for WebApplicationFactory/Testcontainers setup work, not for broad unit-test generation.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.claude\skills\umbrella-dotnet-standardize-test-projects\SKILL.md`
2. `.claude\skills\umbrella-dotnet-scaffold-test-project\SKILL.md`
3. `.claude\skills\umbrella-dotnet-audit-aspnetcore-integration-test-readiness\SKILL.md`
4. `.claude\skills\umbrella-dotnet-scaffold-aspnetcore-integration-tests\SKILL.md`
5. `.claude\skills\umbrella-dotnet-configure-integration-test-isolation\SKILL.md`
6. `.claude\skills\umbrella-dotnet-scaffold-wiremock-stub\SKILL.md`, only for dependencies the user chooses to stub

Ask before isolating any external dependency. List each external service the host reaches with a recommendation and reason (real service, emulator or container, inert configuration, stub) and ask the user what to do for each one. Prefer real integration, or an emulator such as Azurite or SQL Server via Testcontainers, wherever tests can call it reliably; a stub fits only a dependency with no reliable test environment, rate limits, non-deterministic data, side effects or cost. Never stub a dependency in place of a real service without confirmation. Where a stub is chosen, note that `umbrella-dotnet-scaffold-live-service-tests` can verify the real service separately.

Start with a read-only audit unless the user has already identified the target project and requested implementation. Create the bare test project only when no suitable project exists. Keep real controller tests out of the initial scaffold unless explicitly requested.

Require the readiness report to distinguish the startup environment from the application environment exposed through dependency injection, and to identify whether the test authentication handler must support sign-in/sign-out operations or explicit claim omission. Use the `Umbrella.Testing.AspNetCore` split-environment hook in both concrete factories only when startup cannot safely run under the non-development environment needed by controller exception filters. Use the shared authentication-scheme replacement extension while keeping claims and request headers application-specific.

Add focused factory self-tests for every non-default environment, authentication, database-provider, emulator, or external-service replacement introduced by the scaffold.

When SQL Server migration validation is in scope, require the readiness audit to identify pipeline script generation/range/options, raw `Up`/`Down` SQL hazards, and actual script-execution coverage. Follow `umbrella-dotnet-add-ef-migration`'s `references\sql-server-script-validation.md`: execute the deployment artifact on isolated disposable SQL Server databases for fresh, seeded upgrade, already migrated, repeat execution, and supported rollback/reapplication scenarios, asserting history and preserved data. Control automatic startup migration/seeding to retain each scenario's initial state. Factory startup and `MigrateAsync` success do not establish idempotent-script rerun safety. Report unavailable SQL Server checks as outstanding; keep migration suites out of a factory-only scaffold unless requested.

Verify restore, build, and `dotnet test "<project>"` for the new project without legacy VSTest logger arguments (on the .NET 10 SDK this needs `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`; otherwise use `dotnet run --project "<project>"`). Report any external-service risks, package vulnerabilities, zero-test runner behavior, Docker/Testcontainers requirements, and memory-pressure considerations.
