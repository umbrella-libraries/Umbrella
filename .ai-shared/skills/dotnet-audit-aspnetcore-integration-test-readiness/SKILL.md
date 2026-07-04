---
name: dotnet-audit-aspnetcore-integration-test-readiness
description: 'Read-only audit of an ASP.NET Core server project to determine how to scaffold robust integration tests with WebApplicationFactory, xUnit collections, test authentication, EF Core DbContext replacement, SQL Server Testcontainers, configuration overrides, and external-service isolation. Use before creating ASP.NET Core controller integration test infrastructure.'
---

# Audit ASP.NET Core Integration Test Readiness

## Purpose

Inspect an ASP.NET Core server project and produce the concrete inputs needed to scaffold integration-test infrastructure. This skill is read-only. Do not create files or edit projects while using it.

## Discovery

1. Locate the server project and entry point:
   - `.csproj` using `Microsoft.NET.Sdk.Web`;
   - `Program.cs` with top-level statements or an explicit `Program` class;
   - whether a public `partial Program` hook already exists for `WebApplicationFactory<Program>`.
2. Locate existing test projects and central test config:
   - `Directory.Build.props`;
   - `Directory.Build.targets`;
   - `Directory.Packages.props`;
   - existing `Test\*.Test` projects.
3. Identify package management style:
   - explicit versions in project files;
   - true CPM via `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`;
   - shared test package injection conditioned on `IsTestProject=true`.

## Authentication audit

Read the auth setup in `Program.cs` and related extension methods.

Capture:

- default authenticate/challenge/sign-in schemes;
- whether Identity cookies, JWT bearer, policy schemes, or custom handlers are used;
- authorization policy registration methods;
- role policy names and accepted role values;
- primary-role or custom claim conventions;
- user id claim type expected by code, commonly `ClaimTypes.NameIdentifier`;
- app-specific claims required by controllers/resource handlers;
- whether tests need an unauthenticated mode.

Recommendation rules:

- Prefer a test auth handler wired into the app's real default authenticate scheme so `[Authorize]`, policies, and `User` access behave normally.
- Let tests override identity via request headers or a factory/client helper.
- Include defaults that represent a high-privilege but realistic user, not an anonymous bypass.
- Do not remove authorization globally unless the user explicitly asks for smoke-only host tests.

## DbContext and database audit

Find the EF Core context and registration:

- `AddDbContext<TDbContext>` / `AddDbContextPool<TDbContext>`;
- context constructor shape, including whether it takes `DbContextOptions` or `DbContextOptions<TDbContext>`;
- provider and provider options;
- migrations assembly;
- query splitting, retry, command timeout, sensitive logging, and detailed errors;
- database initialization and migration behavior;
- Identity stores or other services that depend on the context.

Recommendation rules:

- For real controller integration tests, prefer the SQL Server Testcontainers factory when production uses SQL Server and EF provider behavior matters.
- Preserve provider-specific options such as migrations assembly and query splitting in the test factory.
- If the context constructor uses non-generic `DbContextOptions`, the concrete factory may need to remove or replace both `DbContextOptions<TDbContext>` and `DbContextOptions`.
- Keep Testcontainers tests in a collection that disables parallelization unless the suite has been explicitly designed for concurrent containers.

## Configuration and external services audit

List all required options and external services created during startup:

- `AddOptionsWithValidateOnStart<T>()`;
- `builder.Configuration.Get<T>()` used before services are built;
- Key Vault, App Insights, Data Protection, Blob Storage, email, AI clients, queues, service bus, health checks, HTTP clients, file systems, dynamic image providers.

For each item, classify it:

- must be provided as inert test configuration;
- should be replaced with a test double service;
- safe as long as no endpoint touches it;
- unsafe because startup itself opens a network connection.

Recommendation rules:

- Prefer `ConfigureAppConfiguration` with in-memory overrides for required config.
- Use `Development` only when the app's startup avoids production-only services in that environment.
- Do not rely on real user secrets, developer machines, cloud resources, or production-looking defaults.
- Note endpoints such as health checks that may touch external services even if normal controller tests do not.

## Test project audit

Determine:

- desired test project name and location;
- required package references, usually `Umbrella.Testing.AspNetCore` and optionally `Umbrella.Testing.Xunit`;
- project references needed to compile factories, auth claims, DbContext, migrations, and shared security types;
- whether a solution entry is needed.

## Output

Return a short readiness report with:

- server project and entry-point hook status;
- authentication scheme and test auth strategy;
- DbContext type, constructor shape, migrations assembly, and replacement notes;
- required test config overrides;
- external services and isolation risks;
- proposed factory classes and collection names;
- packages/project references to add;
- validation commands.

Do not modify files in this skill.
