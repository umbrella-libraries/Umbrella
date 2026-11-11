---
name: umbrella-dotnet-configure-integration-test-isolation
description: 'Harden an existing Umbrella WebApplicationFactory test host so tests never reach a real external service by accident: configuration overrides as the last source without user secrets, a per-dependency decision (real service, emulator or container, inert, stub), no writes to shared systems, an Error-level log recorder, Docker-gated Fact/Theory attributes that fail in CI, a stop-once hosted-service decorator, and one shared host per assembly for expensive hosts. Use after scaffolding integration tests and before adding stubs, browser or live tests.'
---

# Configure Integration Test Isolation

## Purpose

Make an existing integration test host deterministic and safe. The host must use only the settings the tests give it, never call an external service the user has not approved, report errors it logs behind successful responses, and skip or fail predictably depending on whether Docker is available.

This skill builds on `umbrella-dotnet-scaffold-aspnetcore-integration-tests`. It changes the host, not individual tests. Stubs for individual dependencies come from `umbrella-dotnet-scaffold-wiremock-stub`; browser hosting from `umbrella-dotnet-scaffold-browser-test-host`; real-service checks from `umbrella-dotnet-scaffold-live-service-tests`.

## Preflight

1. Read the repo's test README (for example `Test\README.md`) if it exists, for a map of test projects and helpers.
2. Find the factory deriving from `UmbrellaWebApplicationFactory<TProgram>` (directly, or through `UmbrellaSqlServerAzuriteWebApplicationFactory<TProgram, TDbContext>` or a project-specific container factory). If none exists, run `umbrella-dotnet-scaffold-aspnetcore-integration-tests` first and stop here.
3. Note where the factory lives. When more than one test project (server, browser, live) will share the host, it belongs in a shared library such as `IndyRecords.Web.Server.Testing` (`IsTestProject=false`, `IsPackable=false`), referenced by each test project.
4. Search for helpers that already do each job below, and reuse them instead of adding new ones:
   - a configuration override method or test configuration class;
   - an `ILoggerProvider` that records Error-level entries;
   - a Docker availability probe and attributes deriving from `FactAttribute`/`TheoryAttribute` that set `Skip`;
   - an `IHostedService` decorator that guards `StopAsync`;
   - `[assembly: AssemblyFixture(...)]` and `[assembly: CaptureConsole]` declarations.
5. Read every `appsettings*.json` the test environment loads, and the server's startup code, to list each setting that points at an external resource: connection strings, base URLs, API keys, identity providers, SMTP, telemetry, queues, storage.
6. Check whether the entry assembly has a `UserSecretsId` and whether the test environment is `Development` (which loads user secrets).

## Step 1 -- Decide each external dependency with the user

Prefer real integration wherever tests can call it reliably: the real service, or an emulator or container (SQL Server via Testcontainers, Azurite). A stub is only for a dependency that cannot be integrated with reliably (no test environment, rate limits, non-deterministic data, side effects, cost).

List every external dependency found in Preflight with a recommendation and its reason, for example:

| Dependency | Setting keys | Recommendation | Reason |
|---|---|---|---|
| SQL Server | `ConnectionStrings:IndyRecordsDb` | Emulator (Testcontainers) | Real engine, disposable |
| Blob storage | `ConnectionStrings:Blobs` | Emulator (Azurite) | Real API, disposable |
| Catalogue API | `CatalogueApi:BaseUrl`, `CatalogueApi:Key` | Stub (`umbrella-dotnet-scaffold-wiremock-stub`) | Shared data changes, no test instance |
| Video platform | `VideoApi:BaseUrl`, `VideoApi:Key` | Inert config (unreachable) | Not needed by the tested paths |
| Telemetry | `ApplicationInsights:ConnectionString` | Inert config (null) | Never needed in tests |

Ask the user explicitly what to do for each one: real service, emulator or container, inert config, or stub. Do not point a dependency at an unreachable address, or stub it, without that answer. Record the decisions in the test configuration class's comments and in the test README, together with the app-feature choices from Step 3.

One rule is not a choice: tests never write to a shared external system (a production or shared test database, index, queue, mailbox, payment account or content store). Explain this rule to the user when presenting the table, so they know why a "real" choice is limited to read-only use or to an instance the tests own.

## Step 2 -- Add overrides as the last configuration source

Add the overrides in `ConfigureWebHostBuilder` with `ConfigureAppConfiguration`, after removing the user-secrets source:

```csharp
protected override void ConfigureWebHostBuilder(IWebHostBuilder builder)
{
	ArgumentNullException.ThrowIfNull(builder);
	base.ConfigureWebHostBuilder(builder);

	_ = builder.ConfigureAppConfiguration((_, configuration) =>
	{
		foreach (var userSecrets in configuration.Sources.OfType<JsonConfigurationSource>().Where(x => x.Path == "secrets.json").ToArray())
			_ = configuration.Sources.Remove(userSecrets);

		_ = configuration.AddInMemoryCollection(GetDistinctConfigurationOverrides());
	});
}

// Later entries replace earlier ones for the same key, as later configuration sources do.
private Dictionary<string, string?> GetDistinctConfigurationOverrides()
{
	var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
	foreach (var (key, value) in GetConfigurationOverrides())
		overrides[key] = value;
	return overrides;
}

protected virtual IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides()
	=> IndyRecordsTestConfiguration.ExternalServiceOverrides();
```

Explain both choices in comments, because they are easy to undo by mistake:

- **Last source.** `UseSetting` writes host settings, which are the first configuration source. Any key also set in `appsettings.{Environment}.json`, environment variables or user secrets beats it, so a connection string in `appsettings.Development.json` would silently point the tests at a real database. A source added last wins.
- **No user secrets.** The `Development` environment loads the developer's user secrets, which CI agents do not have. A test that passes only because of a local secret fails in CI, and may call a real service with a real key. Every setting the tests need must come from the overrides.

Keep `GetConfigurationOverrides()` virtual and composable: derived factories append with `base.GetConfigurationOverrides().Concat(...)` or `.Append(...)`, and the last value for a key wins. That is how a stub address replaces the unreachable default, and how a live factory replaces the stub.

If `Program` reads a value from `builder.Configuration` before `builder.Build()`, confirm with a factory self-test that the override reached it. Values read that early can miss sources added by the factory; read them through options at resolve time, or also set the key with `UseSetting` and make sure no file the test environment loads sets it.

## Step 3 -- Apply each decision

Every value below reaches the host through the override layer from Step 2, never by editing app settings files.

**Real service.** Point the dependency at an instance the tests may use: a test tenant, a sandbox account or a dedicated test environment, never production. Read its settings from environment variables in the overrides, supplied locally by the developer and in CI by secret pipeline variables (or a key vault task) mapped explicitly into the test step's `env`. When the settings are absent, skip the tests that need it with a clear reason locally, and fail in CI the way the Docker gate in Step 5 does. Keep calls read-only unless the instance belongs to the tests and they clean up after themselves. For checks that should run only on demand, use `umbrella-dotnet-scaffold-live-service-tests` instead.

**Emulator or container.** Start it beside the database containers with Testcontainers, in the factory's `InitializeAsync`, and pass its connection string or address through the overrides: for example Redis (`Testcontainers.Redis`), the Azure Service Bus emulator, Cosmos DB emulator, a mail-catcher such as an SMTP sink, or any service with an official container image. Pin the image tag, wait for readiness with a wait strategy, dispose it in `DisposeAsync`, and gate it on Docker like the other containers. Prefer the vendor's emulator image over a hand-written fake.

**Stub.** Run `umbrella-dotnet-scaffold-wiremock-stub` after this skill; it appends the stub's address to the overrides.

**Inert config.** Point the endpoint at an unreachable address. Put the replacement values in one static class in the shared testing library:

```csharp
internal static class IndyRecordsTestConfiguration
{
	// Loopback port 9 (discard) refuses connections, so a stray call fails fast instead of timing out.
	public const string UnreachableAddress = "http://127.0.0.1:9/";

	public static IEnumerable<KeyValuePair<string, string?>> ExternalServiceOverrides()
	{
		yield return new("VideoApi:BaseUrl", UnreachableAddress);
		yield return new("VideoApi:Key", "integration-test");
		yield return new("Smtp:DeliveryMethod", "SpecifiedPickupDirectory");
		yield return new("Smtp:PickupDirectory", Path.Combine(Path.GetTempPath(), "indyrecords-integration-test-mail"));
		yield return new("ApplicationInsights:ConnectionString", null);
	}
}
```

Rules for the values:

- Replace every key the loaded `appsettings*.json` files set for that service, including keys and secrets, so a real credential is never sent anywhere.
- Give options that are validated at startup or first use a well-formed inert value (`integration-test`, or valid base64 when the app decodes it), so pages do not fail on validation rather than on the code under test.
- Leave the dependencies chosen as real or emulator alone, apart from pointing them at the test instance.

**Safety rule (fixed).** Whatever the user chose, the host must not write to a shared external system. Turn off features that push, sync, publish or index into an external service (`services.Configure<TOptions>(...)` in `ConfigureServices`) unless that service is an emulator, container or instance the tests own. This is not negotiable per dependency; explain it in a comment beside the switches. Look beyond options: a library can write from an event handler that no option turns off (usage tracking recorded in a DAM whenever content that references an asset is saved, for example). Find these by watching a stub's or proxy's request log for writes (`POST`, `PUT`, `PATCH`, `DELETE`) the tests didn't ask for, and replace the service that sends them with one that does nothing (`services.RemoveAll<TService>()`, then add the no-op).

**App features (a choice).** Some features are not external dependencies but still affect tests: cache warm-up, scheduled jobs, startup indexing of the local database, background refresh. List the ones the host runs at startup, recommend keeping or disabling each (for example disable a warm-up that fails against an empty database and slows startup; keep a job the tested pages rely on), and ask the user. Record each choice in the decisions with the dependency table.

## Step 4 -- Record Error-level log entries

Add an `ILoggerProvider` that records entries at `LogLevel.Error` and above, expose it on the factory, and register it in `ConfigureLogging` after `base.ConfigureLogging(logging)`:

```csharp
public ErrorLogRecorder Errors { get; } = new();

protected override void ConfigureLogging(ILoggingBuilder logging)
{
	base.ConfigureLogging(logging);
	_ = logging.AddProvider(Errors);
}
```

Use the sketch in `references\isolation-helpers.md` when none exists. Tests that check a response clear the recorder first and assert it is empty after the request, because many services log a failure and still return 200:

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.True(_factory.Errors.Logged.Count is 0, $"The site logged errors:{Environment.NewLine}{string.Join(Environment.NewLine, _factory.Errors.Logged)}");
```

Tests that run concurrently share the recorder, so tests asserting on it run in a non-parallel collection or tolerate entries from other tests.

Background work that outlives its test shows up here too. When a test's request starts a background task (a content event handler, a fire-and-forget sync) and the task logs after the test has ended, writing to the test output fails ("There is no currently active test"), the component catches that as a failure and logs it as an error, and the recorder holds it against whichever test runs next. Make the work finish inside the test that starts it, or turn it off in the host when it is a write the safety rule forbids anyway. Never hide such entries by clearing the recorder late.

## Step 5 -- Gate container tests on Docker, but never skip in CI

Add a Docker probe and `DockerFact`/`DockerTheory` attributes (named for the app, such as `IndyRecordsFact`) following `references\isolation-helpers.md`:

- Probe `docker version --format {{.Server.Os}}` with a timeout, retried (a busy machine can make the first call slow, and a false negative silently skips every test), and require `linux` because the SQL Server and Azurite images are Linux images.
- Treat Docker as required when `TF_BUILD` is `True` (Azure Pipelines). Add the CI system's own variable if it differs, such as `GITHUB_ACTIONS`. Then the attributes never skip, and the factory's `InitializeAsync` throws a clear message when no engine answers, so a broken agent fails the build instead of reporting a green run that tested nothing.
- Pass `[CallerFilePath]` and `[CallerLineNumber]` through to the xUnit v3 base constructor.
- Give the factory a `protected virtual bool ShouldStart => DockerAvailability.IsAvailable;` and return early from `InitializeAsync` when it is false, so a skipped run does not pay the startup cost.

## Step 6 -- Stop hosted services once, where both stops collide

Apply this only when a hosted service fails while the factory is disposed. When `Program` calls `app.Run()` or `host.Run()`, the factory stops the host and `Run()` stops it again at the same time, so every hosted service's `StopAsync` can run twice concurrently. Services whose shutdown is not idempotent then throw, and the run fails during disposal.

Wrap only the offending service (identified from the disposal stack trace) with the stop-once decorator in `references\isolation-helpers.md`, from `ConfigureServices`:

```csharp
SingleStopHostedService.Decorate(services, "VinylVault.Hosting.FrameworkHost");
```

The decorator shares the first `StopAsync` task with later callers, and throws when the named service is not registered, so a rename in the app is noticed.

## Step 7 -- Share an expensive host per assembly

Match the fixture to the host's cost. A cheap local factory (no containers) can stay a collection fixture (`ICollectionFixture<T>`), as `umbrella-dotnet-scaffold-aspnetcore-integration-tests` scaffolds it. An expensive host (containers, schema creation, seeding) used by more than one collection or test class starts once per test assembly instead, and its collections keep only their name and `DisableParallelization`, with no `ICollectionFixture`. Never register one factory type both ways:

```csharp
// AssemblyFixtures.cs in each test project
[assembly: AssemblyFixture(typeof(IndyRecordsWebApplicationFactory))]
[assembly: CaptureConsole]
```

- The factory implements `IAsyncLifetime`; tests receive it through their constructor.
- Logging during startup happens outside any test's output helper, so add `logging.AddSimpleConsole(options => options.SingleLine = true)` in `ConfigureLogging`; `CaptureConsole` attaches console output to the test that was running.
- Tests that change shared data go in a collection with `DisableParallelization = true`, or create and delete their own data.
- xUnit only honours a `[CollectionDefinition]` declared in the test assembly itself. Keep the collection name as a constant in the shared library and re-declare it in each test project:

```csharp
[CollectionDefinition(DataMutationCollection.Name, DisableParallelization = true)]
public sealed class DataMutationCollectionDefinition;
```

- When the app keeps process-wide static state and can only host once per process, a second host with different settings (Kestrel for browsers, live settings) needs its own test project.

## Step 8 -- Prove the isolation holds

Add self-tests that fail loudly if the overrides ever stop applying:

- the configured database (read from the resolved options, not from configuration) is the test container's server and database;
- each inert endpoint resolves to `UnreachableAddress`, and each stubbed endpoint to the stub;
- no `secrets.json` source is present in the host's `IConfigurationRoot`;
- the startup duration, sent with `TestContext.Current.SendDiagnosticMessage`, for visibility.

## Rules

- Never stub or disable a dependency without the user's per-dependency decision.
- Never let the host write to a shared external system, whatever was chosen; explain this rule to the user.
- Never rely on user secrets, developer machine settings or production endpoints in tests.
- Remove the centrally injected Moq from integration projects that use real services (`<PackageReference Remove="Moq" />`); it is unused and can pull in a conflicting Castle.Core.
- Keep isolation in the shared factory, not in individual tests.
- Container tests need Linux agents with Docker; keep file and path casing exact, because Linux is case-sensitive.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<TestProject>"`.

Run once with Docker running (tests pass) and once with Docker stopped (container tests are skipped locally, not failed). Set `TF_BUILD=True` with Docker stopped to confirm the run fails with the clear message.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## References

- `references\isolation-helpers.md`

## Output

Report:

- each external dependency, the user's decision and the keys overridden, and how real services and extra containers get their settings;
- the write-prevention switches applied, and each app feature (warm-up, jobs) with the user's keep-or-disable choice;
- whether the user-secrets source was removed and where the overrides are added;
- helpers reused or created (recorder, Docker gate, attributes, decorator) and their paths;
- assembly fixture, collections re-declared and the CI variable that makes Docker required;
- validation results with and without Docker.
