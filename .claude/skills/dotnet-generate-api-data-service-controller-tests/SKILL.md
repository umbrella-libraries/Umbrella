---
name: dotnet-generate-api-data-service-controller-tests
description: 'Generate integration tests for a concrete API controller derived from UmbrellaGenericRepositoryDataServiceApiController (Pattern 2, backing controller service), covering every testable response status code per endpoint including ExistsById and TotalCount. Resolves enablement and authorization flags on the backing data service. Use after integration test infrastructure exists.'
---

# Generate Data Service Controller Integration Tests

## Purpose

Generate a complete integration test class for a concrete controller derived from `UmbrellaGenericRepositoryDataServiceApiController`, with one or more tests per testable status code per endpoint. The critical Pattern 2 difference: endpoint-enablement flags, authorization-check flags, and all lifecycle hooks live on the **backing data service** (the `TRepositoryDataService` generic argument, typically derived from `UmbrellaRepositoryDataService`), not on the controller. Always locate and read the data service implementation.

The authoritative contract is `docs\api-base-controller-endpoint-map.md` in the Umbrella repository — read it when available.

## Required inputs

As for `dotnet-generate-api-repo-controller-tests`:

1. `dotnet-audit-api-controller-response-contract` output for the target controller **and its data service**.
2. Working integration test infrastructure via `dotnet-audit-aspnetcore-integration-test-readiness` / `dotnet-scaffold-aspnetcore-integration-tests`, satisfying the response contract host requirements (claims propagation, configured `validationFailureStatusCode`, non-`Development` environment for `500` shapes, policies/handlers).
3. Anonymous, passing, and denying test identities.

## Endpoint contract

| Endpoint | Statuses |
| --- | --- |
| `GET SearchSlim` | `200`, `401`, `403`, `405`, `422`, `500` |
| `GET` | `200`, `401`, `403`, `404`, `405`, `422`, `500` |
| `POST` | `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500` |
| `PUT` | `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `DELETE` | `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `GET ExistsById` | `200`, `401`, `403`, `405`, `422`, `500` |
| `GET TotalCount` | `200`, `401`, `403`, `405`, `500` |

Strike codes the audit marked untestable and never generate tests for them.

## Pattern 2 differences from Pattern 1

Apply the recipes from `dotnet-generate-api-repo-controller-tests` with these deltas:

- **Flag resolution**: `SlimReadEndpointEnabled`, `ReadEndpointEnabled`, `CreateEndpointEnabled`, `UpdateEndpointEnabled`, `DeleteEndpointEnabled`, `ExistsByIdEndpointEnabled`, `TotalCountEndpointEnabled` and the five `AuthorizationXxxChecksEnabled` flags are read from the **data service** class. A disabled endpoint returns `405` via a `NotAllowed` operation result.
- **`ExistsById`**: assert `200` with body `true` for a seeded id and `200` with body `false` for a non-existent id. **Never** generate a `404` test — the service maps not-found to `false`. There is no imperative auth check on the default path, so `403` is declarative-policy-only.
- **`TotalCount`**: seed N entities, assert the body equals N. No `422` exists — the endpoint binds no input. `403` is declarative-policy-only.
- **Sorters/filters**: `SearchSlim` binds `SortExpressionDescriptor`/`FilterExpressionDescriptor` collections. Descriptors that cannot be converted to typed expressions are **silently skipped** — do not generate 4xx tests for invalid filter property names; assert they are ignored instead.
- **Hooks**: `Before*`/`After*` conflict and validation hooks live on the data service; the audit's extension-point findings come from there. `POST`/`DELETE` `409` remains extension-point-only.
- **Concurrency**: the `PUT` `409` stamp-rotation recipe is unchanged (create → `GET` stamp A → `PUT` rotates to B → `PUT` with A → `409` with `code = ConcurrencyStampMismatch`), because the data service routes through the same `UmbrellaRepositoryCoreDataService`.

## Test class template

```csharp
[Collection(IndyRecordsSqlServerIntegrationTestCollection.Name)]
public sealed class ArtistsControllerTests
{
	private const string ApiUrl = "/api/artists";

	private readonly IndyRecordsSqlServerWebApplicationFactory _factory;

	public ArtistsControllerTests(IndyRecordsSqlServerWebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task ExistsByIdAsync_ExistingId_Returns200True()
	{
		Artist artist = await SeedArtistAsync();

		using HttpClient client = _factory.CreateClient();
		using HttpResponseMessage response = await client.GetAsync($"{ApiUrl}/ExistsById?id={artist.Id}", TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
	}
}
```

Seed through a scoped `DbContext` from the factory, reuse the `UmbrellaProblemDetails`/`UmbrellaValidationProblemDetails` assertion helpers, and follow the shared naming convention `<Method>Async_<Scenario>_Returns<Status>`.

## Rules

- Generate tests only for codes the contract audit marked testable; document exclusions in a comment block at the top of the test class.
- Assert the validation failure status and body per the host state resolved by the contract audit (Umbrella behavior options default → `422` + `UmbrellaValidationProblemDetails`; explicit `validationFailureStatusCode` → that code; not configured → `400` + plain ASP.NET `ValidationProblemDetails`, no separate malformed-JSON-root test). Never hard-code `422` without checking.
- Satisfy earlier pipeline gates when targeting later ones (existing id + current stamp + valid model for a `PUT` `403`).
- Keep tests independent with uniquely seeded data.
- Do not weaken production authorization; use the denying identity.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-restore --no-build
```

## Output

Report: endpoints covered, status codes tested per endpoint, codes excluded with reasons, the data service class audited, helpers added, and test run results.
