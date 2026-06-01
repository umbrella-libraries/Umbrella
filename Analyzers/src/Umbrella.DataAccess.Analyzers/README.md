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
| UDA001 | Repository methods returning a single item must start with 'FindBy'       | Enforces the `FindBy*` prefix for methods returning a single entity or model.                                     |
| UDA002 | Repository methods returning a collection must start with 'FindAllBy'     | Enforces the `FindAllBy*` prefix for methods returning a sequence or collection.                                  |
| UDA003 | Repository methods returning a count must start with 'FindCount'          | Enforces the `FindCount*` prefix for methods whose return type is a numeric count.                                |
| UDA004 | Repository methods returning a boolean must start with 'Exists'           | Enforces the `Exists*` prefix for methods returning `bool` or `Task<bool>`.                                       |
| UDA005 | Repository methods must not return IQueryable\<T\>                        | Prevents `IQueryable<T>` leaking out of repository methods — callers must receive materialised results only.      |

### Severity

UDA001–UDA004 emit diagnostics as `Warning` so non-compliant names are flagged without blocking the build by default. UDA005 emits as `Error` because exposing `IQueryable<T>` through repository boundaries is an architectural violation. Adjust severities via `.editorconfig` if needed.

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
