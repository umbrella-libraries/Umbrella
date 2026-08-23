# Umbrella.WebUtilities.DynamicImage Analyzers

Roslyn analyzers that enforce DynamicImage URL/version-token pairing conventions and verify source-generated variant discovery across Umbrella model types, Razor components, and tag helper usages.

UWDI001–UWDI003 are configured with **Error** severity (compile blocking) and are gated by explicit URL-fingerprinting enablement. UWDI004 is an always-active **Warning** because catalog discovery is independent of fingerprinting. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.WebUtilities.DynamicImage.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID      | Title                                                                               | Description                                                                                                                                                              |
|---------|-------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| UWDI001 | DynamicImage URL properties must declare matching version token properties           | Model types with a `*Url` DynamicImage property (e.g. `ImageUrl`) must also declare a matching `*VersionToken` property (e.g. `ImageVersionToken`) of type `string?`.   |
| UWDI002 | DynamicImage URL assignments must also assign matching version tokens               | Object initialisers and assignment statements that set a DynamicImage URL property must also set the corresponding `*VersionToken` property in the same construction/update flow. |
| UWDI003 | DynamicImage UI usages must assign VersionToken                                     | `UmbrellaDynamicImage`, `UmbrellaFileImagePreviewUpload`, and `DynamicImage` tag helper usages bound to a DynamicImage URL model property must also assign the `VersionToken` input. Direct Razor source is checked, including ordinary and null-conditional model bindings. |
| UWDI004 | DynamicImage variant discovery coverage is reduced by non-static inputs             | Dynamic Image usages with variant-shaping inputs other than literals or enum members cannot be added safely to the generated catalog. The diagnostic points to the original Razor attribute and the entire occurrence is omitted from generation. |
| UWDI005 | Dynamic Image catalog configuration is invalid                                     | The generator reports an error for empty or conflicting catalog names, or when more than one catalog owns the same physical Razor file. |

### Activation

UWDI001–UWDI003 are gated on explicit URL-fingerprinting enablement. In the project containing the real
`AddUmbrellaWebUtilitiesDynamicImage` registration callback, its direct assignment to the real
`DynamicImageMiddlewareOptions.EnableUrlFingerprinting` property is authoritative and diagnostics are enabled only
when the final assignment is the compile-time constant `true`.

Projects which do not contain the registration can participate in the same contract by setting the compiler-visible
MSBuild property centrally:

```xml
<PropertyGroup>
  <UmbrellaDynamicImageEnableUrlFingerprinting>true</UmbrellaDynamicImageEnableUrlFingerprinting>
</PropertyGroup>
```

The analyzer package must be installed directly in each project containing model declarations, mapping assignments,
or Dynamic Image UI usages that should be checked. A missing, invalid, or `false` build-property value leaves the
rules disabled. A local registration remains authoritative over the build property.

UWDI001–UWDI003 remain disabled when:

- the property is not assigned;
- it is assigned `false`;
- its value cannot be determined at compile time; or
- it is assigned conditionally or through control flow that prevents the analyzer from proving it is enabled.

This is intentionally independent of the runtime option's default value. Applications must opt into analyzer
enforcement explicitly in their registration code.

UWDI004 is independent of URL fingerprinting and remains active when fingerprinting is unset or disabled. Static
variant discovery is required by the source-generated catalog regardless of whether generated image URLs are
fingerprinted.

### Server-only catalog generation

Install `Umbrella.Generators.DynamicImage` in the Server project. Its `buildTransitive` targets expose the project's
`.razor` and `.cshtml` files directly to the generator without disabling the modern Razor source generator. Additional
source roots can be assigned to named catalogs:

```xml
<PropertyGroup>
  <UmbrellaDynamicImageCatalogName>Server</UmbrellaDynamicImageCatalogName>
</PropertyGroup>

<ItemGroup>
  <UmbrellaDynamicImageSourceRoot Include="..\MyApp.Client" CatalogName="Client" />
</ItemGroup>
```

The generator emits `ServerDynamicImageVariantCatalog`, `ClientDynamicImageVariantCatalog`, and a sorted,
deduplicated `DynamicImageVariantCatalog` aggregate in the `Umbrella.Generated.DynamicImage` namespace. Register
multiple named catalogs with `AddAllowedVariantCatalogs`.

Razor discovery honours effective `_Imports.razor`, `_ViewImports.cshtml`, `@using`, `@addTagHelper`, and
`@removeTagHelper` directives. Variant-shaping values must be numeric/string literals or enum members. Compile-time
constant references and other Razor expressions are intentionally not discoverable and report UWDI004.
`FocalPointX`/`FocalPointY` and `focal-point-x`/`focal-point-y` are runtime inputs rather than variant-shaping inputs;
model expressions are supported, the coordinates are excluded from generated variant identity, and they do not report UWDI004.
`UmbrellaFileImagePreviewUpload` is treated as a Dynamic Image rendering component: its static preview dimensions and
format settings contribute variants to the catalog and its URL/version-token pair is checked like a direct
`UmbrellaDynamicImage` usage.
Its `EnableFocalPointSelection` input is variant-shaping and must be a literal. A literal `true` adds uncropped
`ScaleDown` selector variants alongside the configured crop variants for every effective density, size width, and
automatic picture format. Runtime-bound selection flags report UWDI004. The preview's `FocalPointX` and
`FocalPointY` inputs remain runtime-bound and do not change catalog identity.

### Severity

UWDI001–UWDI003 emit as `Error` because missing version tokens at these points produce broken URLs at runtime when fingerprinting is active. UWDI004 emits as `Warning` because incomplete source-generated variant coverage degrades tooling but does not break runtime behaviour. UWDI005 is an `Error` emitted by the generator because ambiguous catalog ownership would make the generated result unreliable. Adjust analyzer severities via `.editorconfig` if needed.

## Release Tracking

Rule introduction and status are tracked in:
- `AnalyzerReleases.Unshipped.md`
- `AnalyzerReleases.Shipped.md`

## Usage

1. Add the package reference.
2. Enable URL fingerprinting in startup code (`EnableUrlFingerprinting = true`) and, for multi-project applications,
   set `UmbrellaDynamicImageEnableUrlFingerprinting=true` centrally.
3. Build or open the solution in an IDE with Roslyn analyzer support (VS / Rider / `dotnet build`).
4. Fix reported diagnostics — add the missing `*VersionToken` properties, assignments, and component inputs.

## Example EditorConfig Override

```ini
# Downgrade variant-coverage warning to suggestion
dotnet_diagnostic.UWDI004.severity = suggestion
```
