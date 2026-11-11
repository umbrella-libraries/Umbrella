---
name: umbrella-dotnet-scaffold-wiremock-stub
description: 'Stub one external HTTP dependency of an Umbrella WebApplicationFactory test host with WireMock.Net, strictly opt-in after asking the user: a factory-owned server wired through the configuration override layer, path, parameter and body matching, a parsed Requests log, Reset and ClearRequests, named Given scenarios and failure helpers, recorded fixtures, and a non-parallel collection. Use only for a dependency the user has decided tests cannot call reliably.'
---

# Scaffold WireMock Stub

## Purpose

Replace ONE external HTTP dependency of the test host with a WireMock.Net server the tests control, so they decide what the dependency returns and can check exactly what the app sent. Run the skill once per dependency.

A stub is the last resort. Prefer the real service, or an emulator or container (SQL Server via Testcontainers, Azurite), wherever tests can call it reliably. A stub tests the app against your belief about the dependency, so its responses should be recorded from the real one, and live tests (`umbrella-dotnet-scaffold-live-service-tests`) should check the real service separately.

## Decide first

Do this before writing anything.

1. List every external HTTP dependency the tested code paths reach, from the server's startup code, typed `HttpClient` registrations and `appsettings*.json`.
2. For each one, give a recommendation and the reason:

| Dependency | Recommendation | Reason |
|---|---|---|
| Catalogue API (shared data) | Stub | Data changes, no isolated test instance |
| Payments sandbox | Real (sandbox) | Reliable test environment exists |
| Blob storage | Emulator (Azurite) | Real API in a container |
| Video platform | Inert config | Tested paths never call it |

3. Ask the user explicitly what to do for each dependency: real service, emulator, inert config, or stub. Wait for the answer.
4. Continue only for a dependency the user chose to stub. If they chose a real service, an emulator or a container, or inert config, stop and follow `umbrella-dotnet-configure-integration-test-isolation` (Step 3), which covers each of those.

Never stub a dependency automatically in place of a real service.

## Preflight

1. Read the repo's test README (for example `Test\README.md`) if it exists.
2. Find the factory deriving from `UmbrellaWebApplicationFactory<TProgram>` and confirm the isolation setup is in place: a virtual `GetConfigurationOverrides()` (or equivalent) added as the last configuration source, an Error-level `ILoggerProvider` recorder, and a shared host per assembly. If any is missing, run `umbrella-dotnet-configure-integration-test-isolation` first.
3. Search for an existing stub class wrapping `WireMockServer`. Extend it rather than adding a second server for the same dependency, and follow its shape for a new one.
4. Find the shared testing library (for example `IndyRecords.Web.Server.Testing`) that holds the factory. The stub, its collection name and its fixtures go there.
5. Read the app's client code for the dependency: the base address setting key, the request paths and methods, the body format, and how it reacts to a non-success status or an exception (throws, logs and returns empty, retries).

## Step 1 -- Add the package and check the dependency graph

Prefer `WireMock.Net.Minimal` over the full `WireMock.Net` package. The full package brings matchers with heavy dependencies (for example query-language and protobuf libraries); NuGet then unifies those with the app's own versions and can upgrade a library the app depends on, which breaks start-up in ways unrelated to the tests.

1. Record the app's resolved packages first:

```powershell
dotnet list "<SharedTestingProject>" package --include-transitive > before.txt
```

2. Add the reference to the shared testing library (omit `Version` and add a `<PackageVersion>` centrally under CPM, in the test-only group):

```xml
<PackageReference Include="WireMock.Net.Minimal" />
```

3. Compare again and look for any package the app also uses whose resolved version changed. Use `dotnet nuget why "<Project>" <Package>` to see what pulled it in. If a shared dependency moved, choose a WireMock.Net version that does not move it, or stop and report the conflict.
4. Boot the host (run the existing smoke test) before going further.

Use the full package only when a feature the stub needs is missing from Minimal, and repeat the graph check.

## Step 2 -- Create a stub owned by the factory

Create one sealed class per dependency, following `references\stub-class-sketch.md`:

- `private readonly WireMockServer _server = WireMockServer.Start();` on a free port;
- a property giving the address to configure, from `_server.Url`;
- `IDisposable`, disposed in the factory's `DisposeAsync` inside a `finally`, after `base.DisposeAsync()`.

Expose it on the factory and append its address to the override layer, after the base overrides, so it replaces the unreachable default:

```csharp
public CatalogueApiStub CatalogueApi { get; } = new();

protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides()
	=> base.GetConfigurationOverrides().Append(new("CatalogueApi:BaseUrl", CatalogueApi.Address));
```

Keep any credential the client sends set to an inert value; the stub ignores it.

## Step 3 -- Match on path, parameters and body

Match on the method, the path, and the part of the request that distinguishes one call from another. A request no stub matches gets a 404; document what the app does with it.

- Query-string parameters with `WithParam`: `Request.Create().WithPath("/v1/releases").UsingGet().WithParam("format", "vinyl")`.
- A JSON body with `JsonPartialMatcher` (or `JsonPathMatcher`) on the properties that matter: `WithBody(new JsonPartialMatcher("""{ "artistId": 101 }"""))`.
- A body-text `WildcardMatcher` only when the API sends every operation to one endpoint and a stable fragment of the body text is all that tells requests apart.

Do not match on volatile values such as timestamps, correlation IDs or generated tokens.

## Step 4 -- Keep a parsed request log

Expose what the app sent, in order, as a `Requests` property parsed into the form tests assert on: method, path, query parameters and body (the `StubRequest` record in the sketch):

```csharp
public IReadOnlyList<StubRequest> Requests =>
	[.. _server.LogEntries
		.Select(x => x.RequestMessage)
		.OfType<WireMock.IRequestMessage>()
		.OrderBy(x => x.DateTime)
		.Select(x => new StubRequest(x.Method, x.Path, ReadQuery(x), x.Body))];
```

Stubs often return the same response whatever the filters, so tests check that the app applied paging, filters or the visitor's language by reading the request it sent, not by expecting different results.

## Step 5 -- Reset and ClearRequests

- `Reset()` calls `_server.Reset()`: removes every mapping and forgets the log. Tests call it first, in the constructor or `InitializeAsync`.
- `ClearRequests()` calls `_server.ResetLogEntries()`: forgets the requests so far but keeps the mappings, so a test can check only the requests one action sends (for example after a page load and before a filter click).

Keep these names (`Requests`, `ClearRequests()`, `Reset()`) across stubs; the generate skills use them.

When the site caches what the stubbed service returns, set the host's cache lifetimes to a millisecond (in the configuration overrides) so each test sees its own stubs rather than an earlier test's. Don't rely on that alone where a test asserts that a second request reaches the stub: an in-memory request can finish within a millisecond, so the second one sometimes hits the cache. Wait a few tens of milliseconds between the requests, or empty the cache, and say why in a comment.

## Step 6 -- Add Given helpers for named scenarios

Name helpers after the scenario, not the HTTP detail:

```csharp
public void GivenReleases(string format, string responseJson) => ...;
public void GivenArtist(int artistId, string responseJson) => ...;
public void GivenRecordedVinylReleases() => ...; // replays a set of recorded fixtures
public void GivenReleasesFail(HttpStatusCode statusCode) => ...;
```

Use `Given…Fails(status)` to characterise what the app does today when the dependency fails. Assert the current behaviour, including what it logs (`factory.Errors`), and say in a remark that the test records existing behaviour that may want changing, so a later fix updates the test deliberately.

## Step 7 -- Record fixtures from a non-production endpoint

Responses written by hand drift from the real shape. Record them instead:

1. Ask the user which non-production endpoint to record from. Never record from production.
2. Run a WireMock.Net proxy in front of it with `ProxyAndRecordSettings { Url = "<endpoint>", SaveMapping = false }`, so every request reaches the real service.
3. Drive the requests through the app, for example by running the live tests with the dependency's address pointed at the proxy.
4. Save each response body from the proxy's `LogEntries` as JSON. Remove volatile or diagnostic blocks (correlation IDs, timings, query cost) and anything personal or secret.
5. Store them in the shared testing library, for example `CatalogueApiResponses\vinyl-releases.json`, copied to every referencing project's output:

```xml
<None Update="CatalogueApiResponses\*.json" CopyToOutputDirectory="PreserveNewest" />
```

6. Read them with `Path.Combine(AppContext.BaseDirectory, "CatalogueApiResponses", fileName)`. Keep file names lowercase; Linux CI is case-sensitive.
7. Document the recording steps in the test README.

## Step 8 -- Run stub tests in a non-parallel collection

The stub is shared by every test using the host, so tests that set mappings run one at a time. Keep the name in the shared library:

```csharp
public static class CatalogueApiCollection
{
	public const string Name = "Catalogue API";
}
```

xUnit only honours a `[CollectionDefinition]` declared in the test assembly itself, so re-declare it in each test project that uses the stub (server, browser):

```csharp
[CollectionDefinition(CatalogueApiCollection.Name, DisableParallelization = true)]
public sealed class CatalogueApiCollectionDefinition;
```

Tests use `[Collection(CatalogueApiCollection.Name)]` and call `Reset()` and `Errors.Clear()` before arranging.

## Step 9 -- Add a first test

Add one test that proves the wiring: arrange a `Given…`, call the endpoint, assert the response, that no errors were logged, and the request the stub received. Further endpoint tests come from the generate skills, such as `umbrella-dotnet-generate-custom-api-controller-tests` and `umbrella-dotnet-generate-e2e-journey-tests`.

## Rules

- One dependency per run, and only after the user chose a stub for it.
- The stub's address reaches the app only through the override layer, never by editing app settings files.
- Fixtures come from a non-production endpoint and contain no secrets or personal data.
- Never let a stubbed test pass on an unmatched request: assert the request log or the response content.
- Remind the user that the stub does not prove the real service still behaves this way; recommend `umbrella-dotnet-scaffold-live-service-tests` for that.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<TestProject>"`. Confirm the host still starts, the first stub test passes, and the dependency graph check showed no shared package version changes.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## References

- `references\stub-class-sketch.md`

## Output

Report:

- the dependency stubbed, the user's decision for each dependency listed, and the setting key replaced;
- the package used (Minimal or full) and the dependency-graph comparison result;
- the stub class, its Given helpers and the collection name, and the projects that re-declare it;
- fixtures recorded, the endpoint they came from, and what was stripped;
- validation results, and the recommendation to add live tests for the real service.
