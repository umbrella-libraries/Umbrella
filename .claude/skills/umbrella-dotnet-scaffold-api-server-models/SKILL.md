---
name: umbrella-dotnet-scaffold-api-server-models
description: 'Scaffold API model records (request/response types) for a new feature, following the Umbrella shared models pattern with optional base class hierarchies.'
---

# Scaffold API Server Models

## Purpose

Create the API model records for a new feature — the request and response types used by API endpoints. These models are typically kept in a shared project (accessible by both server and client) and use C# records with data-annotation validation attributes.

This skill covers the model types only. For mapping entities to models using Mapperly, use the `umbrella-dotnet-scaffold-mapperly-factories` skill.

## Discovery (read these before writing anything)

1. Read 2-3 existing feature model folders in the shared models directory (e.g., `Web.Shared\Models\Api\`) to understand the naming conventions, which model types are used, and whether base class hierarchies are common.
2. Note whether the project uses `required` properties, `init` vs `set`, `abstract record` base types, and which validation attributes are standard (e.g., `[Required]`, `[ShortStringLength]`).
3. Check whether the project has a constants file pattern per feature (e.g., `<Feature>Constants.cs`) for error message strings used in validation attributes.

---

## Model types reference

Choose only the types the feature needs. Skip types that do not apply.

| Type name | Interfaces | Purpose |
|---|---|---|
| `<Name>Model` | `IKeyedItem<int>`, `IReadOnlyConcurrencyStamp` | Full read model — returned by GET single |
| `Slim<Name>Model` | `IKeyedItem<int>` | Slim read model — used as the item in paginated list responses |
| `Create<Name>Model` | (none) | Create request body |
| `Update<Name>Model` | `IUpdateModel<int>` | Update request body — includes `Id` and `ConcurrencyStamp` |
| `Create<Name>ResultModel` | `ICreateResultModel<int>` | Create operation result |
| `Update<Name>ResultModel` | `IUpdateResultModel` | Update operation result — returns the new `ConcurrencyStamp` |
| `<Name>PaginatedResultModel` (optional) | extends `PaginatedResultModel<Slim<Name>Model>` | Feature-specific paginated response, only when the contract needs a named wrapper or extra properties |

**Choosing which model types to scaffold**

Match the models you create to the endpoints you intend to enable. Models for disabled endpoints (those using `object` or `NoOp*` in the controller) do not need to exist.

| Endpoints enabled | Model types to scaffold |
|---|---|
| Full CRUD | All six non-pagination model types in the table; add a named paginated wrapper only when the surrounding contract uses one |
| Read list only | `Slim<Name>Model`; optionally a named paginated wrapper |
| Read list + detail | `Slim<Name>Model`, `<Name>Model`; optionally a named paginated wrapper |
| Create only (e.g. analytics, session recording) | `Create<Name>Model`, `Create<Name>ResultModel` |
| Read list + create (no update, no detail) | `Slim<Name>Model`, `Create<Name>Model`, `Create<Name>ResultModel`; optionally a named paginated wrapper |
| Read list + detail + create (no update) | `Slim<Name>Model`, `<Name>Model`, `Create<Name>Model`, `Create<Name>ResultModel`; optionally a named paginated wrapper |

Every list endpoint needs `Slim<Name>Model` and a paginated contract. Prefer `PaginatedResultModel<Slim<Name>Model>` directly when that matches nearby controllers and services. Create a feature-specific derived record only when nearby features consistently name the wrapper or the response needs additional properties. If there is no list endpoint, neither form is needed.

---

**Interface property requirements — accessor rules:**

- `IKeyedItem<int>` → `int Id { get; }` — `init` is fine here
- `IReadOnlyConcurrencyStamp` → `string ConcurrencyStamp { get; }` → declare `required string ConcurrencyStamp { get; init; }`. This is the contract for read and result models.
- `IConcurrencyStamp` → `string ConcurrencyStamp { get; set; }` — mutable `set` required; `init` does NOT satisfy this interface. Use it **only** on EF/Dataverse entities and on `IUpdateModel<TKey>` request models that need the stamp assigned after construction.
- `ICreateResultModel<int>` → `int Id { get; }` → declare `required int Id { get; init; }`
- `IUpdateModel<int>` inherits `IKeyedItem<int>` + `IConcurrencyStamp` → needs `int Id { get; }` and `string ConcurrencyStamp { get; set; }`
- `IUpdateResultModel` inherits `IReadOnlyConcurrencyStamp` → declare `required string ConcurrencyStamp { get; init; }`

Result models are always populated by a mapper, never by the controller base, so their `Id` and `ConcurrencyStamp` are init-only. Reaching the stamp through the mutable `IConcurrencyStamp` on a read or result model blocks `init` and will trigger UA013.

---

## Record declaration and input-model style

All analyzer-matched model types use `record`; project type does not determine whether they are `partial`. Use `partial` only when a source generator actually needs to add an implementation.

The `[UmbrellaInputModel]`, `[UmbrellaAllowNonRequiredProperty]` and `[UmbrellaAllowMutableProperty]` attributes used below are in the `Umbrella.Analyzers` namespace but ship in the **`Umbrella.Analyzers.Abstractions`** package. Before scaffolding, confirm the models project can resolve them; if not, add that package (without `PrivateAssets` — it is a runtime dependency) per `umbrella-dotnet-install-analyzers`. Do not work around an unresolved attribute by hand-declaring a local copy.

Models bound to UI or request inputs and intentionally using mutable setters belong to an `[UmbrellaInputModel]` hierarchy. The attribute permits `set` and non-`required` input properties, but it does not permit mutable collection contracts or missing getters. A type that declares mutable trimmable string properties must directly implement `IUmbrellaTrimmable`. Make that type `partial` when the installed trimming generator supplies the implementation; otherwise provide the interface implementation manually without forcing `partial`.

Read and result models normally remain immutable records with `required ... { get; init; }`. Do not mark them as input models merely because the same feature also has a Blazor form.

---

## Base class hierarchies (use when they add value)

When create and update models share the same editable properties, or when the read model shares display properties with the request models, a base class hierarchy reduces duplication and centralises validation attributes.

**When to introduce base classes:**
- The feature has both create and update models that share most properties
- Validation attributes (e.g., `[Required]`, `[MaxLength]`, `[Display]`) would otherwise be duplicated across multiple models
- The full read model shares display properties with the request models

**Typical hierarchy (used when warranted):**

```
<Name>ModelBase (immutable abstract record)
└── <Name>Model : <Name>ModelBase, IKeyedItem<int>, IConcurrencyStamp

[UmbrellaInputModel] CreateUpdate<Name>ModelBase (mutable abstract input record)
├── Create<Name>Model : CreateUpdate<Name>ModelBase
└── Update<Name>Model : CreateUpdate<Name>ModelBase, IUpdateModel<int>

CreateUpdate<Name>ResultModelBase (abstract record)
├── Create<Name>ResultModel : CreateUpdate<Name>ResultModelBase, ICreateResultModel<int>
└── Update<Name>ResultModel : CreateUpdate<Name>ResultModelBase, IUpdateResultModel
```

Notes:
- `<Name>ModelBase` holds immutable read-model properties. Do not reuse mutable UI-bound properties here.
- `[UmbrellaInputModel] CreateUpdate<Name>ModelBase` holds properties shared by create/update inputs. It does not inherit the read-model base because their accessor contracts differ.
- `Slim<Name>Model` usually does NOT inherit from `<Name>ModelBase` — it exists independently with only the fields needed in list views
- `CreateUpdate<Name>ResultModelBase` holds properties returned by both create and update results (e.g., `ConcurrencyStamp`, any computed properties like `ImageUrl`)
- A two-tier result base is only worth adding if there are genuinely shared result properties; if both results only return `ConcurrencyStamp`, a base is unlikely to add value

---

## Step 1 -- Create the model records

**Directory:** `Web.Shared\Models\Api\<Feature>\` (confirm with discovery)

**Abstract base (shared display + editable properties with validation):**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public abstract record <Name>ModelBase
{
    [Required(ErrorMessage = "<Name>Constants.NameRequiredErrorMessage")]
    [MaxLength(200)]
    public required string Name { get; init; }
    // other shared read properties
}
```

**Mutable input base (when create/update share fields):**

```csharp
using Umbrella.Analyzers;
using Umbrella.Utilities.Text;

namespace <AppName>.Web.Shared.Models.Api.<Feature>;

[UmbrellaInputModel]
public abstract partial record CreateUpdate<Name>ModelBase : IUmbrellaTrimmable
{
    [Required(ErrorMessage = "<Name>Constants.NameRequiredErrorMessage")]
    [MaxLength(200)]
    public string? Name { get; set; }
}
```

The `partial` declaration above assumes the trimming source generator is installed. If it is not, implement `IUmbrellaTrimmable` manually and omit `partial`. If the input type declares no mutable trimmable strings, omit the interface and `partial`.

**Full read model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record <Name>Model : <Name>ModelBase, IKeyedItem<int>, IReadOnlyConcurrencyStamp
{
    public required int Id { get; init; }
    public required string ConcurrencyStamp { get; init; }
    // Dynamic Image pairs populated by asynchronous enrichment, when used:
    // [UmbrellaAllowNonRequiredProperty("Populated after the generated mapping.")]
    // [UmbrellaAllowMutableProperty("Populated after the generated mapping.")]
    // public string? ImageUrl { get; set; }
    // [UmbrellaAllowNonRequiredProperty("Populated after the generated mapping.")]
    // [UmbrellaAllowMutableProperty("Populated after the generated mapping.")]
    // public string? ImageVersionToken { get; set; }
}
```

**Slim read model (for paginated list items):**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record Slim<Name>Model : IKeyedItem<int>
{
    public required int Id { get; init; }
    // only the fields shown in list views
}
```

**Create model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record Create<Name>Model : CreateUpdate<Name>ModelBase
{
    // add properties specific to creation only; leave empty if everything is on the base
}
```

**Update model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record Update<Name>Model : CreateUpdate<Name>ModelBase, IUpdateModel<int>
{
    public required int Id { get; init; }
    [Required]
    public required string ConcurrencyStamp { get; set; }
    // add properties specific to update only (e.g., ReplaceExistingImage)
}
```

Note: on this **request** model `ConcurrencyStamp` must use `set` (not `init`), because `IUpdateModel<TKey>` inherits the mutable `IConcurrencyStamp` so a Blazor form can two-way bind it and re-stamp it after a save. Mark it `required` and add `[Required]` so client-side validation enforces it. Result models are the opposite — they use `init`, see below.

**Create result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record Create<Name>ResultModel : ICreateResultModel<int>, IReadOnlyConcurrencyStamp
{
    public required int Id { get; init; }
    public required string ConcurrencyStamp { get; init; }
    // Dynamic Image pairs populated after saving, when used:
    // [UmbrellaAllowNonRequiredProperty("Populated after the file is saved.")]
    // [UmbrellaAllowMutableProperty("Populated after the file is saved.")]
    // public string? ImageUrl { get; set; }
    // [UmbrellaAllowNonRequiredProperty("Populated after the file is saved.")]
    // [UmbrellaAllowMutableProperty("Populated after the file is saved.")]
    // public string? ImageVersionToken { get; set; }
}
```

**Update result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record Update<Name>ResultModel : IUpdateResultModel
{
    public required string ConcurrencyStamp { get; init; }
    // any other values the server recomputes on update
}
```

**Optional named paginated result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public record <Name>PaginatedResultModel : PaginatedResultModel<Slim<Name>Model>;
```

`PaginatedResultModel<T>` is a record in current Umbrella versions, so a derived named model must also be a record. Do not add this wrapper when the controller, data service, and client can use `PaginatedResultModel<Slim<Name>Model>` directly.

---

## `required` keyword guidance

Use `required` on public settable properties unless an analyzer-recognized contract exempts them. `[UmbrellaInputModel]` deliberately permits form/request properties to be populated after construction. For an exceptional non-input property, use `[UmbrellaAllowNonRequiredProperty("reason")]`; do not omit `required` merely because a default initializer exists.

A property being nullable or frequently absent is **not** a reason to drop `required`. `public required string? Name { get; init; }` is valid and forces the mapper to make an explicit decision. Reserve `[UmbrellaAllowNonRequiredProperty]` for properties genuinely assigned after construction, and make the reason say what assigns them.

---

## Migrating an existing result model

Older result models were written as `public string ConcurrencyStamp { get; set; } = null!;` with `[UmbrellaAllowNonRequiredProperty("Populated after parameterless construction by the generic repository controller.")]`. That justification no longer holds: the controller base no longer constructs result models, so there is no parameterless-construction path and no `new()` constraint. To migrate:

1. `Update<Name>ResultModel` → `public required string ConcurrencyStamp { get; init; }`. Delete the suppression attribute and the `= null!` initialiser.
2. `Create<Name>ResultModel` → `public required TKey Id { get; init; }`, plus the same change for the stamp.
3. If a read or create-result model lists `IConcurrencyStamp` in its base list, change it to `IReadOnlyConcurrencyStamp`. Leaving the mutable interface in place keeps the setter slot and blocks `init`.
4. Leave `Update<Name>Model : IUpdateModel<TKey>` alone — its stamp keeps `{ get; set; }` for Blazor two-way binding and for the `updateModel.ConcurrencyStamp = result.Result.ConcurrencyStamp;` refresh after a save.
5. Any result-model property assigned by an `AfterCreateEntityAsync` / `AfterUpdateEntityAsync` override (e.g. `result.ImageUrl`) keeps `{ get; set; }` with `[UmbrellaAllowMutableProperty("reason")]`. Convert only `Id` and `ConcurrencyStamp`.
6. If a type genuinely cannot move, `[UmbrellaAllowMutableProperty("reason")]` suppresses the UA013 setter half.

Also remove any `, new()` constraint on `TCreateResultModel` / `TUpdateResultModel` in an app-specific controller base that wraps `UmbrellaGenericRepositoryApiController` — `required` members cannot satisfy `new()` (CS9040).

---

## Verification

1. Each model implements the correct interface(s) from the model types table.
2. `ICreateResultModel<int>` implementations have `required int Id { get; init; }` — not `set`.
3. Read and result models implement `IReadOnlyConcurrencyStamp` (directly or via `IUpdateResultModel`) and declare `required string ConcurrencyStamp { get; init; }` — not `set`. Only entities and `IUpdateModel<TKey>` request models implement the mutable `IConcurrencyStamp` with `{ get; set; }`.
4. A list endpoint uses `PaginatedResultModel<Slim<Name>Model>` directly or a justified derived `record`; no unnecessary wrapper is introduced.
5. Validation attributes are placed on the base model (not duplicated on each request model).
6. `Slim<Name>Model` does not inherit from any base — it is independent.
7. If base classes were introduced: no base class is empty (an empty base adds no value and should be removed).
8. UI/request models with mutable setters are in an `[UmbrellaInputModel]` hierarchy; read/result models are not marked as input models without a concrete binding reason.
9. Every type that declares mutable trimmable strings directly implements `IUmbrellaTrimmable`; only source-generated implementations force that type to be `partial`.
10. Collection properties expose read-only contracts unless an individual property has a justified `[UmbrellaAllowMutableProperty("reason")]`.
11. Read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build with the installed analyzers enabled.
