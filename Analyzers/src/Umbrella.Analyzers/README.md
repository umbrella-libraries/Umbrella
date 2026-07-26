# Umbrella Analyzers

A collection of Roslyn analyzers enforcing Umbrella coding standards, async patterns, and model immutability across .NET solutions.

Most rules are configured with **Error** severity (compile blocking); UA015, UA016, UA017, and UA019 are **Warning** severity. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID    | Title                                                                 | Description                                                                                                   |
|-------|-----------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| UA001 | Use pattern matching for null checks                                  | Enforces `is null` / `is not null` instead of `== null` / `!= null`.                                           |
| UA002 | Use pattern matching for primitive and enum comparisons               | Enforces `is` / `is not` instead of `==` / `!=` for primitive & enum constants.                                |
| UA003 | Async methods should have a CancellationToken parameter               | Requires eligible public async Task / ValueTask methods to declare exactly `CancellationToken cancellationToken = default`. |
| UA004 | Async methods with CancellationToken should call ThrowIfCancellationRequested | For UA003-eligible methods with the canonical token parameter, requires the first statement to call `cancellationToken.ThrowIfCancellationRequested()`. |
| UA005 | Collection parameters should use read-only collection types            | Requires mutable collection parameters on changeable public methods to use a read-only contract, except DI `IServiceCollection`, binary `byte[]` buffers, and FilterExpression/SortExpression arrays. |
| UA006 | Collection return types should be read-only                           | Requires direct or generically wrapped collection payloads on changeable public methods to use read-only types, except DI `IServiceCollection` and binary `byte[]` buffers. |
| UA007 | Collection return types must be non-nullable                          | Ensures direct or generically wrapped collection payloads on changeable public methods are not nullable. |
| UA008 | Public instance methods with an ILogger should use state-aware exception handling | Requires public instance methods on logger-owning types to place operational code in an outer try/catch and log every caught exception with explicit state when meaningful parameters exist. Static methods and types without an accessible ILogger are excluded. |
| UA009 | Argument and cancellation validation must precede exception handling  | Guard calls, Argument* throw helpers/direct throws, and CancellationToken.ThrowIfCancellationRequested must appear before the first try and never inside a try block. |
| UA010 | Primary constructors are not allowed                                  | Forbids primary constructors on non-record classes / structs (`class C(int x)` / `struct S(int x)`).          |
| UA011 | Model types must be records                                           | Types named `*Model`, `*ViewModel`, `*ModelBase`, `*ViewModelBase`, or `*QueryResult` must be declared as `record`. |
| UA012 | Model properties must use the required keyword                        | Properties on model/QueryResult types must use the `required` modifier.                                       |
| UA013 | Model properties must have getter and be init-only                    | Properties on model/QueryResult types must use `{ get; init; }` accessors.                                    |
| UA014 | Collection properties must use IReadOnlyCollection\<T\>               | Collection properties on model/QueryResult types must be typed as `IReadOnlyCollection<T>`.                   |
| UA015 | Model records must be partial when IUmbrellaTrimmable is used         | When `IUmbrellaTrimmable` is present in the compilation, model records must be `partial` to enable source generation. |
| UA016 | Input model records with string properties must implement IUmbrellaTrimmable | Create/Update model records with string properties must implement `IUmbrellaTrimmable` so user input is trimmed. |
| UA017 | Use [UmbrellaProducesResponseType] instead of [ProducesResponseType]  | Methods on `UmbrellaApiController` subclasses must use `[UmbrellaProducesResponseType]` rather than the raw ASP.NET Core attribute. |
| UA018 | Do not call context.Fail() in HandleRequirementAsync                  | Calling `context.Fail()` inside `HandleRequirementAsync` silently breaks the authorization pipeline — remove the call. |
| UA019 | Controller endpoint override must call base method                    | Overriding a standard CRUD endpoint (`GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `PatchAsync`, `SearchAsync`, `SearchSlimAsync`) in a controller without calling `base.{method}()` skips base lifecycle hooks. Use Before/After lifecycle hook overrides for custom logic, or apply `[NonAction]` to intentionally disable the endpoint. |
| UA020 | Entity types must not be used as query method parameters              | Methods whose names start with `Find`, `Get`, `Search`, `Lookup`, `Fetch`, or `Query` must not accept parameters of a type implementing `IEntity<TEntityKey>`. Passing an entity as a query parameter treats it as a specification bag, coupling the query contract to the entity shape. Accept individual primitive values or a dedicated filter/query type instead. |

### Opt-out attributes (UA011–UA014)
When a justified exception is needed, apply one of these attributes (all require a `justification` string):

| Attribute | Effect |
|-----------|--------|
| `[UmbrellaExcludeFromModelStandards("reason")]` | Excludes the entire type from UA011–UA014 |
| `[UmbrellaAllowOptionalProperty("reason")]` | Allows a property without `required` (UA012) |
| `[UmbrellaAllowLateInitialization("reason")]` | Like above, but signals the property is set post-construction |
| `[UmbrellaAllowMutableProperty("reason")]` | Allows a `set` accessor instead of `init` (UA013) |
| `[UmbrellaAllowMutableCollection("reason")]` | Allows a mutable collection type instead of `IReadOnlyCollection<T>` (UA014) |

### Severity
UA001–UA014, UA018, and UA020 emit diagnostics as `Error` so builds fail until issues are resolved. UA015, UA016, UA017, and UA019 emit as `Warning` — they flag structural issues but do not block the build by default. Adjust severities via ruleset / .editorconfig if you need a different adoption path.

## Release Tracking
Rule introduction and status are tracked in:
- `AnalyzerReleases.Unshipped.md`
- `AnalyzerReleases.Shipped.md`

## Usage
1. Add the package reference.
2. Build or open solution in an IDE with Roslyn analyzer support (VS / Rider / `dotnet build`).
3. Fix reported diagnostics. Consider adding justification comments only where an opt-out (if later provided) is allowed.

## Design Principles
- Prefer immutable / read-only abstractions.
- Fail fast on invalid parameters (before any try/catch logic).
- Enforce consistent async & cancellation patterns.
- Discourage language features that hinder clarity (e.g., primary constructors here).

## Contributing
Issues and pull requests are welcome. When adding a new rule:
1. Implement analyzer (and optional code fix) in `src`.
2. Add unit tests in the `test` project (positive & negative cases).
3. Update `AnalyzerReleases.Unshipped.md` and this README rule table.
4. Ensure all tests pass (`dotnet test`).

## Example EditorConfig Override
If you need to downgrade a rule severity temporarily:
```ini
# Soften primary constructor restriction
dotnet_diagnostic.UA010.severity = warning
```

## Disclaimer
These analyzers target `netstandard2.0` (broad IDE support) and are validated against modern .NET (6+). Some rules assume modern C# syntax (e.g., pattern matching) and may report more aggressively on legacy codebases.
