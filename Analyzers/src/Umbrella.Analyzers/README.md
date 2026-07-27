# Umbrella Analyzers

A collection of Roslyn analyzers enforcing Umbrella coding standards, async patterns, and model immutability across .NET solutions.

Most rules are configured with **Error** severity (compile blocking); UA015, UA017, and UA019 are **Warning** severity. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID    | Title                                                                 | Description                                                                                                   |
|-------|-----------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| UA001 | Use pattern matching for null checks                                  | Enforces `is null` / `is not null` instead of `== null` / `!= null`.                                           |
| UA002 | Use pattern matching for primitive and enum comparisons               | Enforces `is` / `is not` instead of `==` / `!=` for primitive & enum constants.                                |
| UA003 | Async methods should have a CancellationToken parameter               | Requires eligible public async Task / ValueTask methods to declare exactly `CancellationToken cancellationToken = default`. Framework-discovered middleware entry points are excluded. |
| UA004 | Async methods with CancellationToken should call ThrowIfCancellationRequested | For UA003-eligible methods with the canonical token parameter, requires the first statement to call `cancellationToken.ThrowIfCancellationRequested()`. |
| UA005 | Collection parameters should use read-only collection types            | Requires mutable collection parameters on changeable public methods to use a read-only contract, except DI `IServiceCollection`, binary `byte[]` buffers, and FilterExpression/SortExpression arrays. |
| UA006 | Collection return types should be read-only                           | Requires direct or generically wrapped collection payloads on changeable public methods to use read-only types, except DI `IServiceCollection` and binary `byte[]` buffers. |
| UA007 | Collection return types must be non-nullable                          | Ensures direct or generically wrapped collection payloads on changeable public methods are not nullable. |
| UA008 | Public instance methods with an ILogger should use state-aware exception handling | Requires eligible public instance methods on logger-owning types to place operational code in an outer try/catch and log broad `System.Exception` catches with explicit state when meaningful parameters exist. Safe state declarations may precede the try. Bodyless declarations and generated implementations, including Mapperly-generated mappings, are excluded; developer-authored mapper method bodies remain eligible. Framework entry points, `[DoesNotReturn]` control-flow methods, disposal methods, direct base forwarders, and true no-ops are also excluded. |
| UA009 | Argument and cancellation validation must precede exception handling  | Guard calls, Argument* throw helpers/direct throws, and CancellationToken.ThrowIfCancellationRequested must appear before the first try and never inside a try block. |
| UA010 | Primary constructors are not allowed                                  | Forbids primary constructors on non-record classes / structs (`class C(int x)` / `struct S(int x)`).          |
| UA011 | Model types must be records                                           | Types named `*Model`, `*ViewModel`, `*ModelBase`, `*ViewModelBase`, or `*QueryResult` must be declared as `record`. Nested UI-state and paginated models remain eligible; ASP.NET Core Razor Pages `PageModel` descendants are excluded from the model standards rules. |
| UA012 | Model properties must use the required keyword                        | Public instance settable properties on model/QueryResult types must use the `required` modifier. Static, non-public, getter-only, input-model, and model-interface properties are excluded; individual properties can use `[UmbrellaAllowNonRequiredProperty]`. |
| UA013 | Model properties must have getter and be init-only                    | Public instance properties on model/QueryResult types must have a getter and normally use `{ get; init; }`. Getter-only properties are valid. Input-model hierarchies may use `set`; model interfaces are excluded; and a concrete property may use `set` when required to implement an interface setter such as `IConcurrencyStamp.ConcurrencyStamp`. |
| UA014 | Collection properties must use read-only collection types             | Collection properties on model/QueryResult types must expose a read-only collection contract or a recognized immutable collection type. Input models remain subject to this rule unless the property uses `[UmbrellaAllowMutableProperty]`. |
| UA015 | Input models declaring mutable string properties must implement IUmbrellaTrimmable | When `IUmbrellaTrimmable` is present, types in an `[UmbrellaInputModel]` hierarchy that declare public instance `string` properties with a non-init setter must directly implement the interface. Properties marked `[UmbrellaAllowMutableProperty]` or `[UmbrellaDoNotTrim]`, and exact `IConcurrencyStamp.ConcurrencyStamp` implementations, are excluded. Partial types can use source generation; non-partial types can provide the implementation manually. |
| UA017 | Use [UmbrellaProducesResponseType] instead of [ProducesResponseType]  | Controller classes and public MVC action candidates on `UmbrellaApiController` descendants must use the generic or non-generic `[UmbrellaProducesResponseType]` attribute rather than raw ASP.NET Core response type attributes. Generated, static, non-public, generic, `[NonAction]`, and `[NonController]` code is excluded. |
| UA018 | Authorization handlers must not call context.Fail()                   | Authorization handlers must call `context.Succeed(requirement)` only for approved cases and otherwise leave the requirement unsatisfied so another handler can still approve it. |
| UA019 | Controller endpoint override must call base method                    | Overrides of public HTTP endpoints declared by `UmbrellaGenericRepositoryApiController` or `UmbrellaGenericRepositoryDataServiceApiController` must directly call the exact overridden base endpoint on every normal return path. This preserves the base repository/data-service pipeline and lifecycle hooks. Use lifecycle hook overrides for custom logic, or apply `[NonAction]` to intentionally disable the endpoint. |
| UA020 | Entity values must not be used as query criteria                      | Changeable public methods beginning with `Find`, `Get`, `Search`, `Lookup`, `Fetch`, or `Query` must not accept an entity, entity array, or immediate entity sequence. Entity-shaped infrastructure such as include maps, filter/sort expressions, and expression trees remains valid. Accept identifiers, scalar/value types, or a dedicated query criteria type instead. |
| UA021 | Types with public operational methods should provide an ILogger       | Requires a class or record containing non-trivial public instance methods to expose an accessible `ILogger` or `ILogger<T>` through a field, property, captured primary-constructor parameter, or inheritance. Bodyless declarations and generated implementations do not create a logging requirement, but developer-authored mapper method bodies do. The remaining operational-method exclusions match UA008: framework entry points, `[DoesNotReturn]` control-flow methods, disposal and string-trimming implementations, direct base forwarders, recognized test entry points, and true no-ops. Entity types implementing `IEntity<T>` are also excluded because they are not dependency-injection service boundaries. |

### Model attributes (UA012–UA014)
Use the type-level input marker for models populated by UI/model binding. Use a narrowly scoped
property attribute, with a justification, for exceptional properties. Attribute matching uses the
exact `Umbrella.Analyzers` symbol; similarly named application attributes have no effect.

| Attribute | Effect |
|-----------|--------|
| `[UmbrellaInputModel]` | On a model type or base type, allows non-`required` properties (UA012) and `set` accessors (the mutability part of UA013), and identifies user-input types for UA015. It does not suppress UA011, UA014, or UA013 for a missing getter. |
| `[UmbrellaAllowNonRequiredProperty("reason")]` | Allows one property to omit `required` (UA012). |
| `[UmbrellaAllowMutableProperty("reason")]` | Allows one property to use a `set` accessor (UA013), expose a mutable collection contract (UA014), or both. Technically mutable string properties using this attribute are not UA015 trimming candidates. It does not permit a missing getter. |

`[UmbrellaDoNotTrim]` is supplied by `Umbrella.Utilities` for string values where surrounding
whitespace is meaningful, such as passwords. The string-trimmer generator leaves decorated
properties unchanged, and UA015 does not count them as trimming candidates.

### Severity
UA001–UA014, UA018, UA020, and UA021 emit diagnostics as `Error` so builds fail until issues are resolved. UA015, UA017, and UA019 emit as `Warning` — they flag structural issues but do not block the build by default. Adjust severities via ruleset / .editorconfig if you need a different adoption path.

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
