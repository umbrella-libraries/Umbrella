---
name: dotnet-generate-api-controller-tests
description: 'Generate integration tests for hand-rolled API actions on controllers derived directly from UmbrellaApiController, deriving per-action response status contracts by enumerating the status helper and OperationResult calls in each action body. Use for orchestration, Identity-backed, or service-composed endpoints with no repository helper usage.'
---

# Generate API Controller Integration Tests

## Purpose

Generate integration tests for concrete controllers derived directly from `UmbrellaApiController` — hand-rolled actions that orchestrate services, ASP.NET Identity, or external systems and produce responses via the base status helper methods (`Ok`, `Created`, `NoContent`, `BadRequest`, `NotFound`, `Unauthorized`, `Forbidden`, `Conflict`, `ConcurrencyConflict`, `MethodNotAllowed`, `TooManyRequests`, `InternalServerError`, `ValidationProblem`) and/or the `OperationResult`/`OperationResultFailure` mappers. There is no fixed contract: **the enumeration of these calls in the action body is the contract.**

The authoritative reference is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository (see its "Custom Endpoints on the Base Controller Hierarchy" section) — read it when available.

## Required inputs

1. `dotnet-audit-api-controller-response-contract` output — the per-action enumeration with testability verdicts.
2. Working integration test infrastructure via `dotnet-audit-aspnetcore-integration-test-readiness` / `dotnet-scaffold-aspnetcore-integration-tests`, satisfying the response contract host requirements.
3. Anonymous, passing, and denying test identities, plus test doubles for any external dependency that gates a status code.

## Derivation rules

Apply these when reading each action body — they change which tests are valid:

1. **In-action `Unauthorized(...)` is not middleware `401`.** It returns `401` with an `UmbrellaProblemDetails` body and is testable without `[Authorize]` (e.g. anonymous access to conditionally-protected content, locked own-account checks). Assert the problem-details body for this variant and an empty body for the middleware challenge — they are different tests.
2. **`ValidationProblem(ModelState)` from an action body is `400`**, not `422`. The configured `validationFailureStatusCode` (default `422`) applies only to pre-action model binding/annotation failures — and only when the host registers the Umbrella behavior options at all; the contract audit resolves which of the three host states applies. Identity-style flows that copy `IdentityResult` errors into model state therefore produce `400`. In the unconfigured host state, pre-action and in-action validation failures share status `400` and must be distinguished by body content, not status code.
3. **`Conflict(...)` and `ConcurrencyConflict(...)` are both `409`** — distinguish with `problemDetails.Code == "ConcurrencyStampMismatch"`. Hand-rolled update paths typically pair a manual stamp comparison with `catch (UmbrellaConcurrencyException)`; the stamp-rotation recipe (create → read stamp → mutate → replay stale stamp) tests both.
4. **Duplicate-resource guards often exist twice** — a pre-save lookup and a save-result error-code re-check that closes the race window. Both are `409 Conflict`; a duplicate-seed test exercises the first, and the second is usually untestable over HTTP. Record it as excluded rather than attempting a race.
5. **Never infer statuses from route shape.** Anti-enumeration endpoints deliberately return success (e.g. `204`) whether or not the resource exists — generate tests asserting *that* behaviour, and only generate `404` tests where the action actually returns `NotFound`.
6. **Do not assume base-class conventions.** Hand-rolled actions may clamp pagination to different bounds, add ad-hoc guards (`id < 1` → `400`), or return `401` for domain states (locked account). The action body is the source of truth.
7. **External dependencies gate codes.** A `400` behind CAPTCHA verification, flows behind email senders or payment gateways — these are only testable when the test host substitutes the dependency with a controllable fake. Register the fake in the factory; never call the real service.
8. **The `500` contract is standard**: catch-all filters with `returnValue: !IsDevelopment`. Shape assertions need a non-`Development` host; triggering one needs a throwing fake. Optional coverage.
9. **Actions returning `OperationResult(...)`** from logic services map through the standard table (`GenericSuccess` → `200`, `Created` → `201`, `NoContent` → `204`, `NotFound` → `404`, `Conflict`/`ConcurrencyConflict` → `409`, `Forbidden` → `403`, `NotAllowed` → `405`, `InvalidOperation` → `400`, `GenericFailure` → `500`). Test by driving the service into each state, or substitute the service with a fake returning each `IOperationResult` when the real state is impractical to arrange.

## Test generation

Group tests per action; for each status in the enumerated contract, generate one test that arranges the specific branch:

```csharp
[Collection(IndyRecordsSqlServerIntegrationTestCollection.Name)]
public sealed class ContentPageControllerTests
{
	private const string ApiUrl = "/api/contentpage";

	private readonly IndyRecordsSqlServerWebApplicationFactory _factory;

	public ContentPageControllerTests(IndyRecordsSqlServerWebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task GetAsync_AnonymousAccessToProtectedPage_Returns401WithProblemDetails()
	{
		ContentPage page = await SeedContentPageAsync(allowAnonymousAccess: false);

		using HttpClient client = _factory.CreateClient(); // no identity
		using HttpResponseMessage response = await client.GetAsync($"{ApiUrl}?urlSegment={page.UrlSegment}", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		UmbrellaProblemDetails problemDetails = await ReadProblemDetailsAsync(response); // in-action 401 carries a body
	}
}
```

Reuse the shared conventions: SQL Server Testcontainers collection, seeding via a scoped `DbContext`, `<Method>Async_<Scenario>_Returns<Status>` naming, and the `UmbrellaProblemDetails`/`UmbrellaValidationProblemDetails` assertion helpers.

## Rules

- Generate tests only for statuses the audit enumerated and marked testable; document exclusions (race re-checks, dependency-gated codes without fakes, optional `500`s) in a comment block.
- One assertion focus per test: status code, body shape, and the observable side effect that distinguishes the branch (e.g. anti-enumeration endpoints return the same status either way — assert the side effect via seeded state, such as no change to the database).
- Keep tests independent with uniquely seeded data; do not weaken production authorization or skip CAPTCHA-style guards in production code — substitute at the DI layer in the factory only.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-restore --no-build
```

## Output

Report: actions covered, the enumerated contract per action, codes tested, codes excluded with reasons, fakes registered, and test run results.
