# Umbrella.WebUtilities.DynamicImage Analyzers

Roslyn analyzers that enforce DynamicImage URL/version-token pairing conventions across Umbrella model types, Blazor components, and tag helper usages.

UWDI001–UWDI003 are configured with **Error** severity (compile blocking); UWDI004 is **Warning**. All rules are inactive unless DynamicImage URL fingerprinting is explicitly enabled via `AddUmbrellaWebUtilitiesDynamicImage`. Add the package as a PrivateAssets dependency so it does not flow transitively.

## Installation

```xml
<PackageReference Include="Umbrella.WebUtilities.DynamicImage.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

## Rules

| ID      | Title                                                                               | Description                                                                                                                                                              |
|---------|-------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| UWDI001 | DynamicImage URL properties must declare matching version token properties           | Model types with a `*Url` DynamicImage property (e.g. `ImageUrl`) must also declare a matching `*VersionToken` property (e.g. `ImageVersionToken`) of type `string?`.   |
| UWDI002 | DynamicImage URL assignments must also assign matching version tokens               | Object initialisers and assignment statements that set a DynamicImage URL property must also set the corresponding `*VersionToken` property in the same construction/update flow. |
| UWDI003 | DynamicImage UI usages must assign VersionToken                                     | `UmbrellaDynamicImage` Blazor component usages and `DynamicImage` tag helper usages bound to a DynamicImage URL model property must also assign the `VersionToken` input. |
| UWDI004 | DynamicImage variant discovery coverage is reduced by non-static inputs             | DynamicImage usages with non-static variant-shaping inputs (e.g. a runtime width/height) reduce source-generated variant discovery coverage. This is a warning only and does not affect runtime rendering. |

### Activation

UWDI001–UWDI003 are gated on explicit URL-fingerprinting enablement. The analyzer detects the real
`AddUmbrellaWebUtilitiesDynamicImage` registration callback and enables these diagnostics only when its final direct
assignment to the real `DynamicImageMiddlewareOptions.EnableUrlFingerprinting` property is a compile-time constant
`true`.

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

### Severity

UWDI001–UWDI003 emit as `Error` because missing version tokens at these points produce broken URLs at runtime when fingerprinting is active. UWDI004 emits as `Warning` because incomplete source-generated variant coverage degrades tooling but does not break runtime behaviour. Adjust severities via `.editorconfig` if needed.

## Release Tracking

Rule introduction and status are tracked in:
- `AnalyzerReleases.Unshipped.md`
- `AnalyzerReleases.Shipped.md`

## Usage

1. Add the package reference.
2. Enable URL fingerprinting in your startup code (`EnableUrlFingerprinting = true`).
3. Build or open the solution in an IDE with Roslyn analyzer support (VS / Rider / `dotnet build`).
4. Fix reported diagnostics — add the missing `*VersionToken` properties, assignments, and component inputs.

## Example EditorConfig Override

```ini
# Downgrade variant-coverage warning to suggestion
dotnet_diagnostic.UWDI004.severity = suggestion
```
