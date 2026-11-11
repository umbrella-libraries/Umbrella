---
name: umbrella-dotnet-generate-custom-api-controller-tests
description: 'Generate integration tests for custom API controllers built on the three endpoint-less Umbrella bases, deriving per-action response status contracts per variant: UmbrellaDataAccessApiController (protected data-access helper composition), UmbrellaDataServiceApiController (ExecuteOperationAsync over a data service), and UmbrellaApiController (hand-rolled status helper enumeration). The testing counterpart of umbrella-dotnet-scaffold-custom-api-controller.'
---

# Generate Custom API Controller Integration Tests

## Purpose

Generate integration tests for concrete controllers built on the endpoint-less Umbrella base controllers. These controllers have no fixed endpoint map — **the per-action status contract must be derived from the action's code**, then tested with the same per-status recipes as the generic controllers. The three variants mirror `umbrella-dotnet-scaffold-custom-api-controller`:

- **Variant A — `UmbrellaDataAccessApiController`**: actions compose the protected `ReadAllAsync`/`ReadAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync` helpers over the core data access service.
- **Variant B — `UmbrellaApiController`**: fully hand-rolled actions using the status helper methods and/or `OperationResult` mapping (orchestration, Identity flows, external integrations).
- **Variant C — `UmbrellaDataServiceApiController<TDataService>`**: actions compose the protected `ExecuteOperationAsync` overload pair over an injected data service.

The authoritative contract is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository (see its "Custom Endpoints on the Base Controller Hierarchy" section) — read it when available.

## Required inputs (all variants)

1. `umbrella-dotnet-audit-api-controller-response-contract` output for the target controller — the per-action derivation with testability verdicts, including the resolved validation-failure host state.
2. Working integration test infrastructure via `umbrella-dotnet-audit-aspnetcore-integration-test-readiness` / `umbrella-dotnet-scaffold-aspnetcore-integration-tests`, satisfying the response contract host requirements (claims propagation, configured `validationFailureStatusCode`, non-`Development` environment for `500` shapes, policies/handlers).
3. Anonymous, passing, and denying test identities (one denying/passing pair per grant branch the resource handler distinguishes), plus the user's choice for each external dependency the actions reach (see External dependencies).

## Variant A — deriving contracts from data-access helper composition

Each protected helper contributes a fixed status set:

| Helper | Built-in statuses | Notes |
| --- | --- | --- |
| `ReadAllAsync` | `200`, `403`, `500` | `403` only when `enableAuthorizationChecks: true` (default). Pagination clamped 1–50 before the core call. |
| `ReadAsync` | `200`, `404`, `403`, `500` | `404` when the lookup returns `null`. |
| `CreateAsync` | `201`, `400`, `403`, `500` | `400` from a `null` body or entity-level validation on save. No built-in `409`. |
| `UpdateAsync` | `200`, `400`, `404`, `409`, `403`, `500` | `409` (`ConcurrencyStampMismatch`) requires the entity to implement `IConcurrencyStamp`. |
| `DeleteAsync` | `204`, `404`, `403`, `409`, `500` | `409` only from a commit-time race (not deterministically testable over HTTP) or a conflict-returning delete callback. |

An action's contract is the **union** of:

- the statuses of every helper it calls, minus imperative `403` where it passes `enableAuthorizationChecks: false`;
- `422` (or the configured `validationFailureStatusCode`; plain `400` with an ASP.NET `ValidationProblemDetails` body when the host never registers the Umbrella behavior options — the contract audit resolves which state applies) if the action binds any input;
- `401` and declarative `403` from its `[Authorize]` attributes;
- any status produced by callbacks the action supplies (a callback returning an `IOperationResult` maps through the standard status table);
- any status produced by code in the action before or after the helper calls (derive those lines using the Variant B rules).

Worked example — a singleton-settings `GET` calling `ReadAsync(1, ..., enableAuthorizationChecks: false)` under `[Authorize(Policy = ...)]` yields `200`, `404` (only if the seed row can be absent), `401`, `403` (declarative only), `500` — and **no** `422`, since nothing is bound. Do not generate imperative-`403` or `422` tests for such an action.

When a `404` requires temporarily removing a seeded singleton, snapshot the complete row, delete it inside a non-parallel database collection, and restore it in `finally` using `CancellationToken.None`. Preserve its original key; use repository-supported restoration or a tightly scoped `IDENTITY_INSERT` statement when the key is database-generated. Never leave the shared seed absent after the test.

## Variant B — deriving contracts from hand-rolled action bodies

Enumerate the status-helper and `OperationResult`/`OperationResultFailure` calls in the action body — that enumeration *is* the contract. Apply these rules:

1. **In-action `Unauthorized(...)` is not middleware `401`.** It returns `401` *with* an `UmbrellaProblemDetails` body and is testable without `[Authorize]`. Assert the problem-details body for this variant and an empty body for the middleware challenge — they are different tests.
2. **`ValidationProblem(ModelState)` from an action body is `400`**, not `422`. The configured `validationFailureStatusCode` applies only to pre-action model binding — and only when the host registers the Umbrella behavior options; in the unconfigured state, pre-action and in-action validation failures share status `400` and must be distinguished by body content.
3. **`Conflict(...)` and `ConcurrencyConflict(...)` are both `409`** — distinguish with `problemDetails.Code == "ConcurrencyStampMismatch"`. Hand-rolled update paths typically pair a manual stamp comparison with `catch (UmbrellaConcurrencyException)`; the stamp-rotation recipe tests both.
4. **Duplicate-resource guards often exist twice** — a pre-save lookup (testable with a duplicate seed) and a save-result re-check that closes the race window (usually untestable over HTTP; record as excluded).
5. **Never infer statuses from route shape.** Anti-enumeration endpoints deliberately return success for missing resources; only generate `404` tests where the action actually returns `NotFound`.
6. **Do not assume base-class conventions** — custom clamps, ad-hoc guards (`id < 1` → `400`), domain-state `401`s. The action body is the source of truth.
7. **External dependencies gate codes** (CAPTCHA → `400`, email senders, payment gateways, search or catalogue APIs) — decide each one with the user first (see External dependencies); never call a real service that has side effects or cost.
8. **The `500` contract is standard**: catch-all filters with `returnValue: !IsDevelopment`; shape assertions need a non-`Development` host. Prefer a deterministic in-scope trigger when one exists—for example, submit a nonexistent related ID that the mapper writes to a scalar foreign key and let the real relational constraint reject persistence. Assert the Umbrella problem-details response and verify that no partial state change was committed. Otherwise use a throwing fake. Optional coverage.
9. **Actions returning `OperationResult(...)` from logic services** map through the standard table (`GenericSuccess` → `200`, `Created` → `201`, `NoContent` → `204`, `NotFound` → `404`, `Conflict`/`ConcurrencyConflict` → `409`, `Forbidden` → `403`, `NotAllowed` → `405`, `InvalidOperation` → `400`, `GenericFailure` → `500`). Drive the service into each state, or substitute a fake returning each `IOperationResult` when the real state is impractical to arrange.

## Variant C — deriving contracts from `ExecuteOperationAsync` composition

Each action passes a delegate invoking one operation on the injected `TDataService`, plus an endpoint-specific `500` error message. The envelope contributes the mechanics (cancellation check, `IOperationResult` → HTTP mapping, `500` on unhandled exceptions outside Development); **the status codes come from the `IOperationResult`s the composed service operation can return**:

- For services extending `UmbrellaRepositoryDataService`, per-operation status sets match the Pattern 2 endpoint map: `FindAllSlimAsync` → `200`/`403`; `FindByIdAsync` → `200`/`404`/`403`; `CreateAsync` → `201`/`400`/`403`; `UpdateAsync` → `200`/`400`/`404`/`409`-concurrency/`403`; `DeleteAsync` → `204`/`404`/`409`/`403`; `ExistsByIdAsync` → `200` `true`/`false` (never `404`); `FindTotalCountAsync` → `200`. Endpoint-enablement flags on the service → `405`; `AuthorizationXxxChecksEnabled => false` removes the imperative `403`.
- For custom service implementations, enumerate the `IOperationResult` factory calls in the service method — that enumeration is the operation's status set, mapped through the standard table (as Variant B rule 9, but at the service layer).

The action's contract is the union of: the composed operation's statuses, the envelope `500` (assert the action's exact error message string), `422`/configured code if the action binds input, and `401`/declarative `403` from attributes. `TDataService` may implement only a subset of `IGenericDataService` or a fully custom interface — derive from the methods that exist, and check the service class (not the controller) for enablement/authorization flags when it extends `UmbrellaRepositoryDataService`.

## External dependencies

When an action calls an external service (directly or through a logic service), decide how the tests treat it before writing them:

1. List each dependency the action reaches, with a recommendation and reason. Prefer the real service or an emulator/container (Azurite, SQL Server via Testcontainers) wherever tests can call it reliably; a stub only fits a dependency with no reliable test environment, rate limits, non-deterministic data, side effects or cost.
2. **Ask the user per dependency**: real service, emulator, existing stub, extend the existing stub, or skip the tests that need it. Never stub automatically in place of a real service.
3. If an existing stub is chosen, reuse it as the factory exposes it (read the test README for its location). If a new or extended stub is chosen, use `umbrella-dotnet-scaffold-wiremock-stub` first. Note that `umbrella-dotnet-scaffold-live-service-tests` can verify the real service separately.
4. Record skipped dependencies and the codes they gate as exclusions in the class comment block.

When a stub is used:

- Put the test class in the stub's non-parallel collection, and reset the stub (`Reset()`) and any recorded error log at the start of each test. Use `ClearRequests()` to forget requests sent while arranging, and read what was sent from `Requests`.
- Mark tests with the repo's Docker-gated attribute (`[IndyRecordsFact]` here), not plain `[Fact]`, when the host needs containers: without Docker the host never starts, so a plain `[Fact]` fails instead of skipping.
- **Arrange** the stub's response in each test (a recorded fixture with the real response shape where available).
- **Assert on the request log**: what the action sent downstream (path, query, body terms, paging, locale), not only the response, because a stub returns the same data whatever it receives. Assert no downstream call is made where the action should reject the request first (for example `Assert.Empty(factory.CatalogueApi.Requests)` after a `400`).
- Assert that the host logged no errors behind a successful response, when the repo has an error-log recorder.
- Add a **failure-characterisation test**: make the stub fail (for example `503`) and assert what the action does today (a `500`, an empty `200`, a logged error). Document in a `<remarks>` that it records current behaviour, which may want changing, and that the test should be updated if the behaviour is deliberately changed.
- Characterise **both stub failure modes**: an error status with an empty body, and an unmatched request (the stub's
  own `404`, which has a body). Clients often handle them differently: one may give a `500` and the other an empty `200`.
- Put the class in the **stub's non-parallel collection** whenever the action calls a stubbed dependency, rather than a
  database collection.

```csharp
/// <remarks>
/// Records current behaviour: the catalogue client logs a failed search and returns no results, so visitors see
/// "no results" rather than an error during an outage. Update this test if that behaviour is changed.
/// </remarks>
[IndyRecordsFact]
public async Task SearchAsync_WhenTheCatalogueApiFails_ReturnsNoResults()
{
	_factory.CatalogueApi.GivenReleasesFail(HttpStatusCode.ServiceUnavailable);

	using HttpClient client = _factory.CreateClient();
	using HttpResponseMessage response = await client.GetAsync(new Uri("api/catalogue/search?format=vinyl", UriKind.Relative), CancellationToken);

	Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	Assert.Contains(_factory.Errors.Logged, x => x.Contains("CatalogueSearchService", StringComparison.Ordinal));
}
```

## Test generation (all variants)

Use the per-status recipes from `umbrella-dotnet-generate-api-repo-controller-tests` — they describe mechanisms, not endpoints, and apply directly: seeded happy paths via a scoped `DbContext`, `401` anonymous (attribute-gated) vs in-action, `403` per identity class, `404` non-existent key, `409` stamp rotation asserting `code = ConcurrencyStampMismatch`, `400`/`422` per the resolved host state, optional `500` via a deterministic persistence-constraint trigger or a throwing fake + non-`Development` host.

Shared conventions: test class per controller in the SQL Server Testcontainers collection (or the stub's collection
when the action calls a stubbed dependency), `<Method>Async_<Scenario>_Returns<Status>` naming, and the `Umbrella.Testing.AspNetCore.Http` problem-details assertion extensions. Use the plain ASP.NET validation helper when Umbrella behavior options are absent. Capture returned problem details when asserting their fields; otherwise assign the awaited result to `_` for analyzer-clean code:

```csharp
[Collection(IndyRecordsSqlServerIntegrationTestCollection.Name)]
public sealed class SystemSettingsControllerTests
{
	private const string ApiUrl = "/api/systemsettings";

	private readonly IndyRecordsSqlServerWebApplicationFactory _factory;

	public SystemSettingsControllerTests(IndyRecordsSqlServerWebApplicationFactory factory)
	{
		_factory = factory;
	}
}
```

Read the `[HttpGet("...")]`/`[Route]` attributes for URLs — custom shapes mean custom routes; never assume REST conventions.

Also cover, for every action:

- **Hostile input**: for each string parameter forwarded into a downstream query language (SQL, OData, a search or
  graph query API), send a quote, a trailing backslash, braces and a very long value, and assert on the request log what was
  sent. Log unescaped input as a security known issue.
- **Paging**: test the action's clamps at each boundary, with the values missing, and at `int.MaxValue` (offset
  overflow).
- **Headers**: assert the caching headers (`Cache-Control`, `ETag`, `Expires`, `Last-Modified`) and `Content-Type`, and
  that a repeated request reaches the dependency again when nothing is cached.
- **CORS**: derive the policy (origins, methods, headers, credentials) and where it is applied (endpoint, controller or
  app-wide). For each route, send a preflight and a simple GET from an allowed origin, the site's own host and a
  foreign origin; assert `Access-Control-Allow-Origin`, `-Methods`, `-Headers`, `-Credentials` and `Vary: Origin`, and
  record whether the policy is scoped to the routes or applies to the whole site.
- **Path parameters** (catch-all `{*path}` routes that read files): test traversal (`..`, `%2e%2e%2f`, `%5c`,
  absolute paths) both through the client and with raw `TestServer` requests, because `TestServer` doesn't remove dot
  segments the way Kestrel does. Backslash handling differs by OS: branch rows on `OperatingSystem.IsWindows()`. Log
  any escape from the intended folder as a security known issue.
- **Front-end build output**: when a route serves built assets, compare responses with the files the bundle manifest
  names and read content-hashed names at runtime, so the tests pass with both development and production builds.
- **Unreachable failure states**: when a failure can't be produced over the shared host without changing shared files
  (build output, configuration), call the action directly — resolve the controller with
  `ActivatorUtilities.CreateInstance` from a request scope, with a stand-in for the one collaborator — rather than
  starting a second host.
- **Shared shapes**: when several actions share one shape, use a descriptor table and theories with one row per action
  instead of copying tests per action. xUnit truncates string theory arguments at 50 characters, so keep rows distinct
  within that or the results merge.

## Break check

Prove the tests can fail before reporting them:

1. Mutate the action, service or client in batches, each mutation aimed at one or more tests. Copy the files into a
   uniquely named scratch folder first.
2. Build, and restore the sources straight after the mutated build, so a parallel agent never builds against mutated
   code. Run the tests against the mutated binaries.
3. Check every test, and every theory row, failed in at least one batch with a clear message.
4. Restore the files byte-identical, touch them so the incremental build doesn't reuse old binaries, rebuild and rerun.

## Known issues

When an endpoint behaves wrongly in a way the user confirms is out of scope (a wrong status code, a swallowed failure, a missing validation), log it in the repo's known-issues backlog (see `Test\README.md`) with the next ID, assert the **current** behaviour, and put the entry's ID in a comment on that assertion, so a later fix or regression is noticed. Skip a test with the ID as its reason only when the current behaviour can't be asserted reliably. Never fix it silently, and never weaken an assertion to hide it.

## Rules (all variants)

- Generate tests only for codes the contract audit marked testable; document exclusions (race re-checks, dependency-gated codes without fakes, optional `500`s) in a comment block at the top of the test class.
- Satisfy earlier pipeline gates when targeting later codes (the helper/operation pipelines evaluate not-found → concurrency → hooks → authorization → save).
- One assertion focus per test: status code, body shape, and the observable side effect that distinguishes the branch (anti-enumeration endpoints return the same status either way — assert the side effect via seeded state).
- Keep tests independent with uniquely seeded data; do not weaken production authorization or bypass guards in production code — substitute at the DI layer in the factory only.
- Put every created database row, temp file, blob, external-emulator resource, or mutated singleton behind `try`/`finally` and clean it with `CancellationToken.None`.
- Restore ambient state such as `Thread.CurrentPrincipal` in `finally`; when repeated, prefer a small disposable scope helper rather than open-coded assignment.
- Reuse application-local data builders for repeated domain graphs, but do not combine response assertions, identity request construction, and feature-specific upload requests in one generic helper.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-restore --no-build
```

With Microsoft Testing Platform, `--filter-class` accepts a wildcard only at the start or end of the name
(`*Pardot*Tests` is rejected); pass `--filter-class` several times instead.

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<TestProject>"`. Docker must be available for the Testcontainers collection.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report: actions covered, the derived contract per action (variant, composed helpers/operations → statuses), codes tested, codes excluded with reasons, each external dependency with the user's choice (real, emulator, stub, skipped), stub request-log and failure-characterisation tests added, fakes registered, and test run results.
