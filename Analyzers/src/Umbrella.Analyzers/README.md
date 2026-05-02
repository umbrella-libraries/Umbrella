# Umbrella Analyzers

A collection of Roslyn analyzers enforcing Umbrella coding standards, async patterns, and model immutability across .NET solutions.

All rules are configured with **Error** severity (compile blocking) to guarantee issues are fixed rather than ignored. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID    | Title                                                                 | Description                                                                                                   |
|-------|-----------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| UA001 | Use pattern matching for null checks                                  | Enforces `is null` / `is not null` instead of `== null` / `!= null`.                                           |
| UA002 | Use pattern matching for primitive and enum comparisons               | Enforces `is` / `is not` instead of `==` / `!=` for primitive & enum constants.                                |
| UA003 | Async methods should have a CancellationToken parameter               | Requires `CancellationToken cancellationToken = default` for async Task / ValueTask methods.                  |
| UA004 | Async methods with CancellationToken should call ThrowIfCancellationRequested | Ensures the first statement calls `cancellationToken.ThrowIfCancellationRequested()`.                |
| UA005 | Collection parameters should use IEnumerable<T>                       | Requires collection-like parameters to use `IEnumerable<T>` rather than concrete or more specific collection types. |
| UA006 | Collection return types should be IReadOnlyCollection<T>              | Requires methods returning collections to return `IReadOnlyCollection<T>` instead of concrete mutable types.       |
| UA007 | Collection return types must be non-nullable                          | Ensures collection return types are not declared nullable (for example avoid `IEnumerable<T>?`).                  |
| UA008 | Public methods must be wrapped in try...catch                         | Enforces a top-level try/catch (with optional structured logging) in public methods.                          |
| UA009 | Parameter validation must precede first try and never be inside try   | Guard / Throw helper / direct Argument* throws must appear before the first try and not inside any try block.|
| UA010 | Primary constructors are not allowed                                  | Forbids primary constructors on non-record classes / structs (`class C(int x)` / `struct S(int x)`).          |
| UA011 | Model types must be records                                           | Types named `*Model`, `*ViewModel`, `*ModelBase`, `*ViewModelBase`, or `*QueryResult` must be declared as `record`. |
| UA012 | Model properties must use the required keyword                        | Properties on model/QueryResult types must use the `required` modifier.                                       |
| UA013 | Model properties must have getter and be init-only                    | Properties on model/QueryResult types must use `{ get; init; }` accessors.                                    |
| UA014 | Collection properties must use IReadOnlyCollection\<T\>               | Collection properties on model/QueryResult types must be typed as `IReadOnlyCollection<T>`.                   |

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
All analyzers emit diagnostics as `Error` so builds fail until issues are resolved. Adjust severities via ruleset / .editorconfig if you need a softer adoption path.

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
