# Dynamic Image contract reference

## Package and project layout

Keep package versions aligned with the repository's central version policy.

```xml
<!-- Every project containing checked models, assignments, or Razor -->
<PackageReference Include="Umbrella.WebUtilities.DynamicImage.Analyzers"
                  Version="<aligned-version>"
                  PrivateAssets="all" />

<!-- Server only -->
<PackageReference Include="Umbrella.Generators.DynamicImage"
                  Version="<aligned-version>"
                  PrivateAssets="all" />
```

NuGet places both packages under `analyzers/dotnet/cs`. Do not add project-reference-only metadata such as `OutputItemType` or `ReferenceOutputAssembly` to package references.

For cross-compilation fingerprint analysis:

```xml
<PropertyGroup>
  <UmbrellaDynamicImageEnableUrlFingerprinting>true</UmbrellaDynamicImageEnableUrlFingerprinting>
</PropertyGroup>
```

A compilation containing the real `AddUmbrellaWebUtilitiesDynamicImage` callback uses its explicit `EnableUrlFingerprinting` assignment as authoritative. Missing, invalid, conditional, or `false` activation disables UWDI001-UWDI003 in that compilation. UWDI004 remains active.

## Server-only catalogs

```xml
<PropertyGroup>
  <UmbrellaDynamicImageCatalogName>Server</UmbrellaDynamicImageCatalogName>
</PropertyGroup>

<ItemGroup>
  <UmbrellaDynamicImageSourceRoot Include="..\MyApp.Web.Client"
                                  CatalogName="Client" />
</ItemGroup>
```

The generator reads `.razor` and `.cshtml` source directly and excludes normal `bin`, `obj`, and `node_modules` paths. It emits named catalogs plus `Umbrella.Generated.DynamicImage.DynamicImageVariantCatalog`, the sorted deduplicated union.

Each source root requires a non-empty catalog name. Catalog names are case-insensitively unique across distinct catalogs. Multiple roots may contribute to one named catalog, but a physical Razor file must have one owner.

Razor discovery understands:

- `UmbrellaDynamicImage`;
- `UmbrellaFileImagePreviewUpload`;
- `dynamic-image` and `dynamic-source` tag helpers;
- effective `_Imports.razor`, `_ViewImports.cshtml`, `@using`, `@addTagHelper`, and `@removeTagHelper` directives.

Width, height, density, and size-width values must be literals. Resize mode and image format may use enum-member syntax. Do not use constant references, `@Model` bindings, or mixed literal/expression strings for variant-shaping inputs. UWDI004 identifies unsupported inputs and the generator omits the whole occurrence rather than emitting a false default variant.

## Runtime registration

Follow the application's existing file-provider construction and dependency-resolution conventions. The essential shape is:

```csharp
_ = services.AddUmbrellaWebUtilitiesDynamicImage((_, options) =>
{
    options.EnableUrlFingerprinting = true;
    options.Mappings =
    [
        new DynamicImageMiddlewareMapping
        {
            FileProviderMapping = publicImageProviderMapping,
            Cacheability = MiddlewareHttpCacheability.Public,
            MaxAgeSeconds = 31536000
        },
        new DynamicImageMiddlewareMapping
        {
            FileProviderMapping = privateImageProviderMapping,
            Cacheability = MiddlewareHttpCacheability.Private,
            MaxAgeSeconds = 31536000
        },
        new DynamicImageMiddlewareMapping
        {
            FileProviderMapping = temporaryFileProviderMapping,
            Cacheability = MiddlewareHttpCacheability.NoStore
        }
    ];

    _ = options.AddAllowedVariantCatalogs(
    [
        ClientDynamicImageVariantCatalog.All,
        ServerDynamicImageVariantCatalog.All
    ]);
});
```

Configure picture-source formats in the MVC tag-helper and Blazor options. WebP is the safe default for every resizer; enable AVIF only when the server uses NetVips:

```csharp
options.PictureSourceFormats =
[
    DynamicImageFormat.Avif,
    DynamicImageFormat.WebP
];
```

Use either the named catalogs or the aggregate catalog. Do not register both unless duplicate registration is intentional; the `HashSet` deduplicates entries, but duplication obscures ownership.

Catalog variants authorize transforms requested by the URL. Automatic `UmbrellaDynamicImage`, `UmbrellaFileImagePreviewUpload`, and `dynamic-image` usages register their fallback, WebP, and AVIF variants because runtime picture-source options can select those explicit URLs. Manual `dynamic-source` usages register only their declared format.

## URL and version-token propagation

```csharp
UmbrellaVersionedUrl? image = await fileHandler
    .GetVersionedWebFilePathAsync(entity.Id, entity.ImageProviderFileName, cancellationToken)
    .ConfigureAwait(false);

model.ImageUrl = image?.Url;
model.ImageVersionToken = image?.VersionToken;
```

Use asynchronous mapper interfaces for this enrichment. For a bounded page of independent items, resolve pairs concurrently and apply them in the same index/order as the mapped models. Do not start unbounded work over arbitrary streams.

```razor
<UmbrellaDynamicImage Url="@Model.ImageUrl"
                      VersionToken="@Model.ImageVersionToken"
                      WidthRequest="400"
                      HeightRequest="200"
                      ResizeMode="DynamicResizeMode.Crop"
                      ImageFormat="DynamicImageFormat.Jpeg" />

<UmbrellaFileImagePreviewUpload Url="@Model.ImageUrl"
                                VersionToken="@Model.ImageVersionToken"
                                WidthRequest="400"
                                HeightRequest="400" />
```

Static external HTTP(S) URLs do not create local variants.

## HTTP behavior

- A current fingerprinted URL returns the configured mapping policy.
- A missing or stale token redirects to the canonical URL with `Cache-Control: no-store`.
- If fingerprinting is enabled but no token can be produced, the response must not receive long-lived caching.
- `Public` permits shared/CDN caching; `Private` permits browser caching only; `NoStore` forbids storage.
- The response format is determined only by the requested URL; middleware does not inspect `Accept` or emit `Vary: Accept`.
- ETag is emitted from the shared version-token algorithm. Last-Modified is also emitted when provider metadata supplies it.
- Matching `If-None-Match`, or `If-Modified-Since` when supported, returns `304 Not Modified`.
- Altered transform inputs and explicit formats remain subject to global limits and generated allow-list validation.

## Verification probes

Use temporary, uncommitted probes when existing source does not exercise a rule:

- remove a model token to prove UWDI001;
- assign only a URL to prove UWDI002;
- omit a component `VersionToken` to prove UWDI003;
- bind a shaping input to a runtime expression to prove UWDI004;
- create conflicting catalog ownership to prove UWDI005.

Remove probes and generated artifacts before the final build. For browser validation, distinguish a fresh-cache hit (no request) from explicit conditional revalidation (`304`). Both are valid but prove different behavior.
