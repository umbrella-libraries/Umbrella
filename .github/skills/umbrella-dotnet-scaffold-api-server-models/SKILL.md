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
| `<Name>Model` | `IKeyedItem<int>`, `IConcurrencyStamp` | Full read model — returned by GET single |
| `Slim<Name>Model` | `IKeyedItem<int>` | Slim read model — used as the item in paginated list responses |
| `Create<Name>Model` | (none) | Create request body |
| `Update<Name>Model` | `IUpdateModel<int>` | Update request body — includes `Id` and `ConcurrencyStamp` |
| `Create<Name>ResultModel` | `ICreateResultModel<int>` | Create operation result |
| `Update<Name>ResultModel` | `IUpdateResultModel` | Update operation result — returns the new `ConcurrencyStamp` |
| `<Name>PaginatedResultModel` | extends `PaginatedResultModel<Slim<Name>Model>` | Paginated list response |

**Choosing which model types to scaffold**

Match the models you create to the endpoints you intend to enable. Models for disabled endpoints (those using `object` or `NoOp*` in the controller) do not need to exist.

| Endpoints enabled | Model types to scaffold |
|---|---|
| Full CRUD | All 7 types |
| Read list only | `Slim<Name>Model`, `<Name>PaginatedResultModel` |
| Read list + detail | `Slim<Name>Model`, `<Name>PaginatedResultModel`, `<Name>Model` |
| Create only (e.g. analytics, session recording) | `Create<Name>Model`, `Create<Name>ResultModel` |
| Read list + create (no update, no detail) | `Slim<Name>Model`, `<Name>PaginatedResultModel`, `Create<Name>Model`, `Create<Name>ResultModel` |
| Read list + detail + create (no update) | `Slim<Name>Model`, `<Name>PaginatedResultModel`, `<Name>Model`, `Create<Name>Model`, `Create<Name>ResultModel` |

`<Name>PaginatedResultModel` is always paired with `Slim<Name>Model`. If there is no list endpoint, neither is needed.

---

**Interface property requirements — accessor rules:**

- `IKeyedItem<int>` → `int Id { get; }` — `init` is fine here
- `IConcurrencyStamp` → `string ConcurrencyStamp { get; set; }` — mutable `set` required; `init` does NOT satisfy this interface
- `ICreateResultModel<int>` → `int Id { get; set; }` — mutable `set` required
- `IUpdateModel<int>` inherits `IKeyedItem<int>` + `IConcurrencyStamp` → needs `int Id { get; }` and `string ConcurrencyStamp { get; set; }`
- `IUpdateResultModel` inherits `IConcurrencyStamp` → needs `string ConcurrencyStamp { get; set; }`

---

## Record declaration style (Blazor vs. standalone API)

The declaration style depends on the project type. Determine this during discovery.

**Blazor app project:**
All model types use `public partial record`. The `partial` keyword is required so the `IUmbrellaTrimmable` source generator can emit the trim implementation in a separate file. Input models (`Create*Model`, `Update*Model`) that contain or inherit string properties also implement `IUmbrellaTrimmable`. Add `using Umbrella.Utilities.Text;` to those files.

| Model type | Declaration |
|---|---|
| `<Name>ModelBase` (abstract base) | `public abstract partial record` |
| `<Name>Model` (GET detail) | `public partial record` |
| `Slim<Name>Model` (GET list item) | `public partial record` |
| `Create<Name>Model` | `public partial record : IUmbrellaTrimmable` (when has/inherits strings) |
| `Update<Name>Model` | `public partial record : IUmbrellaTrimmable` (when has/inherits strings) |
| `Create<Name>ResultModel` | `public partial record` |
| `Update<Name>ResultModel` | `public partial record` |

**Standalone API project (no Blazor):**
All models use plain `record` — no `partial`, no `IUmbrellaTrimmable`. String trimming is handled at the transport/middleware level.

The code examples in Step 1 use the Blazor convention. For standalone API projects, remove `partial` and omit `IUmbrellaTrimmable`.

---

## Base class hierarchies (use when they add value)

When create and update models share the same editable properties, or when the read model shares display properties with the request models, a base class hierarchy reduces duplication and centralises validation attributes.

**When to introduce base classes:**
- The feature has both create and update models that share most properties
- Validation attributes (e.g., `[Required]`, `[MaxLength]`, `[Display]`) would otherwise be duplicated across multiple models
- The full read model shares display properties with the request models

**Typical hierarchy (used when warranted):**

```
<Name>ModelBase (abstract record)
├── <Name>Model : <Name>ModelBase, IKeyedItem<int>, IConcurrencyStamp
└── CreateUpdate<Name>ModelBase : <Name>ModelBase (abstract record)
    ├── Create<Name>Model : CreateUpdate<Name>ModelBase
    └── Update<Name>Model : CreateUpdate<Name>ModelBase, IUpdateModel<int>

CreateUpdate<Name>ResultModelBase (abstract record)
├── Create<Name>ResultModel : CreateUpdate<Name>ResultModelBase, ICreateResultModel<int>
└── Update<Name>ResultModel : CreateUpdate<Name>ResultModelBase, IUpdateResultModel
```

Notes:
- `<Name>ModelBase` holds the shared display/editable properties and their validation attributes
- `CreateUpdate<Name>ModelBase` inherits `<Name>ModelBase` and is the base for both request models — only needed if there are truly shared create/update properties beyond what's already on `<Name>ModelBase`; omit if it would be empty
- `Slim<Name>Model` usually does NOT inherit from `<Name>ModelBase` — it exists independently with only the fields needed in list views
- `CreateUpdate<Name>ResultModelBase` holds properties returned by both create and update results (e.g., `ConcurrencyStamp`, any computed properties like `ImageUrl`)
- A two-tier result base is only worth adding if there are genuinely shared result properties; if both results only return `ConcurrencyStamp`, a base is unlikely to add value

---

## Step 1 -- Create the model records

**Directory:** `Web.Shared\Models\Api\<Feature>\` (confirm with discovery)

**Abstract base (shared display + editable properties with validation):**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public abstract partial record <Name>ModelBase
{
    [Required(ErrorMessage = "<Name>Constants.NameRequiredErrorMessage")]
    [MaxLength(200)]
    public string Name { get; set; } = null!;
    // other shared editable properties
}
```

Place validation attributes here so they are not repeated on each request model.

**Full read model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record <Name>Model : <Name>ModelBase, IKeyedItem<int>, IConcurrencyStamp
{
    public required int Id { get; init; }
    public required string ConcurrencyStamp { get; set; }
    // computed / server-side properties not in the base (e.g., ImageUrl)
}
```

**Slim read model (for paginated list items):**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record Slim<Name>Model : IKeyedItem<int>
{
    public required int Id { get; init; }
    // only the fields shown in list views
}
```

**Create model:**

```csharp
using Umbrella.Utilities.Text;

namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record Create<Name>Model : <Name>ModelBase, IUmbrellaTrimmable
{
    // add properties specific to creation only; leave empty if everything is on the base
}
```

**Update model:**

```csharp
using Umbrella.Utilities.Text;

namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record Update<Name>Model : <Name>ModelBase, IUpdateModel<int>, IUmbrellaTrimmable
{
    public required int Id { get; init; }
    [Required]
    public required string ConcurrencyStamp { get; set; }
    // add properties specific to update only (e.g., ReplaceExistingImage)
}
```

Note: `ConcurrencyStamp` must use `set` (not `init`) to satisfy `IConcurrencyStamp`. Mark it `required` and add `[Required]` so client-side validation enforces it.

**Create result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record Create<Name>ResultModel : ICreateResultModel<int>
{
    public int Id { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;
    // any other values the server computes on creation (e.g., ImageUrl)
}
```

Note: `Id` must use `set` (not `init`) to satisfy `ICreateResultModel<int>`.

**Update result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public partial record Update<Name>ResultModel : IUpdateResultModel
{
    public string ConcurrencyStamp { get; set; } = null!;
    // any other values the server recomputes on update
}
```

**Paginated result model:**

```csharp
namespace <AppName>.Web.Shared.Models.Api.<Feature>;

public class <Name>PaginatedResultModel : PaginatedResultModel<Slim<Name>Model>;
```

Note: `PaginatedResultModel<T>` is a class, so the paginated result model must also be a `class` (not a `record`).

---

## `required` keyword guidance

Use `required` on properties that the caller must always supply — typically `Id`, `ConcurrencyStamp`, and any non-nullable reference types that have no meaningful default. Skip `required` on properties with sensible defaults (`= ""`, `= null!`) or on mutable properties that may be filled in after construction.

---

## Verification

1. Each model implements the correct interface(s) from the model types table.
2. `ICreateResultModel<int>` implementations have `int Id { get; set; }` — not `init`.
3. `IConcurrencyStamp` implementations have `string ConcurrencyStamp { get; set; }` — not `init`.
4. The paginated result model is a `class` extending `PaginatedResultModel<Slim<Name>Model>`.
5. Validation attributes are placed on the base model (not duplicated on each request model).
6. `Slim<Name>Model` does not inherit from any base — it is independent.
7. If base classes were introduced: no base class is empty (an empty base adds no value and should be removed).
8. **Blazor project:** All record types are `public partial record`; input models with strings implement `IUmbrellaTrimmable` with `using Umbrella.Utilities.Text;`.
9. **Standalone API:** All record types are plain `record` — no `partial`, no `IUmbrellaTrimmable`.
