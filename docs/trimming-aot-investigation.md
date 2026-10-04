# Umbrella trimming and AOT investigation

Investigated on 5 October 2026 against the current working tree using .NET SDK 10.0.401 on Windows x64. This is an investigation, not a compatibility declaration. No product code, project settings, or package versions were changed.

Umbrella can support trimming and AOT incrementally, but a blanket promise across all packages would be misleading. The shared utility/validation layer needs work first; browser/mobile consumers need their own published application tests; MVC and dynamic EF repositories need a different support boundary.

## Previous work

- `759293b51`, 16 September 2020: introduced replaceable JSON implementations in `UmbrellaStatics` for Xamarin iOS AOT. The abstraction remains, but its current default uses reflection-based System.Text.Json. Replacing the delegate alone does not express a statically checked metadata contract.
- `a33e9df57`, 31 July 2024: “Initial work to make the Blazor assembly trim safe.” Added `RequiresUnreferencedCode` to injected property setters and preservation annotations to a component base. Much of this remains.
- Existing source-generated JSON contexts cover selected date-range, key/value, grid, and health-report models. These are useful foundations, but not comprehensive.
- No checked-in `IsTrimmable`, `IsAotCompatible`, `EnableTrimAnalyzer`, or `EnableAotAnalyzer` settings were found in the runtime project/build files. No dedicated trimmed/AOT consumer publish gate was found in the reviewed build configuration.
- `Umbrella.Generators.StringTrimmer`, `IUmbrellaTrimmable`, and analyzer rules about model string trimming remove whitespace from strings. They are unrelated to IL trimming.

## What was verified

Source and project inspection covered runtime package families. Diagnostic .NET 10 Release rebuilds enabled trim, AOT, and single-file analyzers through command-line properties, after restoring with those same properties. This is important: setting analyzer properties only on a no-restore build did not acquire the linker analyzer package and initially produced no IL diagnostics.

The following entry projects built successfully: Utilities, Blazor, ASP.NET Core WebUtilities, EntityFrameworkCore, Csv, FileSystem.Disk, Mapping.Mapperly, FileSystem.AzureStorage, AzureServiceBus, Threading.Redis, DynamicImage.NetVips, and DynamicImage.SkiaSharp. Their project references provided additional coverage. The last five adapter rebuilds used `BuildProjectReferences=false` after referenced Release assemblies had been built.

There were **139 distinct build diagnostics** after deduplicating file, line, column, warning code, and message across logs. Counts below include generated Razor/JSON source where applicable, exclude ordinary code-style warnings, and count diagnostics rather than independent defects.

| Assembly producing diagnostics | Distinct IL diagnostics |
| --- | ---: |
| Umbrella.Utilities | 49 |
| Umbrella.DataAnnotations | 8 |
| Umbrella.AspNetCore.Blazor | 19 |
| Umbrella.AspNetCore.WebUtilities | 30 |
| Umbrella.DataAccess.Abstractions | 6 |
| Umbrella.DataAccess.EntityFrameworkCore | 22 |
| Umbrella.FileSystem.Abstractions | 1 |
| Umbrella.WebUtilities | 4 |
| **Total** | **139** |

Codes: IL2026 (45), IL2067 (5), IL2070 (8), IL2072 (2), IL2073 (1), IL2075 (6), IL2090 (1), IL2091 (39), IL2111 (1), IL2112 (1), IL3050 (30). The adjacent [diagnostic inventory](trimming-aot-build-diagnostics.csv) records the individual locations and messages.

AppFramework, AppFramework.Shared, AspNetCore.Shared, DynamicImage.Abstractions, Mapping.Mapperly, Csv, FileSystem.Disk, AzureStorage, AzureServiceBus, Redis, NetVips, and SkiaSharp emitted no IL diagnostics of their own in these checks. This does **not** establish compatibility: build analyzers do not analyze dependency method bodies, application-provided types, or every runtime path.

A temporary `net10.0` console consumer was published for `win-x64`, self-contained, with `PublishTrimmed=true`, `TrimMode=full`, and `TrimmerSingleWarn=false`. `Umbrella.Utilities` and `Umbrella.DataAnnotations` were rooted with `TrimmerRootAssembly` to expose whole-library issues. The actual linker ran and emitted **48 trim-analysis warnings**, including additional warnings inside HybridCache's default serializer.

The published executable called:

```csharp
UmbrellaStatics.SerializeJson(new ProbeModel { Name = "Umbrella" });
```

It caught `InvalidOperationException`: reflection-based serialization had been disabled and generated metadata/a resolver was required. The probe exited 1. This demonstrates a current failure under default full-trimming settings; it does not mean every use of Utilities fails. The rooting used for analysis is intentionally broader than a typical consumer's reachable feature set.

No Native AOT binary, browser AOT app, or platform-specific MAUI app was published. No end-to-end database, cloud, image, or CSV smoke test was run. .NET 8/9 analyzer baselines remain to be measured. Third-party dependency compatibility is not certified by this investigation.

## Separate the support promises

| Goal | Implication for Umbrella |
| --- | --- |
| Trimming | Preserve members required by reachable reflection and expose requirements to consumers. Validate full trimming in published applications. |
| Blazor WebAssembly AOT | Validate a browser consumer using `RunAOTCompilation=true`; this is the WebAssembly/Mono toolchain, separate from server Native AOT. |
| Mobile AOT | Test the actual mobile runtime and platform. Existing Xamarin/Mono AOT behavior is not proof of Native AOT support. |
| Native AOT | Requires trimming plus statically available executable code/generic instantiations. Validate a native executable for each supported deployment platform. |

[Native AOT guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/), [Blazor WebAssembly build guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0), and [MAUI Native AOT guidance](https://learn.microsoft.com/en-us/dotnet/maui/deployment/nativeaot?view=net-maui-10.0) explain the respective toolchains.

Existing `net8.0;net9.0;net10.0` targets already provide an appropriate place for modern annotations and analyzers. Keep legacy `net462`/`netstandard` assets available; do not enable trimming/AOT properties indiscriminately on those assets. Roslyn analyzers/generators run in the compiler host and are not application runtime compatibility targets. Legacy System.Web/EF6 and test infrastructure should have an explicit separate scope.

## Concrete work required

### 1. Give serialization a generated-metadata contract

Highest priority locations:

- `Core/src/Umbrella.Utilities/UmbrellaStatics.cs:93,102`: defaults serialize an arbitrary object/runtime `Type` using options without generated metadata; IL2026 and IL3050.
- `Core/src/Umbrella.Utilities/Http/Extensions/HttpClientExtensions.cs:34`: generic reflection serialization.
- `Core/src/Umbrella.Utilities/Spatial/PostcodesIOGeocodingService.cs`: HTTP JSON calls for known models can use generated contexts.
- `AspNetCore/src/Umbrella.AspNetCore.WebUtilities/Cookie/JsonCookieService.cs:37,58`: generic cookies need consumer-provided metadata.
- MVC JSON/data-expression binders deserialize runtime model types and create generic collections dynamically.
- `Core/src/Umbrella.WebUtilities/SiteMap/XmlSiteMap.cs:53,54`: XmlSerializer paths produce trim/AOT warnings; explicit XML writing is a promising replacement for this known sitemap format.

Add `JsonTypeInfo<T>`/`JsonSerializerContext` overloads or a service that resolves explicitly registered generated type metadata. Generate contexts inside Umbrella for Umbrella-owned DTOs; applications must supply metadata for application DTOs and supported closed generic response types. Preserve camel-case and case-insensitive behavior deliberately.

Retain old reflection APIs as compatibility paths with accurate `RequiresUnreferencedCode`/`RequiresDynamicCode` contracts where necessary. Updating a global serializer delegate does not by itself make the default safe or convey preservation requirements to the analyzer. Re-enabling reflection globally is not a Native AOT solution.

The health writer already uses a source-generated context, but `HealthReportResultModel.Status` selects the non-generic `JsonStringEnumConverter`. Generated code still reports IL3050. Use an appropriate generic enum converter or source-generation enum settings and verify the wire format. Audit HybridCache value serialization too: configure generated serializers for supported value types rather than rely on its default reflection-based serializer.

[System.Text.Json source-generation guidance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation).

### 2. Annotate DI/reflection contracts and fix small helpers

Straightforward candidates include:

- DI registration/replacement wrappers in `Umbrella.Utilities/IServiceCollectionExtensions.cs`: propagate `PublicConstructors` requirements on implementation generic parameters and runtime `Type` parameters.
- `UmbrellaFileStorageProviderFactory` and `AddUmbrellaRateLimiting`: propagate the same implementation-constructor contracts.
- Type helper methods: preserve only the properties/interfaces/methods actually required, and propagate annotations through callers.
- Runtime enum helpers: prefer statically typed generic enum APIs or suitable underlying-value APIs instead of `Enum.GetValues(Type)` when possible.
- `RangeIfAttribute.OperandType`: the existing `All` annotation promises more than the base getter provides; resolve the data-flow mismatch rather than expand preservation blindly.

`LazyProxy<T>` and generic repository/controller `Lazy<T>` uses also produce IL2091. Review the factory-based lazy behavior carefully; do not add a `new()` constraint to service interfaces or entities just to silence analysis. A narrowly justified suppression may be appropriate only where the explicit factory guarantees no reflective construction, backed by published tests.

Annotations are contracts, not magic preservation switches for arbitrary object graphs. `DynamicallyAccessedMembers` on a generic parameter or `Type` can describe bounded reflection; annotating one parent type does not describe all nested runtime model types.

### 3. Redesign unbounded model validation and logging paths

`ObjectGraphValidator`, `UmbrellaValidator`, `AsyncValidator`, contingent validation, and `ValidationAttributeStore` discover properties/attributes from `object.GetType()`, property names, nested models, and TypeDescriptor. Standard DataAnnotations calls also carry trim requirements. Blazor and MAUI validators consume these shared paths.

Introduce statically registered/generated model metadata or validators for the supported route, including nested objects, collections, conditional/dependent properties, display names, and asynchronous validation. Keep reflection fallback explicitly marked for conventional applications. This is a larger design task than attaching a preservation attribute to one method.

`ILoggerExtensions.cs:202` reflects over arbitrary logging state, commonly anonymous objects. Prefer structured logging, generated LoggerMessage methods, or explicit key/value state so logging does not require runtime property discovery. Cover exception logging as well as successful paths.

### 4. Provide a static route through dynamic filters/sorts

`DataExpressionFactory` reflects over expression types and activates them with runtime constructor arguments. `UmbrellaDynamicExpression` finds property paths and `Parse` methods by name and uses dynamically inferred delegate types. MVC binders add runtime array/generic-list construction.

An AOT-compatible route should use typed factories, explicitly supported property/path catalogs, typed parsing, and known generic instantiations. A source generator could emit catalogs from application models. Keep the existing flexible API as an explicitly unsupported fallback where its requirements cannot be expressed reliably.

Do not classify every `Expression.Compile()` as an unconditional Native AOT failure: supported expression trees can be interpreted under Native AOT. The concrete blockers here include unbounded member discovery, delegate type inference, and generic construction; interpretation can also change performance.

### 5. Revisit the 2024 Blazor annotations

The current build produces IL2026 from generated Razor code that uses components whose injected setters carry `RequiresUnreferencedCode("Dynamically set by Dependency Injection")`. That attribute declares a potentially unsafe call; it does not preserve an injected property. A class preservation annotation also exposes a setter marked unsafe, producing IL2112.

Audit component type/property preservation against current Blazor contracts; remove inappropriate warning declarations only after the required members and types are demonstrably preserved. Propagate generic parameter requirements for input/radio components and statically describe the types passed to DynamicComponent. Test the published browser app's injection, dialogs, grids, validation, input conversion, and storage/JSON features.

The MAUI package additionally creates string-path bindings in the responsive/dynamic image markup extensions and reflects over models in `MauiValidationUtility`. A Native AOT route needs supported compiled bindings/generated validation; platform-specific consumers must prove it.

### 6. Set a realistic server/data-access boundary

ASP.NET Core MVC remains unsupported by Native AOT in the current [.NET 10 feature matrix](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-10.0). Umbrella's controller/model-binder/tag-helper/view functionality cannot obtain server Native AOT support through annotations alone. It can remain usable in conventional server apps while other features get separate compatibility contracts.

`Umbrella.AspNetCore.WebUtilities` also contains middleware, health checks, and a version endpoint. Consider isolating an AOT-oriented runtime package or a clearly documented supported subset. `MapSystemVersionGet` currently warns on library-level `MapGet` and `Results.Json`; provide generated/static request handling and JSON metadata, and validate the result in a real Minimal API application. Merely using the Minimal API name is insufficient.

EF Core's [Native AOT/query-precompilation support](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries) remains experimental and is not recommended for production. Its documented unsupported dynamic query composition directly affects `ReadOnlyGenericDbRepository` filtering, shaping, sorting, and string-based includes. Compiled models alone cannot address this. Retain the generic repository's conventional-runtime scope, or design separate static queries/storage services for a future AOT path. There are also actionable constructor/member annotation mismatches in Umbrella's EF wrappers and contracts, independent of that broader architectural limitation.

### 7. Validate dependency bodies and deployment assets

| Family | Proposed handling based on this investigation |
| --- | --- |
| Mapperly and generated catalogs | Strong first candidate; statically generated mappings/registration already exist. Verify consumers and base Utilities dependencies. |
| AutoMapper 10.0.0 | Treat its runtime mapping/configuration path as a separate high-risk dependency. Prefer the existing Mapperly route for AOT; no compatibility claim for this version was established. |
| CsvHelper 33.1.0 | Umbrella auto-maps arbitrary model types. Dependency inspection confirms reflection-driven expression mapping; a warning-free wrapper build is insufficient. Root/publish supported CSV models and exercise reads/writes; investigate explicit/generated mappings or isolate this feature. |
| Azure storage/Service Bus and Redis | Wrapper builds had no IL warnings. Publish dependency graphs and exercise serialization/configuration and client creation. |
| NetVips, SkiaSharp, FreeImage | Verify managed interop code plus native assets/RIDs, library discovery, callbacks, and real image processing. Native libraries are not inherently incompatible with AOT. FreeImage was source-reviewed but not diagnostically rebuilt here. |
| Graph, Dataverse, logging adapters | Require exact-version dependency/publish checks; not certified here. Configuration/type discovery and SDK serialization deserve particular attention. |
| AppFramework/shared abstractions | Promising once common utility/validation contracts are addressed. Zero local warnings do not remove downstream requirements. |
| Legacy, compiler tools, test infrastructure | Separate from the runtime-library compatibility rollout. Tests hosted by the JIT do not prove a consumer survives trimming. |

The existing unmanaged assembly loader loads native libraries; it is not evidence of dynamic managed plugin loading. Inspect its actual callers before classifying it as a hard blocker.

Any later dependency upgrades should use the repository's safe-upgrade workflow, exclusions, framework-specific versions, restore, and resolved-package checks. No upgrade is proposed as a substitute for analyzing actual call paths.

## Recommended rollout

1. **Establish a baseline and support matrix.** Add opt-in analyzer builds for eligible runtime projects/TFMs and a diagnostic inventory. Keep production compatibility metadata separate until contracts have been reviewed. Decide supported feature/platform combinations explicitly.
2. **Fix the shared foundation.** Start with constructor/type annotations, enum helpers, known-model JSON contexts, and structured logging. Design generated-metadata serialization and model validation interfaces before propagating those changes through clients.
3. **Certify a first vertical slice.** Publish a small console/worker using common Utilities features, generated Mapperly mappings, and one disk/cloud adapter. Exercise DI/lazy creation, JSON round trips, nested validation, and errors. Publish a rooted library-analysis app as well as a realistic consumer.
4. **Certify browser/mobile use separately.** Add a Blazor WebAssembly release/full-trim app, then a WebAssembly AOT build. Add actual MAUI platform smoke apps where support is intended. Cover components and generated application model metadata.
5. **Expand selected server/adapters.** Validate a Minimal API subset and exact dependency graphs. Keep MVC and dynamic EF repositories outside a Native AOT promise until an alternative architecture or framework support makes that viable.
6. **Publish the contract and enforce it in CI.** Set `IsTrimmable` or `IsAotCompatible` per eligible package/modern TFM after addressing the supported surface. Unsupported APIs can remain explicitly annotated; compatibility metadata does not mean those APIs become safe. Record which ones consumers must avoid.

Example of a modern-target condition for an eligible, reviewed library:

```xml
<PropertyGroup>
  <IsAotCompatible Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))">true</IsAotCompatible>
</PropertyGroup>
```

`IsAotCompatible` enables trimming, AOT, and single-file analyzers and signals the support contract. Do not set it repo-wide in Directory.Build.props for all tests, tools, legacy packages, and framework-specific packages. .NET 10 also offers opt-in reference compatibility metadata verification; missing metadata is useful triage evidence, not proof of failure.

CI needs separate full-trim and Native AOT publish/run jobs; enable detailed dependency warnings, fail on unapproved IL diagnostics, and exercise runtime behavior. Native publishing requires the platform compiler toolchain. Also inspect produced NuGet assets and test against packaged libraries, not solely project references. Prefer bounded annotations/generated code; use preservation descriptors and suppressions only for justified, tested invariants.

[Library trimming guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) describes rooted test apps and the warning/annotation workflow.

## Reproduce the diagnostic build

For a modern multi-targeted runtime entry project, substitute its project path in both commands:

```powershell
dotnet restore Core/src/Umbrella.Utilities/Umbrella.Utilities.csproj `
  -p:TargetFrameworks=net10.0 -p:EnableTrimAnalyzer=true `
  -p:EnableAotAnalyzer=true -p:EnableSingleFileAnalyzer=true

dotnet build Core/src/Umbrella.Utilities/Umbrella.Utilities.csproj `
  -f net10.0 -c Release --no-restore -t:Rebuild `
  -p:TargetFrameworks=net10.0 -p:EnableTrimAnalyzer=true `
  -p:EnableAotAnalyzer=true -p:EnableSingleFileAnalyzer=true
```

Use the same analyzer properties at restore and build, and select the intended framework. Restore the normal project configuration afterwards. These commands are diagnostics, not permanent project edits or an AOT certification. For a consumer publish probe, put PublishTrimmed/TrimMode/TrimmerRootAssembly in the consumer project and use `dotnet publish -f net10.0 -c Release -r win-x64 --self-contained true`; restrict dependency restore to compatible targets when using investigation-wide global properties.
