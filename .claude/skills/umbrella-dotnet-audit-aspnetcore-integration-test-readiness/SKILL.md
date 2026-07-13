---
name: umbrella-dotnet-audit-aspnetcore-integration-test-readiness
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
- whether controllers or services call `SignInManager`, `IAuthenticationService.SignInAsync`, `SignOutAsync`, or `RefreshSignInAsync`;
- which authorization paths require a claim to be absent, rather than merely set to a different value.

Recommendation rules:

- Prefer a test auth handler wired into the app's real default authenticate scheme so `[Authorize]`, policies, and `User` access behave normally.
- Let tests override identity via request headers or a factory/client helper.
- Let tests add, change, and explicitly omit application claims so missing-claim denial paths remain observable.
- When application code signs in, signs out, or refreshes a sign-in, require the test handler to implement `IAuthenticationSignInHandler` and configure the real sign-in/sign-out scheme; authentication-only handlers fail after authorization succeeds.
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

- every environment-name branch that enables production-only configuration, service registration, middleware, or health checks.
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

## Response contract audit

When the goal includes testing controller response codes (see `docs\api-base-controller-endpoint-map.md` in the Umbrella repository), capture the host facts that gate which codes are observable:

- whether `Program.cs` (or an extension it calls) registers `UseUmbrellaPropagateClaimsPrincipal()` or otherwise assigns `Thread.CurrentPrincipal` — without it, imperative resource authorization checks in `UmbrellaRepositoryCoreDataService` throw and every expected `403` surfaces as a `500`;
- whether the MVC setup calls `ConfigureUmbrellaApiBehaviorOptions()` / `ConfigureUmbrellaMvcBuilderOptions()` and whether it overrides `validationFailureStatusCode` — this determines whether model binding/validation failures return `422` (the default) or another code, and tests must assert the configured value;
- which environment name startup can safely use and which environment application services must observe — the base controllers' exception filters use `returnValue: !IsDevelopment`, so `500` `UmbrellaProblemDetails` shapes require a non-`Development` application environment even when startup must remain `Development` to avoid real cloud dependencies;
- whether the `CorePolicyNames.Create/Read/Update/Delete` policies (or custom names configured on `UmbrellaRepositoryDataServiceOptions`) are registered, and which resource authorization handlers exist for the entities under test;
- whether a test identity can be constructed that each resource handler denies — an always-succeeds handler makes imperative `403` untestable.

Include these facts in the readiness report when controller response-code tests are in scope. For a fuller conformance check of the server bootstrap itself (with consequences and recommended fixes), run `umbrella-dotnet-audit-server-bootstrap`.

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
- sign-in/sign-out handler requirements and claim-omission capabilities;
- required test config overrides;
- external services and isolation risks;
- proposed factory classes, collection names, and startup/application environment strategy;
- packages/project references to add;
- response contract host facts (claims propagation, validation failure status code, environment name, policy/handler coverage) when controller response-code tests are in scope;
- validation commands.

Do not modify files in this skill.
