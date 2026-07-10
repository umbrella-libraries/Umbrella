---
name: dotnet-audit-api-controller-response-contract
description: 'Read-only audit of a concrete API controller built on the Umbrella base controller hierarchy (UmbrellaGenericRepositoryApiController, UmbrellaGenericRepositoryDataServiceApiController, UmbrellaDataAccessApiController, UmbrellaDataServiceApiController, UmbrellaApiController) that derives the per-endpoint response status code contract and a testability verdict for each code. Use before generating controller integration tests.'
---

# Audit API Controller Response Contract

## Purpose

Inspect a concrete API controller and produce a normalized response contract: for every endpoint, the full set of status codes it can produce, the mechanism that produces each code, and whether that code is testable via HTTP for this specific controller. This skill is read-only. Do not create or edit files while using it.

The authoritative deep-dive is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository — read it when available. The rules below are a self-contained summary sufficient to run the audit.

## Step 1 — Identify the base controller pattern

Walk the controller's base type chain (through any application-level intermediate base, e.g. `IndyRecordsApiController`) and classify by the **most derived** Umbrella base reached:

1. `UmbrellaGenericRepositoryApiController<...>` — Pattern 1, fixed endpoint map, flags on the **controller**.
2. `UmbrellaGenericRepositoryDataServiceApiController<...>` — Pattern 2, fixed endpoint map, flags and hooks on the **backing data service** (resolve the `TRepositoryDataService` generic argument and locate its implementation, typically derived from `UmbrellaRepositoryDataService`). Note: this controller itself derives from `UmbrellaDataServiceApiController` — classify it as Pattern 2, not case 4.
3. `UmbrellaDataAccessApiController` — no fixed endpoints; each action composes the protected helpers (`ReadAllAsync`, `ReadAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`).
4. `UmbrellaDataServiceApiController<TDataService>` — no fixed endpoints; each action composes the protected `ExecuteOperationAsync` envelope over one operation on the injected data service.
5. `UmbrellaApiController` — no fixed endpoints; each action is hand-rolled using the status helper methods and/or `OperationResult` mapping.

## Step 2 — Resolve the configuration flags

Record overrides of these virtual members (on the controller for Pattern 1, on the data service for Pattern 2):

- `SlimReadEndpointEnabled`, `ReadEndpointEnabled`, `CreateEndpointEnabled`, `UpdateEndpointEnabled`, `DeleteEndpointEnabled` (Pattern 2 also: `ExistsByIdEndpointEnabled`, `TotalCountEndpointEnabled`) — a `false` value makes that endpoint return only `405`.
- `AuthorizationSlimReadChecksEnabled`, `AuthorizationReadChecksEnabled`, `AuthorizationCreateChecksEnabled`, `AuthorizationUpdateChecksEnabled`, `AuthorizationDeleteChecksEnabled` — a `false` value removes the imperative `403` for that endpoint.
- Any overridden hooks (`Before*`/`After*` methods) that return non-`null` `IOperationResult` values — each maps through the standard status table and extends the endpoint's contract (`Conflict` → `409`, `InvalidOperation` → `400`, etc.).

For `UmbrellaDataAccessApiController` actions, record the `enableAuthorizationChecks` argument passed to each helper call and any callbacks supplied. For `UmbrellaApiController` actions, enumerate every status helper call in the action body.

## Step 3 — Record entity and model facts

- `TEntityKey` type — non-`string` keys make id-binding `422` tests possible on `GET`/`DELETE`/`ExistsById`.
- Whether `TEntity` implements `IConcurrencyStamp` and the update model carries a `ConcurrencyStamp` — gates the built-in `PUT` `409`.
- Validation attributes on create/update models — gate body `422` tests.
- Entity-level validation (`IEntityValidator` rules stricter than the model annotations) — gates the save-time `400`.
- Extension-point conflicts: create/delete hooks returning `Conflict`, or `UmbrellaRepositoryDataServiceOptions.CreateExceptionFilter`/`DeleteExceptionFilter` handlers — gate `POST`/`DELETE` `409` tests.

## Step 4 — Record authorization facts

- `[Authorize]`/`[AllowAnonymous]` on the controller and actions, including policy names — gates `401` and declarative `403`.
- Which resource authorization handlers exist for the entity and whether a test identity can be constructed that they deny — gates imperative `403`.
- **The distinct grant branches inside each handler** (e.g. owner, account-manager, admin-role, operation-specific rules). List each identity class the handler distinguishes — test generation should produce one denying/passing pair per branch, not one pair total, so a bug in a secondary grant path is not missed.
- Host facts (from `dotnet-audit-aspnetcore-integration-test-readiness` if already run): `UseUmbrellaPropagateClaimsPrincipal` present, validation failure status code (below), environment name strategy.

**Determining the validation failure status code** — read `Program.cs`/`Startup` and the MVC builder extension calls; the host is in exactly one of three states, and every `422` cell in the contract depends on which:

1. **`ConfigureUmbrellaApiBehaviorOptions()` / `ConfigureUmbrellaMvcBuilderOptions()` called with defaults** → model binding/validation failures return `422` with an `UmbrellaValidationProblemDetails` body; malformed JSON at the body root (`$` model-state key) returns `400`.
2. **Called with an explicit `validationFailureStatusCode` argument** → as state 1, but tests must assert the configured code instead of `422`.
3. **Not called at all** → ASP.NET default behaviour: ALL model-state failures return `400` with a plain `ValidationProblemDetails` body (no `Code`/`TraceId`), and the malformed-JSON-root vs validation distinction disappears (both are `400`). Record this explicitly — generated tests must assert `400` + the plain body shape, and the report should recommend adopting `ConfigureUmbrellaApiBehaviorOptions()` so the app matches the standard contract.

Record the resolved state and code in the contract report; the test generators consume it verbatim.

## Step 5 — Derive the contract

- **Patterns 1 and 2**: start from the endpoint map (`SearchSlim`, `GET`, `POST`, `PUT`, `DELETE`, plus `ExistsById`/`TotalCount` for Pattern 2) and strike codes disabled by the flags recorded above.
- **`UmbrellaDataAccessApiController`**: each action's contract is the union of the built-in statuses of the helpers it calls (`ReadAllAsync` → 200/403/500; `ReadAsync` → 200/404/403/500; `CreateAsync` → 201/400/403/500; `UpdateAsync` → 200/400/404/409/403/500; `DeleteAsync` → 204/404/403/409/500), minus imperative `403` where `enableAuthorizationChecks: false`, plus `422` if the action binds input, plus `401`/declarative `403` from attributes, plus callback-driven statuses.
- **`UmbrellaDataServiceApiController`**: each action's contract is the union of the statuses of the `IOperationResult`s the composed service operation can return (for `UmbrellaRepositoryDataService`-derived services these match the Pattern 2 per-operation sets, including `405` from enablement flags and imperative `403` from authorization flags on the service; for custom services, enumerate the `IOperationResult` factory calls in the service method), plus the envelope `500` (assert the action's exact error message), plus `422` if the action binds input, plus `401`/declarative `403` from attributes.
- **`UmbrellaApiController`**: the enumeration of status helper and `OperationResult` calls in the action body is the contract. Apply these rules: in-action `Unauthorized(...)` is a `401` with an `UmbrellaProblemDetails` body testable without `[Authorize]`; `ValidationProblem(ModelState)` is `400`, not `422`; `Conflict(...)` and `ConcurrencyConflict(...)` are both `409` distinguished by `code = ConcurrencyStampMismatch`; never infer a `404` from route shape — anti-enumeration endpoints deliberately return success for missing resources; note statuses gated on external dependencies (CAPTCHA, email, payment) that require test doubles.

## Output

Return a contract report with one row per endpoint/action and status code:

| Endpoint | Status | Producing mechanism | Testable | Reason / recipe |
| --- | --- | --- | --- | --- |

Also list: the resolved base pattern, flag overrides and where they live, entity/model facts, authorization facts, host facts, and any statuses intentionally excluded (e.g. commit-time race `409`s that are real but not deterministically inducible over HTTP; `500`s requiring throwing fakes).

Do not modify files in this skill.
