# Umbrella.DataAccess Analyzers

Roslyn analyzers that enforce Umbrella repository naming and API shape conventions across data-access layers.

Most rules are configured with **Warning** severity; UDA005 is **Error** (compile blocking). Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.DataAccess.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID     | Title                                                                     | Description                                                                                                       |
|--------|---------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| UDA001 | Single-result repository queries must start with 'Find'                   | Enforces the `Find*` prefix for query methods returning a single entity, projection, or aggregate result.         |
| UDA002 | Collection repository queries must start with 'FindAll'                   | Enforces the `FindAll*` prefix for query methods returning a sequence, dictionary, tuple collection, or page.     |
| UDA003 | Count repository queries must start with 'Find' and identify the count    | Accepts names such as `FindCountByStatusAsync` and `FindUnreadCountByRecipientIdAsync`.                            |
| UDA004 | Boolean existence queries must start with 'Exists'                        | Enforces the `Exists*` prefix for query methods returning `bool` or a wrapped boolean result.                      |
| UDA005 | Repository methods must not return IQueryable\<T\>                        | Prevents `IQueryable<T>` leaking directly or through wrappers from public repository methods.                     |

### Severity

UDA001–UDA004 emit diagnostics as `Warning` so non-compliant names are flagged without blocking the build by default. They classify eligible public methods on Umbrella EF Core and EF6 repository descendants by return shape. Recognized command methods such as `Create*`, `Update*`, `Delete*`, and `Reload*` are outside the naming rules even when they return an entity or operation result. Other names are validated, so an arbitrary method returning a single item must still follow the `Find*` convention.

Return shapes are resolved semantically. The analyzers recognize `Task<T>`, `ValueTask<T>`, arrays, standard enumerable contracts, dictionaries, paginated result descendants, collection-bearing tuples, and single-payload generic result wrappers.

UDA005 emits as `Error` because exposing `IQueryable<T>` through repository boundaries is an architectural violation. It also detects derived query contracts such as `IOrderedQueryable<T>` and queryable payloads nested inside tasks, tuples, arrays, and non-delegate generic wrappers. Adjust severities via `.editorconfig` if needed.

## Release Tracking

Rule introduction and status are tracked in:
- `AnalyzerReleases.Unshipped.md`
- `AnalyzerReleases.Shipped.md`

## Usage

1. Add the package reference.
2. Build or open the solution in an IDE with Roslyn analyzer support (VS / Rider / `dotnet build`).
3. Fix reported diagnostics.

## Example EditorConfig Override

```ini
# Promote naming violations to errors
dotnet_diagnostic.UDA001.severity = error
dotnet_diagnostic.UDA002.severity = error
```
