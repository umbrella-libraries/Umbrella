# Umbrella analyzer compatibility

Use this reference when an Umbrella skill or agent creates or changes C# or Razor. The installed analyzer packages remain authoritative; inspect their current diagnostics when a build disagrees with this summary.

Current coverage: UA001, UA002, UA003, UA004, UA005, UA006, UA007, UA008, UA009, UA010, UA011, UA012, UA013, UA014, UA015, UA016, UA017, UA018, UA019, UA020, UA021, UA022, UA023, UA024; UDA001, UDA002, UDA003, UDA004, UDA005; UMA001, UMA002, UMA003; UWDI001, UWDI002, UWDI003, UWDI004, UWDI005.

## Public method contracts (UA001-UA010, UA016, UA020)

- Use `is null`, `is not null`, `is <constant>`, and `is not <constant>` where UA001/UA002 apply. Equality between two runtime values remains valid, and expression-tree code is excluded.
- A developer-controlled public `async` `Task`/`ValueTask` method uses exactly `CancellationToken cancellationToken = default`. Its first statement is `cancellationToken.ThrowIfCancellationRequested();`. Overrides, interface implementations, framework entry points, Blazor component methods, and test entry points can follow their required external signature.
- Public collection parameters and return payloads expose read-only contracts. Prefer `IReadOnlyCollection<T>` for materialized values and `IEnumerable<T>` for streams; never expose `List<T>`, mutable collection interfaces, arrays, or `IQueryable<T>` unless a documented analyzer exemption applies. Public collection return payloads are non-nullable.
- Put argument guards and cancellation checks before the outer `try`. Operational public instance methods on service-like types require an accessible `ILogger` and state-aware broad-exception logging. Log meaningful parameter/local state; bodyless Mapperly declarations do not activate logging, but developer-authored mapper bodies do.
- Do not use primary constructors on non-record classes or structs.
- Public query contracts whose names begin with `Find`, `Get`, `Search`, `Lookup`, `Fetch`, or `Query` accept identifiers, values, or dedicated criteria—not entity instances or immediate entity sequences.

## Models (UA011-UA015, UA021-UA023)

- Model, view-model, model-base, view-model-base, and query-result types are records unless a framework base makes that impossible. Concrete record classes are sealed unless `[UmbrellaAllowUnsealedModel("reason")]` documents intentional inheritance; abstract records and record structs are exempt. Public settable model properties are normally `required` and `init`; getter-only properties are valid.
- Mark only concrete UI-bound or request-input models directly with `[UmbrellaInputModel]`. The marker is not inherited, is invalid on abstract types, and should not be the default for read or result models. It permits non-`required` properties and `set` accessors, but does not permit mutable collection contracts or missing getters.
- Use `[UmbrellaAllowNonRequiredProperty("reason")]` only for a justified single-property UA012 exception. Use `[UmbrellaAllowMutableProperty("reason")]` only for a justified UA013/UA014 exception. Do not use removed model-wide or mutable-collection opt-outs.
- Input models declaring mutable trimmable strings directly implement `IUmbrellaTrimmable`. Declare the type `partial` only when using the source-generated implementation; a manual implementation does not require `partial`. `[UmbrellaDoNotTrim]`, technical mutation, and exact concurrency-stamp implementations retain their analyzer exemptions.
- Collection properties expose read-only contracts even on input models unless the individual property has `[UmbrellaAllowMutableProperty("reason")]`.
- Entities and concrete update-input models use mutable `IConcurrencyStamp`; read and result models use `IReadOnlyConcurrencyStamp` with `required string ConcurrencyStamp { get; init; }`. A non-input model using the mutable contract reports UA023.
- The four model attributes live in the `Umbrella.Analyzers` namespace but ship in the **`Umbrella.Analyzers.Abstractions`** package, not in `Umbrella.Analyzers` itself. Every project receives that package as a global non-private reference because an applied attribute is written into assembly metadata and must be loadable at runtime. If the attributes fail to resolve, add the package (see `umbrella-dotnet-install-analyzers`) — do not drop the attribute or hand-declare a local copy of it.

## APIs, authorization, repositories, and mapping

- On Umbrella API controllers, use `[UmbrellaProducesResponseType]`, not raw `[ProducesResponseType]` (UA017).
- Authorization handlers call `context.Succeed(requirement)` only on approved paths and never call `context.Fail()` (UA018).
- Overrides of generic Umbrella controller endpoints call the exact base endpoint on every normal return path, use lifecycle hooks for customization, or use `[NonAction]` to disable the endpoint (UA019).
- Controller action parameters never declare a single `SortExpression<TItem>` or `FilterExpression<TItem>`; ApiExplorer flattens the non-collection complex parameter and OpenAPI document generation hangs. Declare the collection form (array or `IEnumerable<>`, as the Umbrella generic controller families do) or use `SortExpressionDescriptor` / `FilterExpressionDescriptor` (UA024).
- Repository query names match their result shape: `Find*` for single/count, `FindAll*` for collections, and `Exists*` for boolean existence checks. No public repository method exposes `IQueryable<T>`, directly or through a wrapper (UDA001-UDA005).
- Mapperly calls require a referenced generated catalog. `[Mapper]` classes/record classes are partial and accessible to the generated catalog; public and internal top-level mappers are supported. Prefer the async mapper interfaces when enrichment requires asynchronous work. Bodyless partial mappings are generated; authored method bodies follow the normal logging rules (UMA001-UMA003, UA008, UA016).

## Dynamic Image (UWDI001-UWDI005)

- When URL fingerprinting is active, every Dynamic Image `*Url` model property has a nullable matching `*VersionToken`, assignments populate the pair together, and model-bound Razor usages pass `VersionToken`.
- Enable cross-project checks with `UmbrellaDynamicImageEnableUrlFingerprinting=true` in the projects containing models, assignments, or Razor. A local explicit `EnableUrlFingerprinting` registration remains authoritative in its compilation.
- Variant-shaping Razor values must be literals or enum members. Runtime expressions report UWDI004 and are omitted from the catalog.
- Server-only catalog generation consumes local Razor plus explicitly named external source roots. Catalog names are non-empty and case-insensitively unique, and each physical Razor file has one catalog owner.
- `UmbrellaFileImagePreviewUpload` participates in token checking and variant discovery just like `UmbrellaDynamicImage`.
- Catalogs authorize URL-requested transforms; transparent WebP/AVIF negotiation does not require duplicate variants, while explicitly requested formats do.
- Configure HTTP caching per file mapping: `Public` for shareable/CDN content, `Private` for browser-only content, and `NoStore` for temporary or sensitive files. Long-lived caching requires fingerprints; missing/stale-token redirects remain non-cacheable.

## Completion check

Build the affected projects with their real analyzer packages enabled. Treat newly introduced diagnostics as defects in the generated or changed code; do not silence them globally unless the repository explicitly documents that policy.
