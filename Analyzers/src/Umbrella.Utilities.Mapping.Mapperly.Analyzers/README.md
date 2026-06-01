# Umbrella.Utilities.Mapping.Mapperly Analyzers

Roslyn analyzers that validate Mapperly mapper registrations and class declarations for the Umbrella `IUmbrellaMapper` infrastructure.

UMA001 is configured with **Error** severity (compile blocking); UMA002 and UMA003 are **Warning**. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.Utilities.Mapping.Mapperly.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID     | Title                                                              | Description                                                                                                                                                                   |
|--------|--------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| UMA001 | IUmbrellaMapper calls must target an exact Mapperly registration   | Every closed `MapAsync` / `MapAllAsync` call site must have a matching `[UmbrellaMapperlyCatalogMapping]` entry in one of the configured catalogs. Missing registrations are a compile error. |
| UMA002 | Open generic IUmbrellaMapper calls cannot be fully validated       | Call sites using open generic type arguments (e.g. `TSource`) cannot be validated at the generic definition site and are flagged as warnings for manual review.                |
| UMA003 | Mapperly mapper classes must be public partial class               | Classes decorated with `[Mapper]` must be `public partial class`. The `Umbrella.Generators.Mapperly` source generator only discovers public types; a non-public or non-partial class is silently skipped and never registered in the catalog. |

### Configuring catalog references

The analyzer validates call sites against the catalogs declared on the consuming assembly. Add the attribute to the consuming project (typically in `IServiceCollectionExtensions.cs`):

```csharp
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(MyApp_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog))]
```

Without this attribute, UMA001/UMA002 remain silent (no catalogs → no validation).

### Severity

UMA001 emits as `Error` because an unregistered mapping fails at runtime. UMA002 emits as `Warning` because open generics are inherently unresolvable at the definition site. UMA003 emits as `Warning` because a non-public/non-partial mapper is silently ignored rather than producing a runtime error. Adjust severities via `.editorconfig` if needed.

## Release Tracking

Rule introduction and status are tracked in:
- `AnalyzerReleases.Unshipped.md`
- `AnalyzerReleases.Shipped.md`

## Usage

1. Add the package reference.
2. Add `[assembly: UmbrellaMapperlyCatalogReference(typeof(...))]` to the consuming project.
3. Build or open the solution in an IDE with Roslyn analyzer support (VS / Rider / `dotnet build`).
4. Fix reported diagnostics.

## Example EditorConfig Override

```ini
# Downgrade missing-registration to warning during migration
dotnet_diagnostic.UMA001.severity = warning
```
