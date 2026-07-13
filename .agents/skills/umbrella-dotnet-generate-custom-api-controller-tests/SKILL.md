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
3. Anonymous, passing, and denying test identities (one denying/passing pair per grant branch the resource handler distinguishes), plus test doubles for any external dependency that gates a status code.

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
7. **External dependencies gate codes** (CAPTCHA → `400`, email senders, payment gateways) — only testable with controllable fakes registered in the factory; never call the real service.
8. **The `500` contract is standard**: catch-all filters with `returnValue: !IsDevelopment`; shape assertions need a non-`Development` host and a throwing fake. Optional coverage.
9. **Actions returning `OperationResult(...)` from logic services** map through the standard table (`GenericSuccess` → `200`, `Created` → `201`, `NoContent` → `204`, `NotFound` → `404`, `Conflict`/`ConcurrencyConflict` → `409`, `Forbidden` → `403`, `NotAllowed` → `405`, `InvalidOperation` → `400`, `GenericFailure` → `500`). Drive the service into each state, or substitute a fake returning each `IOperationResult` when the real state is impractical to arrange.

## Variant C — deriving contracts from `ExecuteOperationAsync` composition

Each action passes a delegate invoking one operation on the injected `TDataService`, plus an endpoint-specific `500` error message. The envelope contributes the mechanics (cancellation check, `IOperationResult` → HTTP mapping, `500` on unhandled exceptions outside Development); **the status codes come from the `IOperationResult`s the composed service operation can return**:

- For services extending `UmbrellaRepositoryDataService`, per-operation status sets match the Pattern 2 endpoint map: `FindAllSlimAsync` → `200`/`403`; `FindByIdAsync` → `200`/`404`/`403`; `CreateAsync` → `201`/`400`/`403`; `UpdateAsync` → `200`/`400`/`404`/`409`-concurrency/`403`; `DeleteAsync` → `204`/`404`/`409`/`403`; `ExistsByIdAsync` → `200` `true`/`false` (never `404`); `FindTotalCountAsync` → `200`. Endpoint-enablement flags on the service → `405`; `AuthorizationXxxChecksEnabled => false` removes the imperative `403`.
- For custom service implementations, enumerate the `IOperationResult` factory calls in the service method — that enumeration is the operation's status set, mapped through the standard table (as Variant B rule 9, but at the service layer).

The action's contract is the union of: the composed operation's statuses, the envelope `500` (assert the action's exact error message string), `422`/configured code if the action binds input, and `401`/declarative `403` from attributes. `TDataService` may implement only a subset of `IGenericDataService` or a fully custom interface — derive from the methods that exist, and check the service class (not the controller) for enablement/authorization flags when it extends `UmbrellaRepositoryDataService`.

## Test generation (all variants)

Use the per-status recipes from `umbrella-dotnet-generate-api-repo-controller-tests` — they describe mechanisms, not endpoints, and apply directly: seeded happy paths via a scoped `DbContext`, `401` anonymous (attribute-gated) vs in-action, `403` per identity class, `404` non-existent key, `409` stamp rotation asserting `code = ConcurrencyStampMismatch`, `400`/`422` per the resolved host state, optional `500` via throwing fake + non-`Development` host.

Shared conventions: test class per controller in the SQL Server Testcontainers collection, `<Method>Async_<Scenario>_Returns<Status>` naming, and the `Umbrella.Testing.AspNetCore.Http` problem-details assertion extensions:

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

Docker must be available for the Testcontainers collection.

## Output

Report: actions covered, the derived contract per action (variant, composed helpers/operations → statuses), codes tested, codes excluded with reasons, fakes registered, and test run results.
