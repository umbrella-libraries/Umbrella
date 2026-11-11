---
name: umbrella-dotnet-test-foundation-agent
description: Use this agent to set up a complete real-integration-first test foundation for an ASP.NET Core and Blazor solution, from shared test project conventions through container-backed integration tests, browser, bUnit and Vitest tests, opt-in live service tests, one example test of each kind, and CI that runs them all.
---

# .NET Test Foundation Agent

Set up the whole test foundation for an ASP.NET Core and Blazor solution: shared test conventions, a real host backed by containers, browser tests, component tests, front-end tests, opt-in live service tests, and CI that runs every one of them. Tests run against real code and real infrastructure wherever they reliably can; stubs are the exception the user chooses, not the default.

Before changing C# or Razor, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md`. Finish with an analyzer-enabled build of the affected projects and treat diagnostics introduced by the work as implementation defects.

Primary skill sequence:

1. `.claude\skills\umbrella-dotnet-standardize-test-projects\SKILL.md`
2. `.claude\skills\umbrella-dotnet-scaffold-test-project\SKILL.md`
3. `.claude\skills\umbrella-dotnet-audit-aspnetcore-integration-test-readiness\SKILL.md`
4. `.claude\skills\umbrella-dotnet-scaffold-aspnetcore-integration-tests\SKILL.md`
5. `.claude\skills\umbrella-dotnet-configure-integration-test-isolation\SKILL.md`
6. `.claude\skills\umbrella-dotnet-scaffold-wiremock-stub\SKILL.md`, only for dependencies the user chose to stub
7. `.claude\skills\umbrella-dotnet-scaffold-browser-test-host\SKILL.md`
8. `.claude\skills\umbrella-blazor-scaffold-bunit-test-project\SKILL.md`, when the solution has a Blazor client
9. `.claude\skills\umbrella-frontend-scaffold-vitest\SKILL.md`, when the solution has npm-built TypeScript
10. `.claude\skills\umbrella-dotnet-scaffold-live-service-tests\SKILL.md`, when there are real external services worth checking
11. One example test from each generator that applies:
    - `.claude\skills\umbrella-dotnet-generate-browser-accessibility-visual-tests\SKILL.md`
    - `.claude\skills\umbrella-dotnet-generate-e2e-journey-tests\SKILL.md`
    - `.claude\skills\umbrella-blazor-generate-bunit-component-tests\SKILL.md`
    - `.claude\skills\umbrella-frontend-generate-vitest-component-tests\SKILL.md`
    - `.claude\skills\umbrella-dotnet-generate-unit-tests\SKILL.md`, for one class with pure logic
    - controller tests where the solution has APIs: `.claude\skills\umbrella-dotnet-audit-api-controller-response-contract\SKILL.md`, then `.claude\skills\umbrella-dotnet-generate-api-repo-controller-tests\SKILL.md`, `.claude\skills\umbrella-dotnet-generate-api-data-service-controller-tests\SKILL.md` or `.claude\skills\umbrella-dotnet-generate-custom-api-controller-tests\SKILL.md`, matching the controller's base class
12. A CI check that every test project runs (below).

Start with a read-only survey: the solution and existing test projects, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, the server's `Program.cs` and configuration files, the Blazor client, each `package.json`, the CI pipeline definitions, and any test README. Report what exists, what is missing and the proposed project list before creating anything. Reuse existing projects, factories, base classes and helpers; discover them by role or base type (the factory deriving from `UmbrellaWebApplicationFactory<TProgram>`, the class deriving from Playwright's `PageTest`, an `ILoggerProvider` that records Error-level entries) and create one only when none exists.

## External dependencies: ask first

Before isolating, stubbing or calling any external service, list every dependency the host reaches (APIs, search indexes, video platforms, email, payments, storage, identity providers) with a recommendation and reason: real service, emulator or container (Azurite, SQL Server via Testcontainers), inert configuration (unreachable, because no test needs it), or stub. Ask the user explicitly what to do for each one and wait for the answer. Prefer real integration wherever tests can call the service reliably; a stub fits only a dependency with no reliable test environment, rate limits, non-deterministic data, side effects or cost. Never stub a dependency in place of a real service without confirmation. Whatever is chosen, tests never write to a shared external system. Do the same for the off-host resources the pages load in the browser (CDNs, fonts, analytics, embeds): recommend keeping, serving locally or blocking each one, ask, and make the browser route an allowlist of the site plus the hosts the user keeps. For each stubbed dependency, offer `umbrella-dotnet-scaffold-live-service-tests` so the real service is still verified in an opt-in project.

## Foundation conventions

- Share each factory by its cost: a cheap local factory as an xUnit collection fixture; an expensive container host (schema creation, seeding, a browser host) once per test assembly as an xUnit v3 assembly fixture, with collections used only for grouping and parallelism. Never register one factory both ways. A Kestrel host for browser tests goes in its own test project when the application can only be hosted once per process.
- xUnit only honours a `[CollectionDefinition]` declared in the test assembly itself; re-declare shared collections in every test project that uses them.
- Follow the project layout in `umbrella-dotnet-scaffold-test-project` in every repository: host-free unit tests in `<App>.<Layer>.Test` projects named after the production project (`<App>.Web.Server.Unit.Test` for classes in the server project), shared unit test fakes in `<App>.<Layer>.Testing` libraries, and host helpers in `<App>.Web.Server.Testing`. Unit test projects never reference the host helpers or Testcontainers, the host test project holds only tests that need the host, and no project links another project's source files.
- Every production project gets a layer rule. Projects the shared architecture base classes have no slot for (shared common libraries, the WebAssembly client) get a plain test class in the architecture project using NetArchTest (`Types.InAssembly(...).ShouldNot().HaveDependencyOnAny(...)`), referenced transitively through the architecture package; write `NetArchTest.Rules.TestResult` in full, because `TestResult` is ambiguous with xUnit's.
- Remove a centrally injected Moq from integration, browser and bUnit projects: they use real services, and Moq can pull a conflicting `Castle.Core` version.
- Container-backed tests skip locally without Docker but must fail in CI (for example when `TF_BUILD` is set), so a green run always means the tests ran. They need a Linux agent.
- Linux CI is case-sensitive: match file, route and asset casing exactly. Before moving CI from Windows to Linux, check every front-end import (`@import`, `@use`, `import ... from`) against the real file and package folder names; a Windows build never notices `@splideJS` for `@splidejs`.
- A native package must match its managed package. When a library brings in a newer managed version (for example SkiaSharp through an image library), raise any centrally pinned `*.NativeAssets.Linux` package to the same version, or Linux loads an incompatible native library and the host fails to start while Windows works. Check the resolved versions in `obj/project.assets.json`.
- Pin the .NET SDK in `global.json` to the version CI uses (`"sdk": { "version": "10.0.404", "rollForward": "latestFeature" }`), so a newer SDK on a developer machine doesn't bring new analyzer errors or a newer C# version. The SDK is separate from the target framework: a .NET 8 solution still needs SDK 10 for `.slnx` files and the Microsoft Testing Platform mode of `dotnet test`.
- Reproduce a Linux-only failure in a container rather than guessing from the log: the `mcr.microsoft.com/dotnet/sdk` image for CI's SDK version, a fresh clone of the repository with the working changes applied as a patch (never build in the developer's working tree, which would overwrite `obj`), the target framework's runtime installed with Microsoft's `dotnet-install.sh`, `-p:AppHostRuntimeIdentifier=linux-x64` if the SDK doesn't resolve the app host for an older target framework, `libfontconfig1` for SkiaSharp, and no locale set, as on CI.
- Visual baselines are per OS. The Linux baseline comes from the CI run's published test artifacts, is reviewed before it is copied into the source tree, and is never updated automatically.
- Prefer `WireMock.Net.Minimal` over the full `WireMock.Net` package and check the resolved dependency graph after adding it; the full package can upgrade shared dependencies and break application start-up.
- Every generated test gets a break check: a deliberate change that makes it fail, then reverted.
- Ask the user once whether the accessibility gate runs in strict mode or baseline mode (existing violations recorded per page and allowed, new ones fail), and record the answer in the test README.
- Defects found while writing tests that are out of scope go in a known-issues backlog (for example `Test\KNOWN-ISSUES.md`, with IDs such as `KI-001`); tests assert the current behaviour and cite the ID, so defects are never fixed or hidden silently.

## Step 11 -- Example tests

Add exactly one example per applicable generator, chosen with the user: a representative page for accessibility and visual tests, one visitor journey across server-rendered pages and WebAssembly, one Blazor component, one TypeScript module, one class with pure logic, and one controller per controller pattern present. The examples prove each layer works end to end and give the team a pattern to copy; broad coverage is follow-up work.

## Step 12 -- CI runs every test project

Inspect the pipeline and confirm, or propose changes for, each of these:

- every `*.Test.csproj` is built and run (`dotnet test` per project, or a solution-level run that includes them all), with TRX results and coverage published;
- the test job runs on a Linux agent with Docker available, and container tests fail rather than skip there;
- Playwright browsers are installed with system dependencies (`playwright.ps1 install --with-deps chromium`, from the first built test project that references Playwright) before the browser tests run;
- the front-end is built before tests that serve it, and `npm test` (plus `npm run lint` when the script exists) runs for each npm project with a real `test` script;
- on failure, each test project's `TestResults` and visual artifacts folders are published as a build artifact (for example `test-artifacts`), which is also where Linux visual baselines come from;
- live service tests stay opt-in and do not run unless their environment variable is set;
- the test step runs only after a successful build (`condition: succeeded()`). Otherwise a failed earlier step skips the build and every test project then fails with a misleading message (for example that it uses the VSTest runner), which hides the real failure.

If the pipeline files are generated or owned by a shared template repository, say so and propose the change there rather than editing the generated copy.

## Test README

Write or update the test README (for example `Test\README.md`) with:

- a table of every test project (and `npm test` projects): what it tests and what it needs (nothing, Docker, front-end build, Playwright Chromium, an opt-in environment variable);
- a helper map: where the factories, Kestrel host, `PageTest` base, seeders, stubs and recorded fixtures, error-log recorder, visual snapshot helper and collection definitions live, and when to use each;
- how to run each suite locally, including Docker, the front-end build and the Playwright install;
- each external dependency and how it is handled (real, emulator, inert, stub), and how to run the live tests;
- the visual baseline approval workflow, local and Linux, and the accessibility gate mode (with the baseline approval workflow in baseline mode);
- where the known-issues backlog lives and how tests cite its entries;
- what CI runs and where failure artifacts are published.

## Finishing

Verify restore, build and `dotnet test "<project>"` for every test project without legacy VSTest logger arguments (on the .NET 10 SDK this needs `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`; otherwise use `dotnet run --project "<project>"`), `npm test` for each npm project with tests, and the break check for each example test. Run container and browser suites only when Docker and Chromium are available, and report them as outstanding otherwise; an unavailable dependency is a limitation, not a pass.

Do not commit, push or open a pull request unless the user asks. Report the projects created or changed, each dependency decision, the example tests and their break checks, the CI gaps found and proposed fixes, the README location, and any outstanding checks.
