---
name: umbrella-dotnet-generate-unit-tests
description: 'Generate host-free xUnit unit tests for one class with pure logic in an Umbrella solution (a service, mapper, selection or option factory, validation attribute, helper or extension): choose the class with the user, use real collaborators and in-memory fakes rather than mocks, cover each branch with theories, assert observable results, log out-of-scope defects as known issues, and prove the tests can fail. Use for logic that does not need a web host, database or browser.'
---

# Generate Unit Tests

## Purpose

Add fast unit tests for **one class** whose behaviour can be checked without starting the app: calculations, mappings,
formatting, validation rules, option lists, query building and other branching logic. Anything that needs the web
host, a database, a container or a browser belongs in the integration, browser or component test skills instead.

## Preflight

1. Find the unit test project for the production project the class lives in, following the project layout in
   `umbrella-dotnet-scaffold-test-project` (for example `IndyRecords.Core.Logic.Test` for `IndyRecords.Core.Logic`,
   `IndyRecords.Web.Server.ModelFactories.Test` for mappers, `IndyRecords.Web.Server.Unit.Test` for tag helpers in the
   server project). It must follow the shared test configuration (`IsTestProject=true`, xUnit v3 on Microsoft Testing
   Platform). If there is none, create it with `umbrella-dotnet-scaffold-test-project` first. Never add unit tests to a
   project that starts the host or references Testcontainers, even when one already exists for the same production
   project: a unit test must run without Docker. A test project may also cover the lower layers its project references
   (for example domain and shared classes under `Domain\` and `Shared\` folders of the logic test project); say which
   you chose. Remove a centrally injected mocking package from projects that use only fakes
   (`<PackageReference Remove="Moq" />`), but leave existing tests that use it alone.
   Fakes shared by several unit test projects go in an `<App>.<Layer>.Testing` library (`IsTestProject=false`,
   `IsPackable=false`) for the lowest layer whose types they need, referenced by each. Never put them in a host helper
   library (`*.Web.Server.Testing` or a framework-specific host helper library), which would pull containers into unit
   tests, and never share them as linked source files.
2. Read the test README (for example `Test\README.md`) for the helper map, the known-issues backlog and existing fakes,
   then one existing test class in that project to mirror its folders, namespaces and naming.
3. Check the class is reachable: implementation classes are often `internal`, so the production project needs
   `<InternalsVisibleTo Include="<TestProject>" />`. Add it if missing, rather than making the class public, because the
   architecture tests may require implementations to stay internal. If the user doesn't want production code touched,
   create internal types by reflection through their public interface, as the framework does.

## Choosing the class

If the user hasn't named one, propose two or three candidates and ask. When the user has approved a list or a scope
(for example every selection factory and every mapper with branches), work through it without asking per class, and
report each class covered or skipped with the reason. Prefer classes with:

- branches, loops or calculations (date and time rules, paging maths, URL or path building, sorting and filtering);
- mappers with conditions (fallback values, optional parts, flags that change the output), not plain property copies;
- validation attributes and option factories whose results editors or visitors depend on;
- no I/O, or I/O behind an interface that a small in-memory fake can stand in for.

Skip trivial code (auto-properties, one-line pass-throughs, plain copies a generator produced) and say so in the report.

## Step 1 -- Read the class

List its public (or internal) members, every branch and the inputs that select it, the exceptions it throws, and its
dependencies. Classify each dependency:

| Dependency | Use in tests |
|---|---|
| Pure code (another service with no I/O, a mapper, options) | The real implementation |
| Logging | `NullLogger<T>.Instance`, or a small recording logger when a log entry is part of the behaviour |
| Time | `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) when the class takes a `TimeProvider`; otherwise fixed input values. Where the class reads `DateTime.UtcNow` directly, assert the result falls within a window taken before and after the call |
| A repository, cache or store behind an interface | A hand-written in-memory fake in the test project (reuse one listed in the helper map) |
| HTTP, a database, files or a host-only service | Not a unit test: use the integration test skills |
| Framework objects that normally need the running host (content models, routing features, a static service locator) | A small subclass or a built instance with the data the framework would add; record each recipe in the test README helper map so later tests reuse it. A static service locator needs a non-parallel collection |

Use a mocking library only when an interface is too large to fake by hand and the test checks a single call; never mock
the class under test or pure collaborators. If the class reads configuration, build real `IOptions<T>` with
`Options.Create(...)`.

If the class calls an external service directly (an HTTP client, an SDK), it isn't a unit-test candidate: tell the user,
and point to `umbrella-dotnet-configure-integration-test-isolation` and the integration skills, where the user decides
how that dependency is handled.

## Step 2 -- Write the tests

Create `<FolderMatchingTheClass>\<ClassName>Tests.cs` (follow the project's existing suffix if it uses `Test`):

```csharp
public sealed class ReleaseDateFormatterTests
{
	private readonly ReleaseDateFormatter _formatter = new(NullLogger<ReleaseDateFormatter>.Instance);

	[Theory]
	[InlineData("2025-03-01", "2025-03-01", "1 March 2025")]
	[InlineData("2025-03-01", "2025-03-05", "1 - 5 March 2025")]
	[InlineData("2025-03-30", "2025-04-02", "30 March - 2 April 2025")]
	[InlineData("2025-12-30", "2026-01-02", "30 December 2025 - 2 January 2026")]
	public void Format_CollapsesTheSharedParts(string start, string end, string expected)
	{
		string result = _formatter.Format(DateOnly.Parse(start, CultureInfo.InvariantCulture), DateOnly.Parse(end, CultureInfo.InvariantCulture));

		Assert.Equal(expected, result);
	}

	[Fact]
	public void Format_RejectsAnEndBeforeTheStart()
	{
		var exception = Assert.Throws<ArgumentException>(() => _formatter.Format(new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 1)));

		Assert.Equal("end", exception.ParamName);
	}
}
```

- **One theory per behaviour**, with a row per branch. Include boundaries (zero, one, the last page, month and year
  ends, daylight-saving changes, empty and null inputs) and name what each row proves in a comment when it isn't obvious.
- **Assert observable results**: return values, the state of a returned object, thrown exception types and parameter
  names, and validation results. Don't assert on private state or on how many times a collaborator was called unless
  that call is the behaviour (for example, a cache is not hit twice).
- **Culture and time**: pass `CultureInfo.InvariantCulture` or set the culture explicitly in the test when output is
  formatted, and never depend on the machine's time zone or the current date. CI agents run Linux in UTC; time zone
  IDs may differ between Windows and Linux, so use the ID the production code uses and check it resolves on both;
  where the code matches time zone IDs exactly, choose the ID per operating system and add a row for the other system's
  ID. Run the tests with xUnit v3's `--culture` option under a European culture (`de-DE`), a right-to-left culture
  (`ar-SA`) and `invariant`. Culture data also differs by operating system: Windows knows some names the ICU data on
  Linux doesn't (Kanuri, `kr`), and Linux accepts well-formed unknown names (`notaculture`) that Windows rejects. When
  the class creates or validates cultures from input, assert each system's behaviour (`OperatingSystem.IsWindows()`)
  and log the difference as a known issue, rather than encoding one machine's answer.
- **URLs and paths**: on Linux and macOS, `Uri.TryCreate("/path", UriKind.Absolute, ...)` succeeds, as the file URI
  `file:///path`. Code that asks whether a URL is absolute (to add a host, or to leave non-web URLs alone) then
  treats site-relative paths as absolute on Linux only. Add rows for `/path`, `/Path?x=1#y` and `//host/path`, and make
  sure they run on Linux (CI or a container): Windows hides the bug. The fix is to treat a single leading `/` as
  site-relative before parsing and to check the scheme is `http` or `https`.
- **Validation attributes**: call `GetValidationResult(value, new ValidationContext(model))` and assert the result and
  message; add a row for each limit.
- **Option and selection factories**: assert the exact option texts and values editors see, in order.
- **Mappers**: assert each conditional output; one test confirming a plain mapping is enough, not one per property.
- Keep each test independent: create state per test (fields initialised in the constructor are per test in xUnit).
- Use `Type` or primitive values in theory data rather than framework objects, so rows serialise and show in results.
- Tests that characterise a dependency's behaviour (a library's quirk that the class relies on) go in their own class
  with a comment; they can't be break-checked by mutating the class under test.
- If a wrapped exception doesn't appear where expected, check whether an exception filter's state expression throws:
  the filter then counts as false and the original exception escapes.
- **Real implementations internal to another layer** (services the class under test depends on): create them by
  reflection, as the dependency injection container does, in one shared helper in the `<App>.<Layer>.Testing` library
  (for example `CoreLogicServices.DateService()`), not a copy per test project.
- **Middleware and code that only run in some environments** (sign-in gates for non-production sites, checks that only
  run behind a proxy): the integration host runs in Development, so unit test them in the server's unit test project.
  Construct the middleware with a small `IWebHostEnvironment` fake whose `EnvironmentName` is each environment, and a
  `DefaultHttpContext`. When the middleware acts in `Response.OnStarting`, set a response feature deriving from
  `HttpResponseFeature` that records the callbacks, and run them (in reverse order) from the `next` delegate before it
  writes the body, as the server does at the first write. When it challenges or signs in, register a recording
  `IAuthenticationService` in `RequestServices` whose challenge sets the status and `Location` as the real scheme does,
  and assert what the response then carries, including the body.

## Step 3 -- Break check

Prove the tests can fail: temporarily change one branch in the class (a boundary, a format string, a condition), run the
tests, confirm at least one fails with a clear message, and revert. If nothing fails, the tests don't cover that branch:
add the missing row.

For several classes, mutate in batches:

1. Back up the files into a uniquely named scratch folder. Mutations must still compile under nullable checks; build
   the mutated code with `-p:RunAnalyzersDuringBuild=false -p:EnforceCodeStyleInBuild=false` when analyzer and style
   rules are errors.
2. Restore the files byte-identical straight after the mutated build, before running the tests against the mutated
   binaries, so a parallel build never sees mutated code. Never run `--no-build` after a failed build.
3. Track which tests failed in at least one batch, and report those that never did with the reason (a dependency
   characterisation, code outside the scope you were allowed to mutate).
4. Touch the restored files, rebuild and rerun clean.

## Known issues

If the class behaves wrongly in a way that is out of scope (a wrong boundary, a culture-dependent format, a swallowed
exception), log it in the repo's known-issues backlog (see the test README) with the next ID, assert the **current**
behaviour, and put the entry's ID in a comment on that assertion, so a fix or regression is noticed. Skip a test with the
ID as its reason only when the current behaviour can't be asserted reliably. Never fix it silently, and never weaken an
assertion to hide it.

## Rules

- One class per invocation unless the user approved a list; one test class per production class.
- No web host, database, container, file system or network in unit tests.
- Prefer real collaborators and hand-written fakes; no mocks of the class under test or of pure code.
- No `Thread.Sleep`, no current date or time, no machine culture or time zone.
- Don't change production code to make it testable without asking the user, except adding `InternalsVisibleTo`.

## Validation

```powershell
dotnet build "<TestProject>"
dotnet test "<TestProject>" --no-build
```

On the .NET 10 SDK without `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`, use
`dotnet run --project "<TestProject>"`. All new tests pass, the break check produced a failure, and the tests pass with
the machine culture set to something other than `en-GB` or `en-US`.

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their
installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.
Rules that commonly fire in fakes and tests: CA1725 (parameter names matching the interface), CA1062 on theory
parameters, CA1861 (constant arrays as arguments), IDE0028, IDE0058 on `Assert.NotNull` of a nullable value type, and
xUnit1051 (pass `TestContext.Current.CancellationToken`).

## Output

Report:

- the class covered (and why, if you proposed it), and members skipped as trivial;
- each dependency and how the tests supplied it (real, fake, null logger, fake time);
- tests and theory rows added, with results;
- the break-check result;
- known-issues entries added, and any `InternalsVisibleTo` added.
