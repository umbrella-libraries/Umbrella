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
| UMA001 | IUmbrellaMapper calls must target an exact Mapperly registration   | Every closed `MapAsync` / `MapAllAsync` call site must have a matching `[UmbrellaMapperlyCatalogMapping]` entry in a configured catalog. Missing registrations, including calls made without any configured catalog, are compile errors. |
| UMA002 | Open generic IUmbrellaMapper calls cannot be fully validated       | Open generic calls are validated against every known closed, non-abstract source type that inherits the containing implementation. A warning remains only when no complete set of closed constructions can be validated. |
| UMA003 | Mapperly mapper classes must be partial and accessible             | Classes and record classes decorated with `[Mapper]` must be partial and accessible to the generated catalog. Public and internal mapper types are supported; inaccessible nested mapper types are rejected. |

### Configuring catalog references

The analyzer validates call sites against the catalogs declared on the consuming assembly. Add the attribute to the consuming project (typically in `IServiceCollectionExtensions.cs`):

```csharp
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(MyApp_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog))]
```

Without this attribute, closed mapper calls produce UMA001 because none of their mappings are registered. Open calls continue through UMA002's closed-construction analysis.

### Severity

UMA001 emits as `Error` because an unregistered mapping fails at runtime. UMA002 emits as `Warning` when an open generic call cannot be resolved using known closed source constructions. UMA003 emits as `Warning` because an invalid mapper declaration prevents Mapperly or the generated catalog from using the mapper. Adjust severities via `.editorconfig` if needed.

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
