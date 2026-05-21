---
name: blazor-scaffold-index-page
description: 'Scaffold a Blazor index/listing page (.razor + .razor.cs) for a feature, following the Umbrella UmbrellaGrid pattern with breadcrumb, auth policy, and action column.'
---

# Scaffold Blazor Index Page

## Purpose

Add a Blazor index page that renders a paginated, sortable, filterable grid for a feature. The page uses `UmbrellaGrid` via the project-specific `<AppName>RemoteDataAccessGridComponentBase` base class, which handles all data fetching, sorting, filtering, and delete wiring automatically.

**Prerequisite:** A client data service interface (`I<Name>Service`) must exist — either from `dotnet-scaffold-api-data-service-controller` + `dotnet-scaffold-client-data`, or from `dotnet-rename-client-repository-to-service`. The slim model (`Slim<Name>Model`) and paginated result model must also exist.

## Discovery (read these before writing anything)

1. Read 2–3 existing index pages under `Web\<AppName>.Web.Client\Pages\Admin\` to confirm folder naming conventions, route patterns, breadcrumb usage, grid column patterns, and action column structure.
2. Confirm the project-specific grid component base class name (e.g. `IndyRecordsRemoteDataAccessGridComponentBase`).
3. Read `Web\<AppName>.Web.Shared\Security\Policies\<AppName>PolicyNames.cs` or `SharedPolicyNames.cs` for the correct auth policy constant.
4. Confirm the feature's index route (e.g. `/admin/industries`) and the manage route (e.g. `/admin/industries/manage`) by checking an analogous existing feature.

---

## Step 1 -- Create the folder

**Folder:** `Web\<AppName>.Web.Client\Pages\Admin\<Name>Management\`

---

## Step 2 -- Create Index.razor

**File:** `Web\<AppName>.Web.Client\Pages\Admin\<Name>Management\Index.razor`

```razor
@inherits IndexBase
@page "/admin/<route-plural>"

@{
    string title = "Manage <Names>";
}

<<AppName>PageTitle>@title</<AppName>PageTitle>

<UmbrellaBreadcrumb>
    <UmbrellaBreadcrumbItem Name="@title" />
</UmbrellaBreadcrumb>

<div class="listing-page">
    <div class="listing-page__header">
        <h1>@title</h1>
        <div>
            <a class="btn btn-primary" href="/admin/<route-plural>/manage">Create <i class="fas fa-plus-circle"></i></a>
        </div>
    </div>

    <UmbrellaGrid @ref="GridInstance" TItem="Slim<Name>Model" InitialSortProperty="x => x.CreatedDateUtc" OnDataRequestedAsync="OnGridDataRequestAsync">
        <Columns>
            <UmbrellaColumn Property="x => x.CreatedDateUtc" Sortable="true">@context.CreatedDateUtc.ToString("d")</UmbrellaColumn>
            <UmbrellaColumn Property="x => x.Name" Sortable="true" Filterable="true" />
            <UmbrellaActionsColumn>
                <a class="btn btn-primary btn-sm" href="/admin/<route-plural>/manage/@context.Id" title="Edit">
                    <i class="fas fa-edit" aria-hidden="true"></i>
                </a>
                <button class="btn btn-danger btn-sm" @onclick="_ => DeleteItemClickAsync(context)" title="Delete">
                    <i class="fas fa-trash" aria-hidden="true"></i>
                </button>
            </UmbrellaActionsColumn>
        </Columns>
    </UmbrellaGrid>
</div>
```

**Rules:**
- `@inherits IndexBase` only — no `@page` logic or C# in the `.razor` file beyond `@{...}` for local variables.
- Route uses lowercase, hyphenated, plural form: `/admin/career-quiz-questions`, `/admin/industries`.
- `InitialSortProperty` defaults to `x => x.CreatedDateUtc` — change to a more meaningful property if the entity doesn't have a creation date or if another sort makes more sense for the feature.
- Columns: always include `CreatedDateUtc` first (if available), then any key display fields. Check the `Slim<Name>Model` properties to know what's available.
- `UmbrellaActionsColumn`: include an Edit link always. Include a View link only if a public-facing detail page exists. Include Delete only if deletion is permitted for this feature.
- A "Create" button in the header links to the manage page route with no ID segment.

### Optional: public view link in actions column

If a public detail page exists for this entity:
```razor
<a class="btn btn-secondary btn-sm" href="/<public-route>/@context.Id" title="View">
    <i class="fas fa-eye" aria-hidden="true"></i>
</a>
```

---

## Step 3 -- Create Index.razor.cs

**File:** `Web\<AppName>.Web.Client\Pages\Admin\<Name>Management\Index.razor.cs`

```csharp
using <AppName>.Web.Client.Data.Services.Abstractions;
using <AppName>.Web.Shared.Models.Api.<Feature>;

namespace <AppName>.Web.Client.Pages.Admin.<Name>Management;

[Authorize(<AppName>PolicyNames.<Policy>)]
public abstract class IndexBase : <AppName>RemoteDataAccessGridComponentBase<Slim<Name>Model, int, PaginatedResultModel<Slim<Name>Model>, I<Name>Service>;
```

**Rules:**
- `public abstract class` — the `.razor` file inherits from it via `@inherits`.
- `[Authorize(PolicyName)]` on the class, not in the `.razor` file.
- The four generic type params match the service interface's `TSlimModel`, `TIdentifier`, `TPaginatedResultModel`, and the service interface itself.
- No body needed — the base class provides `GridInstance`, `OnGridDataRequestAsync`, and `DeleteItemClickAsync` automatically.
- No SCSS file unless the feature requires custom page-level styles. Check existing pages — most have none.

---

## Verification

1. The `.razor` file contains only `@inherits IndexBase`, `@page`, optional `@{...}` variable blocks, and HTML/component markup — no C# logic.
2. The code-behind is `public abstract class` with `[Authorize]` and the correct 4 generic params.
3. The route is lowercase, hyphenated, and plural.
4. The grid `TItem` matches the slim model used in the base class generic params.
5. `DeleteItemClickAsync` is called on the delete button — not a custom method.
6. The "Create" button href matches the manage page's create route.
