---
name: umbrella-dotnet-scaffold-live-service-tests
description: 'Create a separate opt-in *.Live.Test project that runs read-only checks of an Umbrella app against real external services: an environment-variable opt-in, settings from environment variables or the app appsettings, a SkipReason and LiveFact combining the Docker and live gates, a ShouldStart gate, overrides appended last over any stub, a Live trait, a timeout backstop, and a scheduled pipeline. Use to verify the real behaviour behind a WireMock stub.'
---

# Scaffold Live Service Tests

## Purpose

Check, separately from the ordinary suites, that this branch's code still works against the real external services that other tests stub or disable. The tests are read-only, run only when someone opts in, and never start the host when they would all be skipped.

They complement `umbrella-dotnet-scaffold-wiremock-stub`: the stub makes the ordinary tests deterministic; live tests catch the real service drifting from the stub's recorded responses.

## Decide first

Before writing anything, list each external service the live tests could call and ask the user, per service:

- whether it should have live tests at all;
- which non-production endpoint to use (never production unless the user explicitly asks, and even then read-only);
- which read-only operations exist (queries, GETs), and confirm the tests will make no writes;
- where credentials come from locally (environment variables, or keys already in the app's committed non-secret `appsettings*.json`) and in the pipeline (secret variables, a key vault).

Continue only for the services the user approves.

## Preflight

1. Read the repo's test README (for example `Test\README.md`) if it exists.
2. Find the shared factory deriving from `UmbrellaWebApplicationFactory<TProgram>` and confirm it has: a virtual `GetConfigurationOverrides()` added as the last configuration source, a virtual `ShouldStart`, Docker-gated attributes, an Error-level `ILoggerProvider` recorder, and background push or sync features disabled. If any is missing, run `umbrella-dotnet-configure-integration-test-isolation` first.
3. Find any stub for the service (a class wrapping `WireMockServer`) and the setting keys it replaces.
4. Search for an existing `*.Live.Test` project, a live settings class, or a `LiveFact` attribute, and extend them rather than adding another.

## Step 1 -- Create a separate project

Use `umbrella-dotnet-scaffold-test-project` for the shell, named `<App>.Web.Live.Test` (for example `IndyRecords.Web.Live.Test`):

```xml
<ItemGroup>
	<!-- Real services, no mocks. -->
	<PackageReference Remove="Moq" />
</ItemGroup>

<ItemGroup>
	<ProjectReference Include="..\IndyRecords.Web.Server.Testing\IndyRecords.Web.Server.Testing.csproj" />
</ItemGroup>
```

Keep it separate so the ordinary suites never depend on an external service, and because an app that hosts once per process cannot run a second host with live settings beside the stubbed one. Explain this in a comment in the project file.

## Step 2 -- Read the opt-in and settings

Create one static settings class per service, for example `LiveCatalogueApi`:

```csharp
internal static class LiveCatalogueApi
{
	private static readonly Lazy<IConfiguration> _appConfiguration = new(LoadAppConfiguration);

	private static bool IsOptedIn => bool.TryParse(Environment.GetEnvironmentVariable("INDYRECORDS_LIVE_TESTS"), out bool optedIn) && optedIn;

	private static string? BaseUrl => Setting("INDYRECORDS_LIVE_CATALOGUE_URL") ?? _appConfiguration.Value["CatalogueApi:BaseUrl"];

	private static string? Key => Setting("INDYRECORDS_LIVE_CATALOGUE_KEY") ?? _appConfiguration.Value["CatalogueApi:Key"];

	/// <summary>Gets why the live tests cannot run, or null when they can.</summary>
	public static string? SkipReason =>
		!IsOptedIn ? "Live tests only run when INDYRECORDS_LIVE_TESTS=true."
		: new[] { BaseUrl, Key }.Any(string.IsNullOrWhiteSpace) ? "No catalogue API settings were found (see LiveCatalogueApi in the Live test project)."
		: null;

	public static IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides()
	{
		if (SkipReason is not null)
			yield break;

		yield return new("CatalogueApi:BaseUrl", BaseUrl);
		yield return new("CatalogueApi:Key", Key);
	}

	private static string? Setting(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

	// The app's settings as its environment loads them, read from the source tree rather than copied.
	private static IConfiguration LoadAppConfiguration()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IndyRecords.slnx")))
			directory = directory.Parent;

		if (directory is null)
			return new ConfigurationBuilder().Build();

		string serverDirectory = Path.Combine(directory.FullName, "Web", "IndyRecords.Web.Server");

		return new ConfigurationBuilder()
			.AddJsonFile(Path.Combine(serverDirectory, "appsettings.json"), optional: true)
			.AddJsonFile(Path.Combine(serverDirectory, "appsettings.Development.json"), optional: true)
			.Build();
	}
}
```

- The opt-in is one environment variable for the whole project (`<APP>_LIVE_TESTS=true`); both local runs and CI must set it.
- Each setting comes from an environment variable first, then falls back to the app's own `appsettings.json` and `appsettings.{Environment}.json`, read from the source tree (walk up from `AppContext.BaseDirectory` to the solution file, then into the server project) rather than copied. Never read user secrets, and never commit values.
- The fallback only makes sense when those files hold non-production, non-secret settings. If they do not, require the environment variables.

## Step 3 -- Derive a live factory

```csharp
public sealed class IndyRecordsLiveWebApplicationFactory : IndyRecordsWebApplicationFactory
{
	protected override bool ShouldStart => base.ShouldStart && LiveCatalogueApi.SkipReason is null;

	// Live settings come last, so they replace the stub's address set by the base class.
	protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides()
		=> base.GetConfigurationOverrides().Concat(LiveCatalogueApi.ConfigurationOverrides());
}
```

- `ShouldStart` keeps the containers and host from starting when every test would be skipped.
- The overrides are appended after the base ones, so the last value for each key (the real endpoint) wins over the stub or the unreachable address.
- Keep the base factory's push, sync and indexing features disabled: nothing the host does at startup or in the background may write to the real service.

## Step 4 -- Add the LiveFact attribute

Combine the Docker gate with the live gate (add a `LiveTheory` the same way if needed):

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveFactAttribute : FactAttribute
{
	public LiveFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		if (!DockerAvailability.IsAvailable)
			Skip = DockerAvailability.SkipReason;
		else if (LiveCatalogueApi.SkipReason is { } reason)
			Skip = reason;
	}
}
```

When CI makes Docker required, the live tests still skip unless the opt-in is set, so ordinary pipeline runs stay independent of external services.

## Step 5 -- Share the host and run one test at a time

```csharp
[assembly: AssemblyFixture(typeof(IndyRecordsLiveWebApplicationFactory))]
[assembly: CaptureConsole]

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveCollection
{
	public const string Name = "Live";
}
```

Run live tests one at a time: each checks that the host logged no errors while serving its own request, which only holds if no other test is using it, and it keeps the load on the external service down.

## Step 6 -- Write the first test

```csharp
[Trait("Category", "Live")]
[Collection(LiveCollection.Name)]
public sealed class CatalogueApiLiveTests
{
	// The app's own client timeout should fail a stalled call; this backstop makes the test fail promptly if it is lost.
	private static readonly TimeSpan _responseTimeout = TimeSpan.FromSeconds(30);

	private readonly IndyRecordsLiveWebApplicationFactory _factory;

	public CatalogueApiLiveTests(IndyRecordsLiveWebApplicationFactory factory) => _factory = factory;

	private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

	[LiveFact]
	public async Task Search_ForVinyl_ReturnsAPageOfResults()
	{
		using HttpClient client = _factory.CreateClient();
		_factory.Errors.Clear();

		using HttpResponseMessage response = await GetAsync(client, "api/catalogue/search?format=vinyl&pageNumber=1&pageSize=6");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.True(_factory.Errors.Logged.Count is 0, $"The app logged errors:{Environment.NewLine}{string.Join(Environment.NewLine, _factory.Errors.Logged)}");

		using JsonDocument result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken);
		JsonElement[] items = [.. result.RootElement.GetProperty("items").EnumerateArray()];
		Assert.InRange(items.Length, 1, 6);
		Assert.All(items, x => Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("title").GetString())));
	}

	private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path)
	{
		try
		{
			return await client.GetAsync(new Uri(path, UriKind.Relative), CancellationToken).WaitAsync(_responseTimeout, CancellationToken);
		}
		catch (TimeoutException exc)
		{
			throw new TimeoutException($"The catalogue API did not respond within {_responseTimeout.TotalSeconds:0} seconds.", exc);
		}
	}
}
```

- Mark every class `[Trait("Category", "Live")]` so runs can filter them.
- Wrap calls in `WaitAsync(timeout, cancellationToken)` and rethrow a `TimeoutException` naming the service. `HttpClient.Timeout` does not help with the in-memory test server, which completes a cancelled request only when the app's pipeline returns.
- Clear the error recorder before each request and assert it is empty after, because services often log and swallow failures behind a 200.
- Real data changes, so assert the shape (counts in range, required fields present, URLs on the expected host), not particular items.
- When a case depends on data that may not exist (a second page, a specific category), call `Assert.Skip("...")` with the reason rather than failing.
- Read-only operations only. No creates, updates, deletes, publishes, sync or cache-purge calls, ever.

## Step 7 -- Run on a schedule

Add a separate scheduled pipeline (for example nightly), not a step on every push:

```yaml
schedules:
  - cron: '0 3 * * 1-5'
    displayName: Live service tests
    branches:
      include: [ main ]
    always: true
trigger: none
pr: none

# Container tests need Docker and the Linux SQL Server and Azurite images.
pool:
  vmImage: ubuntu-latest

steps:
  # Restore, build the front end if pages need it, then:
  - pwsh: dotnet test "Test/IndyRecords.Web.Live.Test/IndyRecords.Web.Live.Test.csproj" --configuration Release
    env:
      INDYRECORDS_LIVE_TESTS: 'true'
      INDYRECORDS_LIVE_CATALOGUE_URL: $(LiveCatalogueUrl)   # pipeline variable: the non-production endpoint
      INDYRECORDS_LIVE_CATALOGUE_KEY: $(LiveCatalogueKey)   # secret variable
```

Map every setting the settings class reads, the endpoint as well as the key, so the run never falls back to whatever the committed appsettings happen to hold. Secret variables are not exposed to scripts unless mapped through `env`; never echo them. Report failures to the team that owns the integration, since they usually mean the service or its data changed.

## Rules

- No writes to an external service, ever.
- Opt-in only; the ordinary CI run skips live tests and does not start their host.
- Non-production endpoints unless the user explicitly decides otherwise.
- Credentials from environment variables or non-secret committed settings; never commit secrets and never read user secrets.
- Live tests never replace stubbed integration tests; they verify the real service separately.

## Validation

```powershell
dotnet build "<LiveTestProject>"
dotnet test "<LiveTestProject>" --no-build
```

On the .NET 10 SDK, `dotnet test` runs Microsoft Testing Platform projects only when `global.json` contains `"test": { "runner": "Microsoft.Testing.Platform" }`; without it, use `dotnet run --project "<LiveTestProject>"`.

Without the opt-in, every test is reported skipped with the opt-in reason and the host does not start (no container startup in the log). With the opt-in and settings provided by the user, run once and report the results. Do not set credentials yourself.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Output

Report:

- the services covered, the user's decisions, endpoints and read-only operations used;
- the project, settings class, opt-in variable and setting variables;
- the live factory, attribute, collection and trait;
- the scheduled pipeline and the secrets it expects;
- validation results with and without the opt-in.
