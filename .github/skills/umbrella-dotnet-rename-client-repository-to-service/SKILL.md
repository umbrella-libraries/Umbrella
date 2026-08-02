---
name: umbrella-dotnet-rename-client-repository-to-service
description: 'Rename a client data type from the old ...Repository convention to ...Service, moving files from Repositories/ to Services/, updating namespaces, DI registration, and all Blazor component references.'
---

# Rename Client Repository to Service

## Purpose

Rename a specific client data interface and implementation from the legacy `...Repository` naming convention to `...Service`, moving files from `Web\<AppName>.Web.Client.Data\Repositories\` to `Web\<AppName>.Web.Client.Data\Services\`. Both conventions use the same `GenericHttpDataService` base class — this is a naming and folder reorganisation only, with no behavioural change.

This skill is typically run as a prerequisite to `umbrella-dotnet-migrate-repo-controller-to-data-service`, which needs a `...Service` interface to create the backing controller service against.

## Discovery (read these before writing anything)

1. Read the existing interface at `Web\<AppName>.Web.Client.Data\Repositories\Abstractions\I<Name>Repository.cs` and the implementation at `Web\<AppName>.Web.Client.Data\Repositories\<Name>Repository.cs`.
2. Read `Web\<AppName>.Web.Client.Data\IServiceCollectionExtensions.cs` to find the current `AddScoped<I<Name>Repository, <Name>Repository>()` line and the `// Services` section where the new registration will go.
3. Search the entire solution for `I<Name>Repository` and `<Name>Repository`, including ordinary `.cs`, `.razor`, project/global-using files, and tests. Do not limit discovery to `.razor.cs`; every source and registration reference must be updated.

---

## Step 1 -- Create the renamed interface

**New file:** `Web\<AppName>.Web.Client.Data\Services\Abstractions\I<Name>Service.cs`

Copy the content of `I<Name>Repository.cs`, then:
- Change namespace from `...Repositories.Abstractions` to `...Services.Abstractions`
- Rename the interface from `I<Name>Repository` to `I<Name>Service`
- Keep the `IGenericDataService<...>` base and all generic type parameters exactly as they are

```csharp
using <AppName>.Web.Shared.Models.Api.<Feature>;
using Umbrella.Utilities.Data.Pagination;

namespace <AppName>.Web.Client.Data.Services.Abstractions;

public interface I<Name>Service : IGenericDataService<
    <Name>Model,
    int,
    Slim<Name>Model,
    PaginatedResultModel<Slim<Name>Model>,
    Create<Name>Model,
    Create<Name>ResultModel,
    Update<Name>Model,
    Update<Name>ResultModel>;
```

Preserve the exact generic type arguments from the original `I<Name>Repository` — including any `NoOpKeyedItem<int>` or `NoOpPaginatedResultModel<int>` placeholders if the original used them. Any custom methods declared on the interface (beyond `IGenericDataService`) must be preserved verbatim.

---

## Step 2 -- Create the renamed implementation

**New file:** `Web\<AppName>.Web.Client.Data\Services\<Name>Service.cs`

Copy the content of `<Name>Repository.cs`, then:
- Change namespace from `...Repositories` to `...Services`
- Rename the class from `<Name>Repository` to `<Name>Service`
- Update the `ILogger<>` type parameter from `ILogger<<Name>Repository>` to `ILogger<<Name>Service>`
- Update the constructor parameter name from `logger` (no change needed there, just the ILogger type)
- Change the implemented interface from `I<Name>Repository` to `I<Name>Service`
- Update the `using` for the old abstractions namespace to `...Services.Abstractions`
- Keep `ApiUrl`, all base constructor params, and any custom methods exactly as they are

```csharp
using <AppName>.Web.Client.Data.Services.Abstractions;
using <AppName>.Web.Shared.Models.Api.<Feature>;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.DataAnnotations.Abstractions;
using Umbrella.Utilities.Http.Abstractions;

namespace <AppName>.Web.Client.Data.Services;

internal sealed class <Name>Service : GenericHttpDataService<...same params as before...>, I<Name>Service
{
    public <Name>Service(
        ILogger<<Name>Service> logger,
        IGenericHttpService httpService,
        IGenericHttpServiceUtility httpServiceUtility,
        IUmbrellaValidator validator)
        : base(logger, httpService, httpServiceUtility, validator)
    {
    }

    protected override string ApiUrl => "api/<ApiSegment>";
    // preserve any custom methods verbatim
}
```

---

## Step 3 -- Update client DI

**File:** `Web\<AppName>.Web.Client.Data\IServiceCollectionExtensions.cs`

Remove the old registration from the `// Repositories` section:
```csharp
_ = services.AddScoped<I<Name>Repository, <Name>Repository>();
```

Add the new registration in the `// Services` section, in alphabetical order:
```csharp
_ = services.AddScoped<I<Name>Service, <Name>Service>();
```

---

## Step 4 -- Update all consumer references

Update every solution-wide reference found during discovery. Blazor code-behind files commonly require the following changes, but ordinary C#, Razor markup, global usings, server replacement registrations, and tests are equally in scope:

1. Replace the `using` for the old namespace:
   ```csharp
   // Before:
   using <AppName>.Web.Client.Data.Repositories.Abstractions;
   // After:
   using <AppName>.Web.Client.Data.Services.Abstractions;
   ```
   (If the namespace is already covered by global usings, no change may be needed — check the project's global usings.)

2. Update the `[Inject]` property type:
   ```csharp
   // Before:
   [Inject]
   private I<Name>Repository Repository { get; set; } = null!;
   // After:
   [Inject]
   private I<Name>Service Repository { get; set; } = null!;
   ```
   The property name (`Repository`) stays the same — do not rename it.

3. Update any index page base class type parameter:
   ```csharp
   // Before:
   public abstract class IndexBase : <AppName>RemoteDataAccessGridComponentBase<..., I<Name>Repository>;
   // After:
   public abstract class IndexBase : <AppName>RemoteDataAccessGridComponentBase<..., I<Name>Service>;
   ```

---

## Step 5 -- Delete the old files

Once all references have been updated and you have confirmed no remaining usages of `I<Name>Repository` or `<Name>Repository` in the solution:

- Delete `Web\<AppName>.Web.Client.Data\Repositories\Abstractions\I<Name>Repository.cs`
- Delete `Web\<AppName>.Web.Client.Data\Repositories\<Name>Repository.cs`

---

## Step 6 -- Update server DI (if applicable)

If the server already had a `ReplaceScoped<I<Name>Repository, ...>()` registration (e.g. from a prior partial migration), update it to `ReplaceScoped<I<Name>Service, ...>()`. If no server registration exists, skip this step — it will be handled by `umbrella-dotnet-migrate-repo-controller-to-data-service`.

---

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Verification

1. No remaining references to `I<Name>Repository` or `<Name>Repository` anywhere in the solution.
2. The new interface and implementation files are in the `Services\` and `Services\Abstractions\` folders with `...Service` names and the correct namespaces.
3. All generic type arguments (including any `NoOp*` placeholders and custom methods) are preserved exactly from the originals.
4. `ApiUrl` value is unchanged.
5. Client DI registers `I<Name>Service` in the `// Services` section; old `// Repositories` entry is removed.
6. All Blazor component `[Inject]` properties reference `I<Name>Service`; property names are unchanged.
