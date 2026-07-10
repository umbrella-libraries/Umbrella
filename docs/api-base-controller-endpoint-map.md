# API Base Controller Endpoint Map

This map covers the public API endpoints exposed by the base controllers in the `Umbrella.AspNetCore.WebUtilities.Mvc` namespace:

- `UmbrellaGenericRepositoryApiController` (Pattern 1 — direct repository)
- `UmbrellaGenericRepositoryDataServiceApiController` (Pattern 2 — backing controller service)

The map describes the reusable base controller contract, including supported virtual hooks/callbacks and custom data-service implementations. Derived controllers and services that introduce response codes outside this contract should add their own endpoint-specific attributes.

It also provides per-endpoint, per-status-code integration testing guidance, with each code traced to the exact code path that produces it, so that test generation can determine which codes are testable for a given concrete controller and how to trigger them.

Three further base controllers sit above these in the hierarchy and expose **no endpoints of their own**: `UmbrellaApiController` (the root, providing the status-code helper methods, problem-details shapes and `IOperationResult` mapping), `UmbrellaDataAccessApiController` (adding the protected `ReadAllAsync`/`ReadAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync` helpers that the Pattern 1 generic controller composes), and `UmbrellaDataServiceApiController<TDataService>` (the service-pattern equivalent: an unconstrained `Lazy<TDataService>` plus the protected `ExecuteOperationAsync` overload pair that the Pattern 2 generic controller's endpoints compose). Concrete applications derive custom-shaped endpoints from them directly. They have no fixed endpoint map — see [Custom Endpoints on the Base Controller Hierarchy](#custom-endpoints-on-the-base-controller-hierarchy) for how to derive a per-action contract and generate tests for them.

## Controller-Level Responses

| Controller | Class-Level Responses |
| --- | --- |
| `UmbrellaGenericRepositoryApiController` | `401`, `403`, `405`, `422`, `500` |
| `UmbrellaGenericRepositoryDataServiceApiController` | `401`, `403`, `405`, `500` |

## Consolidated Endpoint Map

| Endpoint | Controllers | Inputs | Method-Level Responses | Effective Responses |
| --- | --- | --- | --- | --- |
| `GET SearchSlim` | Both | `pageNumber`, `pageSize`, `sorters`, `filters`, `filterCombinator`, `CancellationToken` | Repository: `200`<br>Data service: `200`, `422` | Repository: `200`, `401`, `403`, `405`, `422`, `500`<br>Data service: `200`, `401`, `403`, `405`, `422`, `500` |
| `GET` | Both | `id`, `CancellationToken` | Repository: `200`, `404`<br>Data service: `200`, `404`, `422` | Repository: `200`, `401`, `403`, `404`, `405`, `422`, `500`<br>Data service: `200`, `401`, `403`, `404`, `405`, `422`, `500` |
| `POST` | Both | Body model, `CancellationToken` | Repository: `201`, `400`, `409`<br>Data service: `201`, `400`, `409`, `422` | Repository: `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500`<br>Data service: `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500` |
| `PUT` | Both | Body model, `CancellationToken` | Repository: `200`, `400`, `404`, `409`<br>Data service: `200`, `400`, `404`, `409`, `422` | Repository: `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500`<br>Data service: `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `DELETE` | Both | `id`, `CancellationToken` | Repository: `204`, `404`, `409`<br>Data service: `204`, `404`, `409`, `422` | Repository: `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500`<br>Data service: `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `GET ExistsById` | `UmbrellaGenericRepositoryDataServiceApiController` only | `id`, `CancellationToken` | `200`, `422` | `200`, `401`, `403`, `405`, `422`, `500` |
| `GET TotalCount` | `UmbrellaGenericRepositoryDataServiceApiController` only | `CancellationToken` | `200` | `200`, `401`, `403`, `405`, `500` |

`409` on `POST` is extension-point driven. It can be returned by supported virtual hooks/callbacks or custom data-service implementations, but is not produced by the default built-in create path when those hooks return `null`. `409` on `DELETE` is extension-point driven with one built-in exception: a commit-time optimistic concurrency failure (another request modified or deleted the row between load and commit) surfaces as `409`.

## Input Type Differences

`SearchSlim` differs slightly between the two controllers:

| Controller | Sorters Input | Filters Input |
| --- | --- | --- |
| `UmbrellaGenericRepositoryApiController` | `[FromQuery] SortExpression<TEntity>[]? sorters` | `[FromQuery] FilterExpression<TEntity>[]? filters` |
| `UmbrellaGenericRepositoryDataServiceApiController` | `[FromQuery] IEnumerable<SortExpressionDescriptor>? sorters` | `[FromQuery] IEnumerable<FilterExpressionDescriptor>? filters` |

All other shared endpoints are conceptually aligned, with model and key type names differing by generic controller design.

## Status Code Production Mechanics

Every status code in the contract is produced by one of a small number of mechanisms. Understanding these is a prerequisite for deciding whether a code is testable on a given concrete controller.

### The two request pipelines

**Pattern 1 (`UmbrellaGenericRepositoryApiController`)**: action method → `UmbrellaDataAccessApiController.ReadAllAsync/ReadAsync/CreateAsync/UpdateAsync/DeleteAsync` → `IUmbrellaRepositoryCoreDataService` (`UmbrellaRepositoryCoreDataService`) → `IGenericDbRepository` → `IDataAccessUnitOfWork.CommitAsync`. Endpoint-enablement flags (`SlimReadEndpointEnabled` etc.) live **on the controller**.

**Pattern 2 (`UmbrellaGenericRepositoryDataServiceApiController`)**: action method → `TRepositoryDataService` (typically derived from `UmbrellaRepositoryDataService`, which itself derives from `UmbrellaRepositoryCoreDataService`) → same repository/unit-of-work path. Endpoint-enablement flags and all virtual hooks live **on the data service**, not the controller. `ExistsByIdEndpointEnabled` and `TotalCountEndpointEnabled` also exist here. The controller derives from `UmbrellaDataServiceApiController<TRepositoryDataService>`, whose `ExecuteOperationAsync` envelope its endpoints compose — the HTTP contract is unchanged by that layering.

Both pipelines return `IOperationResult` values that `UmbrellaApiController.OperationResult(...)` maps to HTTP results:

| `OperationResultStatus` | HTTP result | Body type |
| --- | --- | --- |
| `GenericSuccess` | `200` | Typed model (or empty) |
| `Created` | `201` | Typed model (or empty) |
| `NoContent` | `204` | Empty |
| `InvalidOperation` (with `ValidationResults`) | `400` via `ValidationProblem(...)` | `ValidationProblemDetails` |
| `InvalidOperation` (message only) | `400` | `UmbrellaValidationProblemDetails` |
| `NotFound` | `404` | `UmbrellaProblemDetails` |
| `NotAllowed` | `405` | `UmbrellaProblemDetails` |
| `Conflict` | `409` | `UmbrellaProblemDetails` |
| `ConcurrencyConflict` | `409` with `code = HttpProblemCodes.ConcurrencyStampMismatch` | `UmbrellaProblemDetails` |
| `Forbidden` | `403` | `UmbrellaProblemDetails` |
| `GenericFailure` | `500` | `UmbrellaProblemDetails` |

### 401 — authentication middleware only

Neither base controller carries `[Authorize]`. A `401` is produced solely by the ASP.NET Core authentication middleware challenging an unauthenticated request. It is therefore only testable when the **concrete** controller (or action, or a global fallback authorization policy) applies `[Authorize]`. The response body is whatever the authentication handler emits — typically empty, **not** `UmbrellaProblemDetails`.

### 403 — two distinct mechanisms

1. **Declarative**: `[Authorize(Policy = ...)]` / role requirements on the concrete controller failing for an *authenticated* user → middleware `403` (empty body).
2. **Imperative (resource-based)**: when `AuthorizationSlimReadChecksEnabled` / `AuthorizationReadChecksEnabled` / `AuthorizationCreateChecksEnabled` / `AuthorizationUpdateChecksEnabled` / `AuthorizationDeleteChecksEnabled` is `true` (the default), `UmbrellaRepositoryCoreDataService` calls `IUmbrellaAuthorizationService.AuthorizeAsync(User, entity, policyName)` with the policy names from `UmbrellaRepositoryDataServiceOptions` (`CorePolicyNames.Read/Create/Update/Delete` by default). A failed check returns `OperationResult.Forbidden` → `403` with an `UmbrellaProblemDetails` body.

The imperative path has two hard infrastructure dependencies. `UmbrellaRepositoryCoreDataService.User` reads `ClaimsPrincipal.Current`, which ASP.NET Core does **not** populate — the app (and the test host) must register the `UseUmbrellaPropagateClaimsPrincipal()` middleware (or equivalent) so `HttpContext.User` flows to `Thread.CurrentPrincipal`. If it is missing, the `User` getter throws and the request surfaces as a `500`, not a `403`. Secondly, the four `CorePolicyNames` policies must be registered with handlers (see the `dotnet-scaffold-resource-auth-handler` skill); an unregistered policy name makes `IAuthorizationService.AuthorizeAsync` throw → `500`.

### 405 — endpoint-enablement flags

- Pattern 1: `SlimReadEndpointEnabled` / `ReadEndpointEnabled` / `CreateEndpointEnabled` / `UpdateEndpointEnabled` / `DeleteEndpointEnabled` overridden to `false` on the **controller** short-circuits the action with `MethodNotAllowed("Unsupported Endpoint")`.
- Pattern 2: the same five flags plus `ExistsByIdEndpointEnabled` and `TotalCountEndpointEnabled` overridden to `false` on the **data service** return `OperationResult.NotAllowed(...)` → `405`.

A `405` is only testable for endpoints that a concrete controller/service has actually disabled. (Requesting a wrong HTTP verb on an existing route also yields a routing-level `405`, but with no `UmbrellaProblemDetails` body — that is a framework behaviour, not part of this contract.)

### 422 vs 400 — model binding and validation

`ConfigureUmbrellaApiBehaviorOptions()` (called via `ConfigureUmbrellaMvcBuilderOptions()`) replaces the `[ApiController]` `InvalidModelStateResponseFactory`:

- Model-state errors keyed by `$` (malformed JSON at the root of the request body, or a root value that cannot be converted) → **400** with `UmbrellaValidationProblemDetails`, always.
- All other model-state errors (data-annotation failures on a successfully parsed body, query/route binding failures such as a non-numeric `id` or `pageNumber`, missing required parameters) → the configurable `validationFailureStatusCode`, which defaults to **422**, with `UmbrellaValidationProblemDetails`. Both extension methods accept this parameter, so an application can opt into e.g. plain `400` for all validation failures.

This happens **before the action executes**. If the application does not call `ConfigureUmbrellaApiBehaviorOptions()` at all, all of these cases fall back to the ASP.NET default `400` — with plain `ValidationProblemDetails` bodies rather than `UmbrellaValidationProblemDetails`, and no malformed-JSON-root distinction — so every `422` assertion in the contract becomes untestable; treat the config as a mandatory prerequisite (or generate tests asserting the plain-`400` contract). If the application overrides `validationFailureStatusCode`, generated tests must assert that configured value wherever this guidance says `422`. Test generation must resolve which of these three states the host is in before emitting any validation tests.

A second, post-binding `400` source exists inside the pipeline: entity-level validation. `GenericDbRepository.SaveAsync` runs `IEntityValidator.ValidateEntityAsync` when `RepoOptions.ValidateEntity` is `true` (the default); failures come back as a `GenericFailure` save result carrying `ValidationResults`, which `UmbrellaRepositoryCoreDataService.CreateAsync/UpdateAsync` convert to `InvalidOperation` + `ValidationResults` → controller `ValidationProblem(...)` → **400**. This is how a request that passes model annotations but violates entity rules fails.

### 409 — conflict and concurrency, traced to the repository

There are two `OperationResultStatus` values that map to `409`: `Conflict` and `ConcurrencyConflict` (the latter adds `code = HttpProblemCodes.ConcurrencyStampMismatch` to the problem details — assert on this to distinguish them).

The **built-in, deterministic** `409` exists only on `PUT`, and only when `TEntity` implements `IConcurrencyStamp`:

1. `UmbrellaRepositoryCoreDataService.UpdateAsync` loads the entity by `model.Id`, then compares `entity.ConcurrencyStamp` against `model.ConcurrencyStamp` **before mapping**. A mismatch immediately returns `ConcurrencyConflict` → `409`. This is the primary, integration-testable path.
2. `catch (UmbrellaConcurrencyException)` in the same method also maps to `ConcurrencyConflict`. `UmbrellaConcurrencyException` is thrown by `GenericDbRepository.ThrowIfConcurrencyTokenMismatch` (when `RepoOptions.ThrowIfConcurrencyTokenMismatch` is `true`, the default) if the entity's stamp diverges from the EF-tracked original value — this catches hooks/mappers that rewrite the stamp after load.

3. A genuine commit-time race is also a `409`: the actual database save is deferred to `IDataAccessUnitOfWork.CommitAsync`, which rethrows EF's `DbUpdateConcurrencyException` as `UmbrellaConcurrencyException` (all other commit failures are wrapped in `UmbrellaDataAccessException`). Both `UpdateAsync` and `DeleteAsync` in `UmbrellaRepositoryCoreDataService` catch it and return `ConcurrencyConflict`. Note that this path cannot be induced deterministically through HTTP alone (it requires interleaving a competing write between the entity load and the commit within a single request), so integration tests should rely on the stale-stamp recipe below — which exercises the same contract — and leave the race path to service-level tests if desired.

`409` on `POST` is extension-point driven only; on `DELETE` the extension points are the practical triggers (the built-in race path above is real but not deterministically testable over HTTP):

- A `BeforeCreateMappingModelToEntityAsync` / `BeforeCreateEntityAsync` / `BeforeDeleteEntityAsync` override returning `OperationResult.Conflict(...)` (e.g. duplicate-name checks before create).
- `UmbrellaRepositoryDataServiceOptions.CreateExceptionFilter` + `HandleCreateExceptionAsync` (or the delete equivalents) mapping database exceptions — e.g. unique-index violations — to a `Conflict` result. The defaults are no-ops (`filter => false`).
- A fully custom Pattern 2 data-service implementation.

If the concrete controller/service implements none of these, `POST` `409` is **not testable** and `DELETE` `409` is not testable via HTTP, so tests should not be generated for them.

### 404 — not-found lookups

`GET`, `PUT` and `DELETE` return `NotFound` → `404` when the entity for the supplied `id` (or `model.Id` for `PUT`) does not exist. Always testable with a well-formed but non-existent key. Note that `ExistsById` never returns `404` — the data service maps the repository's not-found result to a `200` with body `false`.

### 500 — exception catch-alls, environment-sensitive

Every action and every core-service method wraps its work in `catch (Exception exc) when (Logger.WriteError(exc, ..., returnValue: !IsDevelopment))`. The filter only *catches* when the host environment is **not** `Development`; in `Development` the exception propagates to the developer exception page. Two consequences for tests:

1. To assert the contractual `500` + `UmbrellaProblemDetails` shape, the test host must run with a non-`Development` environment name (e.g. `Production` or a dedicated `IntegrationTest` environment).
2. Triggering a `500` requires inducing a genuine failure — e.g. replacing the repository/data-service registration with a throwing fake, or breaking database connectivity. This is optional coverage; most suites reasonably omit it.

In Pattern 2, service-level exceptions are wrapped in `UmbrellaDataAccessException` and rethrown to the controller's catch, so the observable behaviour is the same `500`.

### Success codes

- `200` — `GET`, `PUT`, `SearchSlim`, `ExistsById` (body `true`/`false`), `TotalCount` (body integer count).
- `201` — `POST`; body is the create-result model carrying at minimum `Id` (and `ConcurrencyStamp` where applicable). The `Location` header is empty (`Created("", content)`).
- `204` — `DELETE` success; empty body.

Also note `ClampPaginationParameters`: `pageNumber` is floored at `1` and `pageSize` clamped to `1–50`, so out-of-range *numeric* paging values can never produce an error status — assert the clamping behaviour (e.g. `pageSize=500` returns at most 50 items) rather than expecting a `4xx`.

## Test Host Prerequisites

For the response-code contract above to be observable, the `WebApplicationFactory` (or equivalent) host must provide:

1. **Umbrella MVC configuration** — `ConfigureUmbrellaMvcBuilderOptions()` (or at minimum `ConfigureUmbrellaApiBehaviorOptions()` + `ConfigureUmbrellaMvcOptions()` for the Pattern 1 sorter/filter model binders). Without it, `422` collapses to `400`. Match the host application's `validationFailureStatusCode` (default `422`) and assert that value in validation tests.
2. **Claims principal propagation** — `UseUmbrellaPropagateClaimsPrincipal()` in the pipeline. Without it, every imperative authorization check throws → `500` instead of `403`/`200`.
3. **Authentication** — a test authentication handler that can issue (a) no identity, (b) an authenticated identity that passes resource checks, and (c) an authenticated identity that fails them. `401` additionally requires `[Authorize]` on the concrete controller.
4. **Authorization policies** — the `CorePolicyNames.Create/Read/Update/Delete` policies (or the custom names configured on `UmbrellaRepositoryDataServiceOptions`) registered with the entity's resource authorization handlers.
5. **A real relational database** (e.g. SQL Server Testcontainers) when testing `409` concurrency and entity-validation `400`s, so that stamp rotation and save semantics behave as in production.
6. **A non-`Development` environment name** when asserting `500` response shapes.

Assertion conventions: all error responses in the contract are `application/problem+json`; `400`/`422` bodies deserialize to `UmbrellaValidationProblemDetails`, all other errors to `UmbrellaProblemDetails` (both carry a correlation/trace id). The concurrency `409` carries `code = HttpProblemCodes.ConcurrencyStampMismatch`.

Do not build this host from scratch: the repository already provides the infrastructure. Use the `dotnet-audit-aspnetcore-integration-test-readiness` skill to assess the target server project, then `dotnet-scaffold-aspnetcore-integration-tests` (with `dotnet-scaffold-test-project` where a test project does not yet exist) to generate the `WebApplicationFactory` classes, xUnit collections, test authentication handler, SQL Server Testcontainers wiring, and configuration overrides from `Umbrella.Testing.AspNetCore`. Generated endpoint tests should target that scaffolding and only extend it where a prerequisite above (e.g. a denying test identity, a non-Development environment name) is missing.

## Per-Endpoint Testing Guidance

Terminology used below: *auth checks enabled* = the relevant `AuthorizationXxxChecksEnabled` flag left at its default `true` (on the controller for Pattern 1, on the data service for Pattern 2); *denying handler* = a resource authorization handler that can fail for a test identity you can authenticate as.

### `GET SearchSlim`

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `200` | Load page → optional auth-all check → map to slim models | Always. Seed ≥1 entity; assert items, `TotalCount`, `PageNumber`, `PageSize`, `MoreItems`. Also assert pagination clamping (`pageSize=500` → ≤50 items). |
| `401` | Authentication middleware challenge | Only if concrete controller has `[Authorize]`. Send unauthenticated request. |
| `403` | `AuthorizeAllAsync` over the loaded page fails for ≥1 entity → `Forbidden` | Auth checks enabled + denying handler. Seed an entity the test user may not read; request a page containing it. Not testable if `AuthorizationSlimReadChecksEnabled` is `false` and no declarative policy denies. |
| `405` | Endpoint-enabled flag `false` (controller in Pattern 1, data service in Pattern 2) | Only for controllers/services that disable it. Assert `UmbrellaProblemDetails` with 405. |
| `422` | Model-state error: non-numeric/missing `pageNumber` or `pageSize`; malformed sorter/filter query values | Always (both patterns declare it effectively). E.g. `?pageNumber=abc&pageSize=10`. Requires prerequisite 1. |
| `500` | Unhandled exception in load/map | Optional; throwing-fake repository + non-Development host. |

Sorter/filter caveat: in Pattern 2, descriptors that cannot be converted into typed expressions are silently skipped (no error); do not generate 4xx tests for bad filter *property names* — assert they are ignored instead.

### `GET` (single item)

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `200` | Entity found → auth check → mapped model | Always. Seed entity, GET by id, assert mapped fields (capture `ConcurrencyStamp` here for PUT tests). |
| `401` | Middleware challenge | Only with `[Authorize]` on concrete controller. |
| `403` | `AuthorizeAsync(User, entity, ReadPolicyName)` fails → `Forbidden` | Auth checks enabled + denying handler. Seed an entity owned by another user; GET as non-owner. |
| `404` | Repository returns `null` for id → `NotFound` | Always. GET with a well-formed, non-existent id. |
| `405` | `ReadEndpointEnabled = false` | Only when disabled. |
| `422` | `id` binding failure (e.g. `id=abc` for an `int`/`Guid` key) or missing `id` | Testable unless `TEntityKey` is `string` (nothing to fail binding). Requires prerequisite 1. |
| `500` | Unhandled exception | Optional, as above. |

### `POST`

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `201` | Model mapped → hooks pass → auth check passes → `SaveEntityAsync` returns `Created` → unit-of-work commit | Always. POST valid model; assert body `Id` (and `ConcurrencyStamp` if applicable), then verify persistence via GET. `Location` header is empty by design. |
| `400` | (a) malformed JSON root (`$` model-state key); (b) `null`/absent body reaching the action → `InvalidOperation`; (c) entity-level validation failure via `IEntityValidator` (`RepoOptions.ValidateEntity`, default `true`) → `InvalidOperation` + `ValidationResults` → `ValidationProblem` | (a) always: send `{"unclosed`. (c) only when the entity has validation rules stricter than the API model — craft a body that passes annotations but fails entity validation. |
| `401` | Middleware challenge | Only with `[Authorize]`. |
| `403` | `AuthorizeAsync(User, entity, CreatePolicyName)` fails | Auth checks enabled + denying handler (e.g. handler restricts creation to a role the test user lacks). Not testable when `AuthorizationCreateChecksEnabled` is `false`. |
| `405` | `CreateEndpointEnabled = false` | Only when disabled. |
| `409` | Extension points only: create hooks returning `Conflict`, or `CreateExceptionFilter`/`HandleCreateExceptionAsync` mapping e.g. unique-index violations | Only when the concrete controller/service implements one. E.g. duplicate-name guard: seed an entity, POST a duplicate, assert 409. Otherwise **do not generate this test**. |
| `422` | Data-annotation failure on the parsed body | Testable whenever the create model has validation attributes (required/length/range/etc.). Send a parseable body violating one. Requires prerequisite 1. |
| `500` | Unhandled exception; commit failures (`UmbrellaDataAccessException`) | Optional. |

### `PUT`

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `200` | Entity loaded → stamp matches → hooks pass → auth passes → save `GenericSuccess` → commit | Always. Create, GET to capture current `ConcurrencyStamp`, PUT with it; assert updated body (including the **rotated** stamp) and re-GET to confirm persistence. |
| `400` | Same three sources as POST (malformed JSON root; `InvalidOperation` hooks; entity-level validation failure on save) | As per POST. |
| `401` | Middleware challenge | Only with `[Authorize]`. |
| `403` | `AuthorizeAsync(User, entity, UpdatePolicyName)` fails — note this runs **after** the load, stamp check, and mapping | Auth checks enabled + denying handler. Update another user's entity (send its correct current stamp so the 409 check doesn't mask the 403). |
| `404` | `FindByIdAsync(model.Id)` returns `null` | Always. PUT a valid model with a non-existent `Id`. |
| `405` | `UpdateEndpointEnabled = false` | Only when disabled. |
| `409` | Built-in: `entity.ConcurrencyStamp != model.ConcurrencyStamp` pre-mapping check in `UmbrellaRepositoryCoreDataService.UpdateAsync`; secondarily `UmbrellaConcurrencyException` from the repository stamp guard or a commit-time race | Only when `TEntity` implements `IConcurrencyStamp` (i.e. the update model carries a stamp). Recipe: create → GET (capture stamp A) → PUT succeeds (stamp rotates to B) → PUT again with stamp A → assert `409` and `code = ConcurrencyStampMismatch`. **Not testable** (via the default path) when the entity has no concurrency stamp. The commit-time race path returns the same `409` but cannot be induced deterministically over HTTP — the stale-stamp recipe covers the contract. |
| `422` | Data-annotation failure on the parsed body (including a missing required `ConcurrencyStamp`/`Id` where annotated) | As per POST. Requires prerequisite 1. |
| `500` | Unhandled exception; non-concurrency commit failures (`UmbrellaDataAccessException`) | Optional. |

Ordering note for test design: the PUT pipeline evaluates **404 → 409 (stamp) → hooks/400 → 403 → save/commit**. When targeting a later code, satisfy every earlier gate (existing id, current stamp, valid model) so the intended status is the one observed.

### `DELETE`

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `204` | Entity loaded → auth passes → `BeforeDeleteEntityAsync` returns `null` → delete + commit → `NoContent` | Always. Create, DELETE, assert 204 and empty body, then GET → 404 (or `ExistsById` → `false` in Pattern 2). |
| `401` | Middleware challenge | Only with `[Authorize]`. |
| `403` | `AuthorizeAsync(User, entity, DeletePolicyName)` fails | Auth checks enabled + denying handler; delete another user's entity. |
| `404` | `FindByIdAsync(id)` returns `null` | Always. DELETE a non-existent id. |
| `405` | `DeleteEndpointEnabled = false` | Only when disabled. |
| `409` | Extension points: `BeforeDeleteEntityAsync` returning `Conflict` (e.g. "in use" guards), or `DeleteExceptionFilter`/`HandleDeleteExceptionAsync` mapping FK-violation exceptions. Built-in: a commit-time concurrency race (`UmbrellaConcurrencyException`) also maps to `409` | Only when an extension point is implemented — e.g. seed a dependent record so the guard fires. The built-in race path is not deterministically inducible over HTTP; otherwise **do not generate this test**. |
| `422` | `id` binding failure | Unless `TEntityKey` is `string`. Requires prerequisite 1. |
| `500` | Unhandled exception (including unguarded FK violations at commit) | Optional. |

### `GET ExistsById` (Pattern 2 only)

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `200` | Repository `ExistsByIdAsync` → `NoContent` mapped to `true`, `NotFound` mapped to `false` | Always. Assert body `true` for a seeded id and `false` for a non-existent id — **never** expect a `404` from this endpoint. |
| `401` | Middleware challenge | Only with `[Authorize]`. |
| `403` | Declarative policy on the concrete controller only — the default path performs **no imperative auth check** | Only via `[Authorize(Policy=...)]` denial; there is no entity-level 403 to test here. |
| `405` | `ExistsByIdEndpointEnabled = false` on the data service | Only when disabled. |
| `422` | `id` binding failure | Unless `TEntityKey` is `string`. Requires prerequisite 1. |
| `500` | Unhandled exception (incl. the service's guard against unexpected repository statuses) | Optional. |

### `GET TotalCount` (Pattern 2 only)

| Code | Traced trigger | Testable when / recipe |
| --- | --- | --- |
| `200` | Repository `FindTotalCountAsync` → count | Always. Seed N entities, assert body equals N. |
| `401` | Middleware challenge | Only with `[Authorize]`. |
| `403` | Declarative policy only — no imperative check on the default path | Only via `[Authorize(Policy=...)]` denial. |
| `405` | `TotalCountEndpointEnabled = false` on the data service | Only when disabled. |
| `500` | Unhandled exception | Optional. |

No `422` exists for `TotalCount` — the endpoint binds no input, so no model-state failure is possible (consistent with its attribute set).

## Custom Endpoints on the Base Controller Hierarchy

`UmbrellaApiController`, `UmbrellaDataAccessApiController` and `UmbrellaDataServiceApiController<TDataService>` define no endpoints, so no fixed endpoint map exists for them. Applications typically insert their own intermediate abstract controller (adding `[Route("api/[controller]")]`, a mapper, shared error messages) and build custom-shaped actions on top — e.g. a singleton-settings controller whose `GET` takes no `id`, an Identity-backed account controller, or orchestration endpoints with no repository at all. A test-generation skill must therefore **derive each action's status contract from the action's code**, using the rules below. Everything needed already exists in this document:

- **All of [Status Code Production Mechanics](#status-code-production-mechanics) applies hierarchy-wide**, not just to the two generic controllers: the `OperationResultStatus` → HTTP mapping table, the status-helper problem-details shapes, the 400/`validationFailureStatusCode` model-state factory (any `[ApiController]`-derived action with bindable inputs can produce a `422`), the 401/403 middleware behaviour, and the `returnValue: !IsDevelopment` catch-filter convention for `500`s.
- **The [Test Host Prerequisites](#test-host-prerequisites) apply unchanged.**
- **The per-code test recipes in [Per-Endpoint Testing Guidance](#per-endpoint-testing-guidance) are reusable** once you know which codes an action can produce — the recipes describe how to trigger a mechanism, not a specific endpoint.

### `UmbrellaDataAccessApiController` protected helper contracts

Custom actions on this controller compose the same protected helpers the Pattern 1 generic controller uses, so each helper call contributes a fixed, traced status set to the action's contract:

| Helper | Built-in statuses | Conditions |
| --- | --- | --- |
| `ReadAllAsync` | `200`, `403`, `500` | `403` only when `enableAuthorizationChecks: true` (parameter, default `true`) and the read policy denies ≥1 loaded entity. Pagination is clamped 1–50 before the core call. |
| `ReadAsync` | `200`, `404`, `403`, `500` | `404` when the entity/id lookup returns `null`; `403` per `enableAuthorizationChecks`. |
| `CreateAsync` | `201`, `400`, `403`, `500` | `400` from a `null` body reaching the action, or entity-level `IEntityValidator` failures on save; `403` per `enableAuthorizationChecks`. No built-in `409`. |
| `UpdateAsync` | `200`, `400`, `404`, `409`, `403`, `500` | `409` (`ConcurrencyStampMismatch` code) from the pre-mapping stamp comparison, the repository stamp guard, or a commit-time race — requires the entity to implement `IConcurrencyStamp`. |
| `DeleteAsync` | `204`, `404`, `403`, `409`, `500` | `409` only from a commit-time concurrency race (not deterministically testable over HTTP) or a `beforeDeleteEntityAsyncCallback` returning `Conflict`. |

An action's derived contract is the **union** of: the statuses of every helper it calls (minus `403` where it passes `enableAuthorizationChecks: false`), plus `422` if it binds any input, plus `401`/declarative `403` from its `[Authorize]` attributes, plus any status produced by callbacks it supplies (a callback returning any `IOperationResult` maps through the standard table) and by code in the action before/after the helper calls. Example: a singleton-settings `GET` calling `ReadAsync(1, ..., enableAuthorizationChecks: false)` under `[Authorize(Policy = ...)]` yields `200`, `404` (only if the seed row can be absent), `401`, `403` (declarative only), `500` — and no `422`, since nothing is bound.

### `UmbrellaDataServiceApiController` operation contracts

Custom actions on this controller compose the protected `ExecuteOperationAsync` overload pair: each call takes a delegate invoking one operation on the injected `TDataService`, an endpoint-specific `500` error message, and optional log state. The envelope contributes a fixed mechanical contract — cancellation check, `IOperationResult` → HTTP mapping via the standard table, and the `returnValue: !IsDevelopment` catch producing the `500` — while the **status codes themselves come from the `IOperationResult`s the service operation can return**:

- For services extending `UmbrellaRepositoryDataService`, the per-operation status sets match the Pattern 2 endpoint map (e.g. `FindByIdAsync` → `200`/`404`/imperative `403`; `UpdateAsync` → `200`/`400`/`404`/`409`-concurrency; a disabled endpoint flag → `405`).
- For custom service implementations, enumerate the `IOperationResult` factory calls in the service method — that enumeration is the operation's status set, mapped through the standard table.

An action's derived contract is the union of: the composed operation's statuses, plus the envelope `500`, plus `422` (or the configured `validationFailureStatusCode`) if the action binds input, plus `401`/declarative `403` from its `[Authorize]` attributes. `TDataService` is unconstrained, so services implementing only a subset of `IGenericDataService` (e.g. a read+update singleton-settings service) or fully custom interfaces are first-class — derive from the methods that actually exist.

### Deriving contracts for hand-rolled actions (`UmbrellaApiController`)

For actions that orchestrate services or ASP.NET Identity directly, enumerate the status-helper and `OperationResult`/`OperationResultFailure` calls in the action body — that enumeration *is* the method-level contract. Rules that real-world usage shows a skill must apply:

1. **In-action `Unauthorized(...)` is not middleware 401.** It returns `401` *with* an `UmbrellaProblemDetails` body and is testable without `[Authorize]` (e.g. anonymous access to protected content, or own-account-locked checks). Distinguish it from the empty-bodied middleware challenge when generating assertions.
2. **`ValidationProblem(ModelState)` from an action body is `400`**, not `422` — the custom factory only governs pre-action model binding. Identity-style flows (`IdentityResult` errors copied into model state) therefore produce `400`.
3. **Both `Conflict(...)` and `ConcurrencyConflict(...)` are `409`** — assert `code = ConcurrencyStampMismatch` to target the concurrency variant. Hand-rolled update paths typically pair a manual stamp comparison with `catch (UmbrellaConcurrencyException)`, both → `ConcurrencyConflict`.
4. **Duplicate-resource guards often exist twice** — a pre-save lookup *and* a save-result error-code check that closes the race window. Both return `409 Conflict`; a duplicate-seed test exercises the first, and the second is usually untestable over HTTP.
5. **Never infer statuses from route shape.** Anti-enumeration endpoints deliberately return success (e.g. `204`) for missing resources; only generate a `404` test where the action actually returns `NotFound`.
6. **Do not assume base-class conventions.** Hand-rolled actions may clamp pagination differently, add their own guards (e.g. `id < 1` → `400`), or return `401` for states like a locked own-account. The action body is the source of truth.
7. **External dependencies gate some codes.** Statuses that depend on external services (CAPTCHA verification → `400`, email senders, payment gateways) are only testable if the test host substitutes those dependencies with controllable fakes — record each such status's dependency alongside the derived contract.
8. **The `500` contract is identical**: catch-all filters with `returnValue: !IsDevelopment`, so shape assertions need a non-Development host, and triggering one requires a throwing fake.

## Testability Decision Checklist

When generating an integration test suite for a concrete controller derived from one of the two **generic** controllers, resolve these questions first — they determine which cells of the matrices above apply. (For controllers derived directly from `UmbrellaApiController` or `UmbrellaDataAccessApiController`, derive the per-action contract using [Custom Endpoints on the Base Controller Hierarchy](#custom-endpoints-on-the-base-controller-hierarchy) instead, then apply questions 1, 3, 5, 7, 8 and 9 below to each action.)

1. Does the concrete controller (or a global policy) apply `[Authorize]`? → gates all `401` tests, and declarative `403` tests.
2. Which `AuthorizationXxxChecksEnabled` flags are overridden to `false` (controller for Pattern 1, data service for Pattern 2)? → removes the corresponding imperative `403` tests.
3. Do resource authorization handlers exist for the entity, and can a test identity be constructed that they deny? → required for any imperative `403` test.
4. Which `XxxEndpointEnabled` flags are overridden to `false`? → those endpoints get exactly one test (`405`) and lose all others; enabled endpoints get no `405` test.
5. Does `TEntity` implement `IConcurrencyStamp` (and the update model carry the stamp)? → gates the built-in `PUT 409` test.
6. Do any create/delete hooks or `UmbrellaRepositoryDataServiceOptions` exception handlers return `Conflict`? → gates `POST`/`DELETE` `409` tests; absent these, generate none.
7. Is `TEntityKey` a non-`string` type? → gates the `422` id-binding tests on `GET`/`DELETE`/`ExistsById`.
8. Do the create/update models carry validation attributes, and does the entity have `IEntityValidator` rules beyond them? → gates body `422` and entity-validation `400` tests respectively.
9. Is the test host configured per the prerequisites section (behavior options, claims propagation, policies, non-Development environment)? → gates `422` fidelity, imperative `403`s, and `500` shape assertions.

## Intended Test-Generation Skill Breakdown

This document is the contract source for four planned skills, one per base controller, each generating integration tests for concrete controllers:

1. **`UmbrellaGenericRepositoryApiController` (Pattern 1)** — consume the endpoint matrices and the testability decision checklist directly; enablement and auth-check flags live on the controller.
2. **`UmbrellaGenericRepositoryDataServiceApiController` (Pattern 2)** — same matrices, but resolve the enablement/auth-check flags and hooks on the backing data service (the `TRepositoryDataService` generic argument), not the controller.
3. **`UmbrellaDataAccessApiController`** — derive each action's contract from the protected-helper table and union rule in the custom-endpoints section.
4. **`UmbrellaApiController`** — derive each action's contract using the hand-rolled-action derivation rules in the custom-endpoints section.

All four share the status-code production mechanics, the test host prerequisites (delegated to the scaffolding skills referenced there), and the per-code test recipes. Each skill must spot-verify the concrete controller against the code rather than trusting this document blindly — the checklist questions force most of that verification. Real-world usages of the two endpoint-less base controllers can be found in consuming applications such as the ERM repository (`Erm.Admin.Web.Server`).
