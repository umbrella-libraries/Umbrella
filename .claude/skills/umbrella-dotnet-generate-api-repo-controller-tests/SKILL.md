---
name: umbrella-dotnet-generate-api-repo-controller-tests
description: 'Generate integration tests for a concrete API controller derived from UmbrellaGenericRepositoryApiController (Pattern 1, direct repository), covering every testable response status code per endpoint: success paths, 401/403 authorization, 404, 405 disabled endpoints, 409 concurrency via stamp rotation, 400/422 validation, and optional 500. Use after integration test infrastructure exists.'
---

# Generate Generic Repository Controller Integration Tests

## Purpose

Generate a complete integration test class for a concrete controller derived from `UmbrellaGenericRepositoryApiController`, with one or more tests per testable status code per endpoint. Tests must trace the documented contract, not guesses.

The authoritative contract is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository — read it when available.

## Required inputs

1. Run `umbrella-dotnet-audit-api-controller-response-contract` against the target controller to obtain the per-endpoint contract and testability verdicts. Endpoint-enablement and authorization-check flags live **on the controller** for this pattern.
2. Working integration test infrastructure — factories, collections, test authentication — via `umbrella-dotnet-audit-aspnetcore-integration-test-readiness` and `umbrella-dotnet-scaffold-aspnetcore-integration-tests` (plus `umbrella-dotnet-scaffold-test-project` if no test project exists). Confirm the response contract host requirements from that skill: claims principal propagation, the configured `validationFailureStatusCode`, a non-`Development` environment when asserting `500` shapes, and registered authorization policies/handlers.
3. A test authentication approach that can issue anonymous, passing, and denying identities.

## Endpoint contract

The five endpoints and their effective statuses (before flag adjustments from the audit):

| Endpoint | Statuses |
| --- | --- |
| `GET SearchSlim` | `200`, `401`, `403`, `405`, `422`, `500` |
| `GET` | `200`, `401`, `403`, `404`, `405`, `422`, `500` |
| `POST` | `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500` |
| `PUT` | `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `DELETE` | `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |

Strike codes the audit marked untestable. Never generate a test for a struck code.

## Test class template

One test class per controller, in the SQL Server Testcontainers collection (concurrency and entity-validation tests need real database semantics):

```csharp
[Collection(IndyRecordsSqlServerIntegrationTestCollection.Name)]
public sealed class AlbumsControllerTests
{
	private const string ApiUrl = "/api/albums";

	private readonly IndyRecordsSqlServerWebApplicationFactory _factory;

	public AlbumsControllerTests(IndyRecordsSqlServerWebApplicationFactory factory)
	{
		_factory = factory;
	}
}
```

Seed data through a scoped `DbContext` from the factory, not through the API, except where a recipe explicitly round-trips (e.g. capturing a `ConcurrencyStamp`):

```csharp
private async Task<Album> SeedAlbumAsync()
{
	using IServiceScope scope = _factory.Services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<IndyRecordsDbContext>();

	var album = new Album { Name = $"Test Album {Guid.NewGuid():N}" };
	_ = dbContext.Albums.Add(album);
	_ = await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

	return album;
}
```

## Per-status recipes

### Success paths

- `GET` `200`: seed, request by id, assert the mapped model fields. Capture `ConcurrencyStamp` here for the `PUT` tests.
- `SearchSlim` `200`: seed ≥1 entity; assert `Items`, `TotalCount`, `PageNumber`, `PageSize`, `MoreItems`. Add a clamping assertion — `pageSize=500` returns at most 50 items and never an error; do not generate 4xx tests for out-of-range numeric paging.
- `POST` `201`: valid body; assert the create-result model (`Id`, plus `ConcurrencyStamp` where applicable); verify persistence with a follow-up `GET`. The `Location` header is empty by design — do not assert a URL.
- `PUT` `200`: create → `GET` (capture stamp) → `PUT` with current stamp; assert the result carries a **rotated** stamp; re-`GET` to confirm persistence.
- `DELETE` `204`: seed → delete → assert empty body → `GET` returns `404`.

### Error paths

- `401`: only when the audit found `[Authorize]` (or a fallback policy). Send each request unauthenticated; the body is produced by the authentication handler — typically empty, not `UmbrellaProblemDetails`.
- `403` (imperative): only when the endpoint's `AuthorizationXxxChecksEnabled` flag is `true` **and** a denying identity exists. Seed an entity the denying identity fails the resource handler for (e.g. owned by another user); assert `403` with an `UmbrellaProblemDetails` body. When the resource handler has **multiple grant branches** (owner, account manager, admin role, …), generate one denying/passing pair **per identity class** the handler distinguishes — the contract audit enumerates them — so a bug in a secondary grant path is caught; a single pair only exercises one path through the handler. For `SearchSlim`, the denied entity must appear in the requested page. For declarative-policy `403`s, authenticate as a user failing the policy; the body is empty.
- `404`: `GET`/`PUT`/`DELETE` with a well-formed, non-existent key.
- `405`: only for endpoints the controller disables via `XxxEndpointEnabled => false`; assert `UmbrellaProblemDetails` with status `405`. Do not generate `405` tests for enabled endpoints.
- `409` on `PUT` (requires `IConcurrencyStamp`): create → `GET` (stamp A) → `PUT` succeeds (rotates to B) → `PUT` again with stamp A → assert `409` and `problemDetails.Code == "ConcurrencyStampMismatch"`. Do not attempt to test the commit-time race — it is real but not deterministically inducible over HTTP.
- `409` on `POST`/`DELETE`: only when the audit found a hook or exception-handler conflict path (e.g. duplicate-name guard, "in use" guard). Seed the conflicting state and assert `409` without the concurrency code.
- `422`: model-state failures — non-numeric/missing `pageNumber`/`pageSize` on `SearchSlim`, malformed `id` (non-`string` keys) on `GET`/`DELETE`, data-annotation violations on `POST`/`PUT` bodies. The status and body depend on the host state resolved by the contract audit: Umbrella behavior options with defaults → assert `422` + `UmbrellaValidationProblemDetails`; explicit `validationFailureStatusCode` → assert that code + `UmbrellaValidationProblemDetails`; **behavior options not configured** → assert `400` + plain ASP.NET `ValidationProblemDetails` (no `Code`/`TraceId`), and do not generate a separate malformed-JSON-root test (it is indistinguishable by status in that state). Never hard-code `422` without checking.
- `400`: malformed JSON root on `POST`/`PUT` (e.g. `{"unclosed`) — only a distinct test when the Umbrella behavior options are configured (`$` model-state key rule); entity-level validation failures that pass model annotations but violate `IEntityValidator` rules, when the audit found such rules.
- `500` (optional): only with a non-`Development` host and a deliberately throwing replacement registration (e.g. a repository fake). Most suites reasonably omit this; when generated, assert the `UmbrellaProblemDetails` shape.

## Assertion helpers

Generate (or reuse) helpers that deserialize `application/problem+json` bodies:

```csharp
private static async Task<UmbrellaProblemDetails> ReadProblemDetailsAsync(HttpResponseMessage response)
{
	Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

	UmbrellaProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<UmbrellaProblemDetails>(TestContext.Current.CancellationToken);
	Assert.NotNull(problemDetails);

	return problemDetails;
}
```

Use `UmbrellaValidationProblemDetails` for `400`/`422` responses. For the concurrency `409`, assert `problemDetails.Code` equals `HttpProblemCodes.ConcurrencyStampMismatch`.

## Rules

- Generate tests only for codes the contract audit marked testable; note excluded codes and reasons in a comment block at the top of the test class.
- Name tests `<Method>Async_<Scenario>_Returns<Status>` (e.g. `PutAsync_StaleConcurrencyStamp_Returns409`).
- Satisfy every earlier pipeline gate when targeting a later one — `PUT` evaluates 404 → 409 (stamp) → hooks/400 → 403 → save, so a `403` test must use an existing id, the current stamp, and a valid model.
- Keep tests independent: unique seeded data per test, no ordering assumptions.
- Do not weaken production authorization to make tests pass; use the denying/passing identities instead.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-restore --no-build
```

Docker must be available for the Testcontainers collection.

## Output

Report: endpoints covered, status codes tested per endpoint, codes excluded with reasons, helpers added, and test run results.
