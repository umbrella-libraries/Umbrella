---
name: dotnet-scaffold-mapperly-factories
description: 'Scaffold Mapperly mapper classes that map between EF Core entities and API model records, following the Umbrella UmbrellaMapper discovery pattern.'
---

# Scaffold Mapperly Factories

## Purpose

Create Mapperly mapper classes that map between EF Core entities and API model records. These mappers plug into the `UmbrellaMapper` infrastructure: at startup, `UmbrellaMapper` scans assemblies for all public types implementing Mapperly interfaces and discovers them automatically. No explicit DI registration is needed per mapper.

Mappers are the server-side complement to API models. The models skill (`dotnet-scaffold-api-server-models`) creates the record types; this skill creates the classes that produce them from entities.

## Discovery (read these before writing anything)

1. Read 2-3 existing mapper files in the `ModelFactories` project (e.g., `Web.Server.ModelFactories\Mappings\Api\`) to understand class visibility, naming, and how manual properties are handled.
2. Note whether the project puts all mappers for a feature in one file (e.g., `<Feature>Mappers.cs`) or one class per file.
3. Confirm `AddUmbrellaUtilitiesMappingMapperly` is called somewhere (typically `Program.cs` or a startup extension) and note whether it uses `TargetAssemblies` or `TargetAssemblyNamePrefix`.
4. Check the project `.csproj` for global usings — typically `Riok.Mapperly.Abstractions` and `Umbrella.Utilities.Mapping.Mapperly.Abstractions` are pre-configured.

---

## How UmbrellaMapper discovers mappers

`UmbrellaMapper` calls `Assembly.GetExportedTypes()` on the configured assemblies, then scans for types implementing `IUmbrellaMapperlyNewInstanceMapper<,>`, `IUmbrellaMapperlyNewCollectionMapper<,>`, and `IUmbrellaMapperlyExistingInstanceMapper<,>`. It instantiates each mapper using `ActivatorUtilities.CreateInstance`, so constructor injection works normally.

**Consequence:** mapper classes must be `public`. An `internal` mapper will not be found and will silently do nothing.

---

## Interface reference

| Interface | Method signature | Use for |
|---|---|---|
| `IUmbrellaMapperlyNewInstanceMapper<TSource, TDest>` | `TDest Map(TSource source)` | Entity → model (GET single) |
| `IUmbrellaMapperlyNewCollectionMapper<TSource, TDest>` | `IReadOnlyCollection<TDest> MapAll(IEnumerable<TSource> source)` | Entity → model collection (list) |
| `IUmbrellaMapperlyExistingInstanceMapper<TSource, TDest>` | `void Map(TSource source, TDest destination)` | Request model → entity (update) |

A class can implement any combination of these on the same source/destination pair. It cannot implement the same interface twice with different type arguments — use a separate class in that case.

**Choosing which mapper interfaces to implement**

Only create mappings for the model types you scaffolded. Skip any direction for which no corresponding model exists.

| Endpoint / model | Mapper interface to implement |
|---|---|
| GET single (`<Name>Model`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, <Name>Model>` |
| GET list (`Slim<Name>Model`) | `IUmbrellaMapperlyNewCollectionMapper<Entity, Slim<Name>Model>` |
| POST request (`Create<Name>Model → Entity`) | `IUmbrellaMapperlyNewInstanceMapper<Create<Name>Model, Entity>` |
| POST result (`Entity → Create<Name>ResultModel`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, Create<Name>ResultModel>` (separate class) |
| PUT request (`Update<Name>Model → Entity`) | `IUmbrellaMapperlyExistingInstanceMapper<Update<Name>Model, Entity>` |
| PUT result (`Entity → Update<Name>ResultModel`) | `IUmbrellaMapperlyNewInstanceMapper<Entity, Update<Name>ResultModel>` (separate class) |

For example, a create-only analytics controller only needs the POST request row (and POST result if the result model has auto-mappable properties). A read-list-only controller only needs the GET list row.

---

## Step 1 -- Create the mapper file

**File location:** `Web.Server.ModelFactories\Mappings\Api\<Feature>Mappers.cs`

All mapper classes for a feature go in a single file. Use numbered suffixes when a feature needs multiple classes.

**Minimal mapper (all properties auto-mapped by Mapperly):**

```csharp
using <AppName>.Core.Domain.Entities;
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Server.ModelFactories.Mappings.Api;

[Mapper]
public partial class <Name>Mapper :
    IUmbrellaMapperlyNewInstanceMapper<<Name>Entity, <Name>Model>,
    IUmbrellaMapperlyNewCollectionMapper<<Name>Entity, Slim<Name>Model>
{
    public partial <Name>Model Map(<Name>Entity source);
    public partial IReadOnlyCollection<Slim<Name>Model> MapAll(IEnumerable<<Name>Entity> source);
}
```

`partial` methods with no body are auto-implemented by Mapperly's source generator.

**Mapper with properties that need manual values (e.g., file URLs):**

When a destination property cannot be auto-mapped, use `[MapperIgnoreTarget]` on a private partial method and write a public wrapper:

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

**Bidirectional mapper (entity ↔ model — used for create/update):**

A single class can map in both directions if the type pairs are distinct:

```csharp
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

**Multiple mapper classes in the same file:**

When a class would implement the same interface twice with different type arguments (which C# forbids), use additional classes with numbered suffixes in the same file:

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

---

## Rules

- Always `public partial class` — never `internal`. `UmbrellaMapper` scans `GetExportedTypes()` and will not find non-public mappers.
- `[Mapper]` attribute on the class triggers Mapperly source generation for `partial` methods.
- `partial` methods with no body are auto-implemented by Mapperly. Methods with a body are your manual overrides.
- `[MapperIgnoreTarget(nameof(Prop))]` goes on the `partial` method (the one Mapperly implements), not on the class. Name the private method with an `Internal` suffix to avoid naming conflicts with the public interface method.
- When you write a public wrapper that calls a private `partial` method, add `Guard.IsNotNull(source)` at the top.
- All mapper classes for one feature go in one file named `<Feature>Mappers.cs`.
- No explicit DI registration is needed — `UmbrellaMapper` discovers and instantiates mappers automatically at startup.

---

## Step 2 -- Verify assembly scanning

Confirm the `ModelFactories` assembly is covered by `AddUmbrellaUtilitiesMappingMapperly`:

```csharp
_ = services.AddUmbrellaUtilitiesMappingMapperly((sp, options) =>
{
    // Explicit assembly list:
    options.TargetAssemblies = [typeof(SomeExistingMapper).Assembly];

    // Or by name prefix — all assemblies whose name starts with this:
    options.TargetAssemblyNamePrefix = "<AppName>.";
});
```

If `TargetAssemblyNamePrefix` already covers the app prefix, no change is needed for new mappers in the same assembly.

---

## Verification

1. All mapper classes are `public partial class` — not `internal`, not `sealed`.
2. Every `partial` method either has no body (Mapperly generates it) or has a body that performs manual post-mapping logic.
3. `[MapperIgnoreTarget(nameof(Prop))]` is placed on the private `partial` method, not on the class.
4. Public wrapper methods call `Guard.IsNotNull(source)`.
5. No explicit DI registration was added — `UmbrellaMapper` handles discovery.
6. The `ModelFactories` assembly is reachable by `UmbrellaMapper` (covered by `TargetAssemblies` or `TargetAssemblyNamePrefix`).
