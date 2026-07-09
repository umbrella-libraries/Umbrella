---
name: dotnet-generate-data-access-controller-tests
description: 'Generate integration tests for custom-shaped API actions on controllers derived from UmbrellaDataAccessApiController, deriving per-action response status contracts from the protected ReadAllAsync/ReadAsync/CreateAsync/UpdateAsync/DeleteAsync helper calls each action composes. Use for controllers whose endpoint shapes do not fit the generic repository controllers.'
---

# Generate Data Access Controller Integration Tests

## Purpose

Generate integration tests for concrete controllers derived from `UmbrellaDataAccessApiController`. These controllers define no fixed endpoints — each action composes the protected data-access helpers with custom signatures (e.g. a singleton-settings `GET` with no `id` parameter, a shaped search). The status contract must be **derived per action** from the helper calls, then tested with the same recipes as the generic controllers.

The authoritative contract is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository (see its "Custom Endpoints on the Base Controller Hierarchy" section) — read it when available.

## Required inputs

1. `dotnet-audit-api-controller-response-contract` output for the target controller — the per-action helper inventory and derived contract.
2. Working integration test infrastructure via `dotnet-audit-aspnetcore-integration-test-readiness` / `dotnet-scaffold-aspnetcore-integration-tests`, satisfying the response contract host requirements.
3. Anonymous, passing, and denying test identities.

## Deriving each action's contract

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
- `422` (or the configured `validationFailureStatusCode`) if the action binds any input — id parameters, query values, body models;
- `401` and declarative `403` from its `[Authorize]` attributes;
- any status produced by callbacks the action supplies (a callback returning an `IOperationResult` maps through the standard status table — `Conflict` → `409`, `InvalidOperation` → `400`, `Forbidden` → `403`, etc.);
- any status produced by code in the action before or after the helper calls (audit these lines like `UmbrellaApiController` hand-rolled actions).

Worked example — a singleton-settings controller:

```csharp
[Authorize(IndyRecordsPolicyNames.SystemSettingsManagement)]
public class SystemSettingsController : IndyRecordsDataAccessApiController
{
	[HttpGet]
	public Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
		=> ReadAsync<SystemSettings, int, ISystemSettingsRepository, RepoOptions, SystemSettingsModel>(
			1, _repository, cancellationToken, enableAuthorizationChecks: false);
}
```

Derived `GET` contract: `200`, `401` (attribute), `403` (declarative policy only), `404` (only if the settings row can be absent — check seeding), `500`. **No** `422` — nothing is bound. Do not generate imperative-`403` or `422` tests for this action.

## Test generation

Use the per-status recipes from `dotnet-generate-generic-repo-controller-tests` — they describe mechanisms, not endpoints, and apply directly:

- happy paths seeded via a scoped `DbContext`;
- `401` anonymous (attribute-gated), `403` denying identity (imperative, when checks enabled) or policy-failing identity (declarative, empty body);
- `404` non-existent key (only for actions whose lookups can miss);
- `409` stamp rotation for update actions on `IConcurrencyStamp` entities, asserting `code = ConcurrencyStampMismatch`;
- `400` malformed JSON root / entity-level validation; `422` per bound inputs, asserting the configured `validationFailureStatusCode`;
- `500` optional, throwing fake + non-`Development` host.

Use the shared test class shape ([Collection] on the SQL Server Testcontainers collection), naming convention `<Method>Async_<Scenario>_Returns<Status>`, and the `UmbrellaProblemDetails`/`UmbrellaValidationProblemDetails` assertion helpers.

## Rules

- The action signature is the routing truth: custom shapes mean custom URLs — read the `[HttpGet("...")]`/`[Route]` attributes rather than assuming REST conventions.
- Generate tests only for codes in the derived contract that the audit marked testable; document exclusions in a comment block.
- Satisfy earlier gates when targeting later codes (the helper pipelines evaluate not-found → concurrency → hooks → authorization → save).
- Keep tests independent with uniquely seeded data; do not weaken production authorization.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-restore --no-build
```

## Output

Report: actions covered, the derived contract per action (helpers → statuses), codes tested, codes excluded with reasons, and test run results.
