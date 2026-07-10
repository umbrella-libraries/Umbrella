---
name: dotnet-scaffold-aspnetcore-integration-tests
description: 'Scaffold ASP.NET Core controller integration test infrastructure into an existing or newly created .NET test project using Umbrella.Testing.AspNetCore: concrete local and SQL Server/Azurite Testcontainers WebApplicationFactory classes, xUnit collections, test authentication, config overrides, Program hook, and a minimal smoke test. Use after auditing the server app or when adding WebApplicationFactory-based integration tests.'
---

# Scaffold ASP.NET Core Integration Tests

## Purpose

Add the reusable infrastructure needed for real ASP.NET Core controller integration tests. This skill assumes the target test project already follows the shared Umbrella test-project pattern. Use `dotnet-scaffold-test-project` first when no suitable test project exists.

## Required inputs

Before writing files, either run `dotnet-audit-aspnetcore-integration-test-readiness` or gather the same facts:

- server project path and entry point type;
- whether a public partial `Program` hook exists;
- authentication scheme and required claims;
- EF Core `DbContext` type and constructor shape;
- database provider and migrations assembly;
- required startup configuration values;
- external services that must be isolated, including Azure Blob Storage/Azurite requirements;
- project package/version style.

## Packages and references

Ensure the test project references:

```xml
<PackageReference Include="Umbrella.Testing.AspNetCore" Version="<version>" />
<PackageReference Include="Umbrella.Testing.Xunit" Version="<version>" />
```

Rules:

- Use the repo's package-version style. Omit `Version` under true CPM.
- Add project references needed for the server, DbContext, migrations, shared auth/claim types, and shared policy types.
- Do not add shared xUnit/MTP runner packages if the repo injects them centrally for `IsTestProject=true`.

## Program hook

If the app uses top-level statements and no public entry point hook exists, add this file to the server project:

```csharp
public partial class Program
{
}
```

Keep it in the global namespace unless the app already defines `Program` inside a namespace.

## Folder layout

Use this structure unless the repo already has a better convention:

```text
Test\<App>.Web.Server.Test\
  Integration\
    Shared\
      Auth\
        TestAuthenticationHandler.cs
      Hosting\
        <App>TestConfiguration.cs
        <App>WebApplicationFactory.cs
        <App>SqlServerWebApplicationFactory.cs
        <App>IntegrationTestCollection.cs
        <App>SqlServerIntegrationTestCollection.cs
        <App>WebApplicationFactoryTests.cs
```

## Test authentication

Create a test auth handler that:

- uses the app's real default authenticate scheme where possible;
- returns a `ClaimsPrincipal` with realistic default user id, name, roles, and required custom claims;
- allows per-test overrides through request headers or a small client helper;
- supports unauthenticated requests for authorization tests.

When controller response-code tests are in scope, the handler (or client helper) must be able to issue **three distinct identities** per entity under test:

- **anonymous** — no authentication result, so `[Authorize]` endpoints challenge with `401`;
- **passing** — an authenticated identity that satisfies both the declarative policies and the imperative resource authorization handlers;
- **denying** — an authenticated identity that passes `[Authorize]` but fails the entity's resource authorization handler (e.g. a non-owner user id), so imperative checks produce `403`.

A single high-privilege default identity cannot exercise the `401`/`403` paths. When a resource handler distinguishes several identity classes (owner, account manager, admin role, …), the handler/helper must be able to construct a passing and a denying variant for each class, not just one pair overall.

For Identity-cookie apps, replace the application scheme handler rather than bypassing authorization:

```csharp
services.PostConfigure<AuthenticationOptions>(options =>
{
	options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
	options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
	options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
	options.DefaultScheme = IdentityConstants.ApplicationScheme;

	if (options.SchemeMap.TryGetValue(IdentityConstants.ApplicationScheme, out AuthenticationSchemeBuilder? scheme))
		scheme.HandlerType = typeof(TestAuthenticationHandler);
	else
		options.AddScheme<TestAuthenticationHandler>(IdentityConstants.ApplicationScheme, displayName: null);
});
```

Only remove cookie post-configuration if cookie events or validation interfere with the test handler. Keep the change local to the factory.

## Test configuration

Create a test configuration helper and add it in `ConfigureWebHostBuilder`:

```csharp
protected override void ConfigureWebHostBuilder(IWebHostBuilder builder)
{
	ArgumentNullException.ThrowIfNull(builder);

	_ = builder.ConfigureAppConfiguration((_, configurationBuilder) => <App>TestConfiguration.Add(configurationBuilder));
}
```

Rules:

- Provide inert values for all required startup options.
- Override production-looking default connection strings/secrets.
- Avoid user secrets and cloud resources.
- Use `Development` only when it disables production-only startup paths.
- Replace unsafe services if startup itself opens network connections.

## Response contract host requirements

When the test project will assert controller response codes (see `docs\api-base-controller-endpoint-map.md` in the Umbrella repository), verify the following in the server app and compensate in the factory where missing:

- **Claims principal propagation** — the pipeline must call `UseUmbrellaPropagateClaimsPrincipal()` (or equivalent) so `HttpContext.User` flows to `Thread.CurrentPrincipal`. Without it, the imperative authorization checks in `UmbrellaRepositoryCoreDataService` throw and expected `403` responses surface as `500`s. If the production pipeline lacks it, flag this to the user rather than patching it only in tests — the production app has the same defect.
- **Validation status code** — confirm the app calls `ConfigureUmbrellaApiBehaviorOptions()` / `ConfigureUmbrellaMvcBuilderOptions()` and record any `validationFailureStatusCode` override. Generated tests must assert the configured value (default `422`) for model binding/validation failures, and `400` for malformed JSON root errors.
- **Environment name** — the base controllers' exception filters only catch when the host environment is not `Development`. Run the factory with a non-`Development` environment (e.g. `Production` or a dedicated `IntegrationTest` name, provided startup avoids production-only services) whenever `500` response shapes are asserted.
- **Authorization policies** — the `CorePolicyNames.Create/Read/Update/Delete` policies (or the custom names configured on `UmbrellaRepositoryDataServiceOptions`) must be registered together with the entities' resource authorization handlers. Use `dotnet-scaffold-auth-policy` and `dotnet-scaffold-resource-auth-handler` if they are missing.

## Test logging

Do not add a project-level `ConfigureLogging` override by default. `UmbrellaWebApplicationFactory` already clears host logging providers, adds the xUnit output logger provider, and sets the minimum log level to `Warning`.

Rules:

- Do not call `logging.ClearProviders()` in consuming app factories unless there is a specific, documented reason; it removes the xUnit output logger.
- Do not add `ConfigureTestLogging` helper methods to scaffolded test projects.
- Override `GetMinimumLogLevel()` in the factory only when a project genuinely needs more or less log detail.
- Prefer keeping the default `Warning` minimum level to reduce ADO memory pressure while preserving useful xUnit output on failures.

## Local factory

Create a sealed local factory:

```csharp
public sealed class <App>WebApplicationFactory : UmbrellaLocalWebApplicationFactory<Program>
{
	protected override void ConfigureWebHostBuilder(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.ConfigureAppConfiguration((_, configurationBuilder) => <App>TestConfiguration.Add(configurationBuilder));
	}

	protected override void ConfigureAuthentication(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		ReplaceApplicationAuthenticationHandler(services);
	}
}
```

Use this for host/auth/routing tests that do not require database state.

## SQL Server/Azurite Testcontainers factory

Create a sealed SQL Server factory when the app uses SQL Server. The Umbrella base also starts an Azurite Testcontainer by default; override `UseAzurite` and return `false` only when the app does not need Azure Blob Storage during integration tests.

```csharp
public sealed class <App>SqlServerWebApplicationFactory : UmbrellaSqlServerAzuriteWebApplicationFactory<Program, <App>DbContext>
{
	protected override void ConfigureWebHostBuilder(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		_ = builder.ConfigureAppConfiguration((_, configurationBuilder) => <App>TestConfiguration.Add(configurationBuilder));
	}

	protected override void ConfigureAzuriteConfiguration(IConfigurationBuilder configurationBuilder, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(configurationBuilder);
		ArgumentNullException.ThrowIfNull(connectionString);

		_ = configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["<AzureStorageConnectionStringKey>"] = connectionString
		});
	}

	protected override void ConfigureAuthentication(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		<App>WebApplicationFactory.ReplaceApplicationAuthenticationHandler(services);
	}

	protected override void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder optionsBuilder)
	{
		ArgumentNullException.ThrowIfNull(optionsBuilder);

		_ = optionsBuilder
			.MigrationsAssembly("<MigrationsAssembly>")
			.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
			.EnableRetryOnFailure();
	}
}
```

If the app does not use Azure Blob Storage, add:

```csharp
protected override bool UseAzurite => false;
```

Add this override when the context constructor needs non-generic options removed:

```csharp
protected override void ReplaceDbContextRegistration(IServiceCollection services)
{
	ArgumentNullException.ThrowIfNull(services);

	base.ReplaceDbContextRegistration(services);
	_ = services.RemoveAll<DbContextOptions>();
}
```

Do not use EF InMemory as a substitute for SQL Server controller integration tests unless the user explicitly asks for a fast smoke-only suite.

## xUnit collections

Create separate collections for local and container-backed factories:

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class <App>IntegrationTestCollection : ICollectionFixture<<App>WebApplicationFactory>
{
	public const string Name = "<App> Integration";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class <App>SqlServerIntegrationTestCollection : ICollectionFixture<<App>SqlServerWebApplicationFactory>
{
	public const string Name = "<App> SQL Server Integration";
}
```

Rules:

- Put Testcontainers tests in the SQL collection. This collection may own both SQL Server and Azurite containers.
- Keep parallelization disabled by default to reduce memory pressure and avoid multiple containers fighting for resources.
- Do not share one factory type for local and SQL tests unless the app has no meaningful non-database integration tests.

## Smoke test

Add one minimal smoke test so the project is runnable before real controller tests exist:

```csharp
[Collection(<App>IntegrationTestCollection.Name)]
public sealed class <App>WebApplicationFactoryTests
{
	private readonly <App>WebApplicationFactory _factory;

	public <App>WebApplicationFactoryTests(<App>WebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public void CreateClientCreatesConfiguredTestClient()
	{
		using HttpClient client = _factory.CreateClient();

		Assert.NotNull(client.BaseAddress);
	}
}
```

Avoid starting Docker in the default smoke test unless the user explicitly asks for a container smoke test.

## Validation

Run:

```powershell
dotnet restore "<TestProject>"
dotnet build "<TestProject>" --no-restore
dotnet test "<TestProject>" --no-restore --no-build
```

If Docker/Testcontainers tests are included immediately, run them only when Docker is available and the user expects the container cost.

## Output

Report:

- packages and references added;
- factory classes and collection names;
- auth scheme and default claims;
- DbContext replacement details;
- config overrides and external-service caveats;
- validation results and warnings.
