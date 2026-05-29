---
name: dotnet-scaffold-mapperly-factories
description: 'Scaffold Mapperly mapper classes that map between EF Core entities and API model records, or between client-side model types, following the Umbrella source-generated catalog pattern.'
---

# Scaffold Mapperly Factories

## Purpose

Create Mapperly mapper classes for either:

- **Server-side** (`Web.Server.ModelFactories`): entity ↔ API model (GET, POST, PUT endpoints).
- **Client-side** (`Web.Client.Data`): API model → update model (to populate the edit form) and update-result model → update model (to refresh the form after save without a full page reload).

Mappers plug into the `UmbrellaMapper` infrastructure via a **source-generated catalog**. The `Umbrella.Generators.Mapperly` package scans the assembly at compile time, discovers all mapper classes, and emits a `{AssemblyName}UmbrellaMapperlyCatalog` class. That catalog is then passed to `AddUmbrellaUtilitiesMappingMapperly` at startup. No per-mapper DI registration is needed.

## Discovery (read these before writing anything)

1. Read 2–3 existing mapper files in the target project (server: `Web.Server.ModelFactories\Mappings\Api\`; client: `Web.Client.Data\Mappings\Api\`) to understand naming conventions and how manual properties are handled.
2. Check the project `.csproj` for `Umbrella.Generators.Mapperly` and global usings for `Riok.Mapperly.Abstractions` and `Umbrella.Utilities.Mapping.Mapperly.Abstractions`.
3. Check the consuming project's `Program.cs` for `AddUmbrellaUtilitiesMappingMapperly(...)` to understand which catalog(s) are already registered.
4. Check the consuming project's `IServiceCollectionExtensions.cs` for `[assembly: UmbrellaMapperlyCatalogReference(...)]`.

---

## How UmbrellaMapper discovers mappers

When you reference `Umbrella.Generators.Mapperly` in a project, the incremental source generator runs at compile time. It scans the assembly for all public non-abstract types implementing any of the six mapper interfaces and emits a catalog class:

- **Class name:** `{AssemblyName}UmbrellaMapperlyCatalog` — dots in the assembly name become underscores.
  - Example: `IndyRecords.Web.Server.ModelFactories` → `IndyRecords_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog`
- **Namespace:** `Umbrella.Generated.Mapping.Mapperly`

The catalog exposes a `static Instance` property and implements `IUmbrellaMapperlyCatalog`. At startup, the consuming project registers it:

```csharp
builder.Services.AddUmbrellaUtilitiesMappingMapperly(
    Umbrella.Generated.Mapping.Mapperly.MyApp_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog.Instance);
```

The Roslyn analyzer (`UA019`/`UA020`) validates `IUmbrellaMapper` call sites using the `[assembly: UmbrellaMapperlyCatalogReference(typeof(...))]` attribute on the consuming project — if a `MapAsync` call has no registered mapping, it emits an error at compile time.

**Consequence:** mapper classes must be `public`. An `internal` mapper is not discovered by the source generator and will silently do nothing.

---

## Interface reference

### Server-side (entity ↔ API model)

| Interface | Method signature | Use for |
|---|---|---|
| `IUmbrellaMapperlyNewInstanceMapper<TSource, TDest>` | `TDest Map(TSource source)` | Entity → model (GET single / POST/PUT result) |
| `IUmbrellaMapperlyNewCollectionMapper<TSource, TDest>` | `IReadOnlyCollection<TDest> MapAll(IEnumerable<TSource> source)` | Entity → model collection (GET list) |
| `IUmbrellaMapperlyExistingInstanceMapper<TSource, TDest>` | `void Map(TSource source, TDest destination)` | Request model → entity (PUT update) |

### Client-side (model → model, in `Web.Client.Data`)

| Interface | Method signature | Use for |
|---|---|---|
| `IUmbrellaMapperlyNewInstanceMapper<ManageModel, UpdateModel>` | `UpdateModel Map(ManageModel source)` | Populate edit form from loaded `ManageModel` |
| `IUmbrellaMapperlyExistingInstanceMapper<UpdateResultModel, UpdateModel>` | `void Map(UpdateResultModel source, UpdateModel destination)` | Refresh edit form (e.g. concurrency stamp) after successful save — avoids full page reload |

A class can implement any combination of interfaces on the same source/destination pair. It cannot implement the same interface twice with different type arguments — use a separate class with a numbered suffix in that case.

**Choosing which mapper interfaces to implement**

Only create mappings for model types that exist. Skip any direction for which no corresponding model exists.

| Endpoint / model | Mapper interface to implement |
|---|---|
| GET single (`<Name>Model`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, <Name>Model>` |
| GET list (`Slim<Name>Model`) | `IUmbrellaMapperlyNewCollectionMapper<Entity, Slim<Name>Model>` |
| POST request (`Create<Name>Model → Entity`) | `IUmbrellaMapperlyNewInstanceMapper<Create<Name>Model, Entity>` |
| POST result (`Entity → Create<Name>ResultModel`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, Create<Name>ResultModel>` (separate class) |
| PUT request (`Update<Name>Model → Entity`) | `IUmbrellaMapperlyExistingInstanceMapper<Update<Name>Model, Entity>` |
| PUT result (`Entity → Update<Name>ResultModel`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, Update<Name>ResultModel>` (separate class) |
| Edit form populate | `IUmbrellaMapperlyNewInstanceMapper<<Name>Model, Update<Name>Model>` (in Client.Data) |
| Edit form refresh after save | `IUmbrellaMapperlyExistingInstanceMapper<Update<Name>ResultModel, Update<Name>Model>` (in Client.Data) |

---

## Step 1 -- Create the mapper file

### Server-side mapper

**File location:** `Web.Server.ModelFactories\Mappings\Api\<Feature>Mappers.cs`

**Minimal mapper (all properties auto-mapped):**

```csharp
using <AppName>.Core.Domain.Entities;
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Server.ModelFactories.Mappings.Api;

[Mapper]
public partial class <Name>Mapper :
    IUmbrellaMapperlyNewInstanceMapper<<Name>Entity, <Name>Model>,
    IUmbrellaMapperlyNewCollectionMapper<<Name>Entity, Slim<Name>Model>,
    IUmbrellaMapperlyNewInstanceMapper<Create<Name>Model, <Name>Entity>,
    IUmbrellaMapperlyExistingInstanceMapper<Update<Name>Model, <Name>Entity>
{
    public partial <Name>Model Map(<Name>Entity source);
    public partial IReadOnlyCollection<Slim<Name>Model> MapAll(IEnumerable<<Name>Entity> source);
    public partial <Name>Entity Map(Create<Name>Model source);
    public partial void Map(Update<Name>Model source, <Name>Entity destination);
}
```

**Mapper with properties needing manual values (e.g., file URLs):**

```csharp
using <AppName>.Core.Domain.Entities;
using <AppName>.Core.Logic.FileSystem.Abstractions;
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Server.ModelFactories.Mappings.Api;

[Mapper]
public partial class <Name>Mapper :
    IUmbrellaMapperlyNewInstanceMapper<<Name>Entity, <Name>Model>,
    IUmbrellaMapperlyNewCollectionMapper<<Name>Entity, Slim<Name>Model>
{
    private readonly I<Name>FileHandler _fileHandler;

    public <Name>Mapper(I<Name>FileHandler fileHandler)
    {
        _fileHandler = fileHandler;
    }

    public <Name>Model Map(<Name>Entity source)
    {
        Guard.IsNotNull(source);

        var model = MapInternal(source);
        model.ImageUrl = _fileHandler.GetWebFilePath(source.ImageProviderFileName, source.Id);

        return model;
    }

    [MapperIgnoreTarget(nameof(<Name>Model.ImageUrl))]
    private partial <Name>Model MapInternal(<Name>Entity source);

    public IReadOnlyCollection<Slim<Name>Model> MapAll(IEnumerable<<Name>Entity> source)
    {
        Guard.IsNotNull(source);

        var models = MapAllInternal(source);

        foreach (var (entity, model) in source.Zip(models))
        {
            model.ImageUrl = _fileHandler.GetWebFilePath(entity.ImageProviderFileName, entity.Id);
        }

        return models;
    }

    [MapperIgnoreTarget(nameof(Slim<Name>Model.ImageUrl))]
    private partial IReadOnlyCollection<Slim<Name>Model> MapAllInternal(IEnumerable<<Name>Entity> source);
}
```

**Multiple mapper classes (when the same interface can't be implemented twice):**

```csharp
[Mapper]
public partial class <Name>Mapper2 : IUmbrellaMapperlyNewInstanceMapper<<Name>Entity, Create<Name>ResultModel>
{
    public partial Create<Name>ResultModel Map(<Name>Entity source);
}

[Mapper]
public partial class <Name>Mapper3 : IUmbrellaMapperlyNewInstanceMapper<<Name>Entity, Update<Name>ResultModel>
{
    public partial Update<Name>ResultModel Map(<Name>Entity source);
}
```

### Client-side mapper (in `Web.Client.Data\Mappings\Api\`)

```csharp
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Client.Data.Mappings.Api;

[Mapper]
public partial class <Name>Mapper :
    IUmbrellaMapperlyNewInstanceMapper<<Name>Model, Update<Name>Model>,
    IUmbrellaMapperlyExistingInstanceMapper<Update<Name>ResultModel, Update<Name>Model>
{
    public partial Update<Name>Model Map(<Name>Model source);
    public partial void Map(Update<Name>ResultModel source, Update<Name>Model destination);
}
```

The first interface populates the edit form when loading an existing record. The second refreshes the form (especially `ConcurrencyStamp`) after a successful save, without a full page reload.

---

## Step 2 -- Verify source generator package in the mapper project

Open the `.csproj` for the mapper project and confirm:

```xml
<ItemGroup>
  <PackageReference Include="Umbrella.Generators.Mapperly" Version="..." />
  <PackageReference Include="Umbrella.Utilities.Mapping.Mapperly" Version="..." />
</ItemGroup>
<ItemGroup>
  <Using Include="Riok.Mapperly.Abstractions" />
  <Using Include="Umbrella.Utilities.Mapping.Mapperly.Abstractions" />
</ItemGroup>
```

If `Umbrella.Generators.Mapperly` is missing, add it (match the version of `Umbrella.Utilities.Mapping.Mapperly`). The generator emits the catalog class — without it, nothing is discovered.

---

## Step 3 -- Register the catalog in the consuming project's Program.cs

The consuming project (Web Server or Web Client) must pass the generated catalog instance to `AddUmbrellaUtilitiesMappingMapperly`. The generated class lives in namespace `Umbrella.Generated.Mapping.Mapperly` and its name is the assembly name with dots replaced by underscores, suffixed with `UmbrellaMapperlyCatalog`.

```csharp
// Web Server Program.cs — server-side catalog from ModelFactories assembly
builder.Services.AddUmbrellaUtilitiesMappingMapperly(
    Umbrella.Generated.Mapping.Mapperly.<AppName>_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog.Instance);

// Web Client Program.cs — client-side catalog from Client.Data assembly
builder.Services.AddUmbrellaUtilitiesMappingMapperly(
    Umbrella.Generated.Mapping.Mapperly.<AppName>_Web_Client_DataUmbrellaMapperlyCatalog.Instance);
```

If both catalogs are already registered (i.e. you are adding mappers to an existing assembly), no change is needed here.

---

## Step 4 -- Add the assembly attribute for the Roslyn analyzer

The consuming project's `IServiceCollectionExtensions.cs` must carry a `[assembly: UmbrellaMapperlyCatalogReference(typeof(...))]` attribute pointing to its generated catalog. This is what enables the UA019/UA020 diagnostic rules to validate `IUmbrellaMapper` call sites at compile time.

```csharp
// In Web.Server/IServiceCollectionExtensions.cs
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(Umbrella.Generated.Mapping.Mapperly.<AppName>_Web_Server_ModelFactoriesUmbrellaMapperlyCatalog))]

namespace <AppName>.Web.Server;

public static class IServiceCollectionExtensions { ... }
```

```csharp
// In Web.Client/IServiceCollectionExtensions.cs
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(Umbrella.Generated.Mapping.Mapperly.<AppName>_Web_Client_DataUmbrellaMapperlyCatalog))]

namespace <AppName>.Web.Client;

public static class IServiceCollectionExtensions { ... }
```

**Important:** the attribute lives in the consuming project (the one calling `AddUmbrellaUtilitiesMappingMapperly`), not in the mapper project itself. Web client and web server have separate catalogs and separate assembly attributes.

Note: the source generator also auto-emits a `[assembly: UmbrellaMapperlyCatalogReference]` attribute in the mapper project itself (pointing back to its own catalog). This is separate from — and in addition to — the manually-added attribute in the consuming project. When grepping for `UmbrellaMapperlyCatalogReference` you will see two occurrences per catalog: one in the generated `.g.cs` file in the mapper project, and one in the consuming project's `IServiceCollectionExtensions.cs`. The consuming-project attribute is the one the analyzer uses to validate call sites there.

If the attribute is already present in the consuming project, no change is needed — the analyzer will automatically pick up any new mappers that the source generator adds to the catalog.

---

## Rules

- Always `public partial class` — never `internal`. Non-public types are invisible to the source generator.
- `[Mapper]` attribute on the class triggers Mapperly source generation for `partial` methods.
- `partial` methods with no body are auto-implemented by Mapperly. Methods with a body are manual overrides.
- `[MapperIgnoreTarget(nameof(Prop))]` goes on the private `partial` method (the one Mapperly implements), not on the class.
- When you write a public wrapper that calls a private `partial` method, add `Guard.IsNotNull(source)` at the top.
- All mapper classes for one feature go in one file named `<Feature>Mappers.cs`.
- No per-mapper DI registration is needed — the generated catalog handles all registrations.
- The generated catalog class name is `{AssemblyName_Dots_Replaced_By_Underscores}UmbrellaMapperlyCatalog` in namespace `Umbrella.Generated.Mapping.Mapperly`.

---

## Verification

1. All mapper classes are `public partial class` — not `internal`, not `sealed`.
2. Every `partial` method either has no body (Mapperly generates it) or has a body for manual post-mapping logic.
3. `[MapperIgnoreTarget(nameof(Prop))]` is placed on the private `partial` method.
4. Public wrapper methods call `Guard.IsNotNull(source)`.
5. The mapper project has `Umbrella.Generators.Mapperly` in its `.csproj` with global usings for `Riok.Mapperly.Abstractions` and `Umbrella.Utilities.Mapping.Mapperly.Abstractions`.
6. The consuming project's `Program.cs` passes the generated catalog to `AddUmbrellaUtilitiesMappingMapperly(...)`.
7. The consuming project's `IServiceCollectionExtensions.cs` has `[assembly: UmbrellaMapperlyCatalogReference(typeof(...))]` pointing to the generated catalog.
8. Web client and web server have separate catalogs — each registered and attributed independently.
