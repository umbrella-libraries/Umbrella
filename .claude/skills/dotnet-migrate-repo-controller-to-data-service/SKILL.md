---
name: dotnet-migrate-repo-controller-to-data-service
description: 'Migrate an existing GenericRepositoryApiController (Pattern 1, direct repository) to GenericRepositoryDataServiceApiController (Pattern 2, backing controller service). Creates the controller service, updates the controller, and rewires DI. Run dotnet-rename-client-repository-to-service first if the client interface still uses the ...Repository naming convention.'
---

# Migrate Repo Controller to Data Service Controller

## Purpose

Upgrade an existing Pattern 1 API controller (inheriting `GenericRepositoryApiController`, direct repository access) to Pattern 2 (inheriting `GenericRepositoryDataServiceApiController`, backed by a controller service). This enables Blazor SSR pre-rendering support and moves all CRUD hooks into a dedicated controller service class.

**Prerequisites:**
- The API controller to migrate must already exist.
- If the client data interface still uses the `...Repository` naming convention, run `dotnet-rename-client-repository-to-service` first to rename it to `...Service` and move it to `Services\Abstractions\`.
- If no client interface exists yet (API-only project), one will be created as part of this skill.

## Discovery (read these before writing anything)

1. Read the existing controller at `Web\<AppName>.Web.Server\Controllers\Api\Manage<Name>Controller.cs` in full — capture the current base class, all generic type params, all constructor dependencies, and any lifecycle hook overrides (`AfterCreateEntityAsync`, `AfterUpdateEntityAsync`, `AfterDeleteEntityAsync`).
2. Read 1–2 existing controller services in `Web\<AppName>.Web.Server\Services\` (fall back to the legacy `Web\<AppName>.Web.Server\Services\Api\` location in older projects) to confirm the `UmbrellaRepositoryDataService` generic parameter order and project-specific patterns.
3. **Determine if a client service interface already exists:** Check `Web\<AppName>.Web.Client.Data\Services\Abstractions\I<Name>Service.cs`. If it exists (after running the rename skill), the server registration must use `ReplaceScoped`. If it does not exist, create it and use `AddScoped`.
4. Read `Web\<AppName>.Web.Server\IServiceCollectionExtensions.cs` — `// Controller Services` section — to confirm the current DI state for this feature.

---

## Step 1 -- Ensure the service interface exists

**If already created by `dotnet-rename-client-repository-to-service`:** skip this step.

**If creating fresh** (no client data project, or the interface was never created):

**File:** `Web\<AppName>.Web.Client.Data\Services\Abstractions\I<Name>Service.cs` (Blazor project)  
or `Web\<AppName>.Web.Server\Services\Abstractions\I<Name>Service.cs` (API-only project)

```csharp
using <AppName>.Web.Shared.Models.Api.<Feature>;
using Umbrella.Utilities.Data.Pagination;

namespace <AppName>.Web.[Client.Data|Server].Services.Abstractions;

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

Use the same model types that the existing controller uses in its generic parameter list (positions T1–T7). If the controller uses `object` or `NoOp*` for some positions, use the same placeholders here.

---

## Step 2 -- Create the controller service

**File:** `Web\<AppName>.Web.Server\Services\<Name>ControllerService.cs`

Concrete controller services live directly in the `Services` folder, alongside the `Services\Abstractions\` interfaces folder — even when the project has existing controller services in the legacy `Services\Api\` location.

Extract any lifecycle hook logic from the existing controller into this new class. If the controller had no hooks, the service body will only have the auth-disabled overrides.

```csharp
using <AppName>.Core.Data.Repositories.Abstractions;
using <AppName>.Core.Domain.Entities;
using <AppName>.Web.[Client.Data|Server].Services.Abstractions;
using <AppName>.Web.Shared.Models.Api.<Feature>;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace <AppName>.Web.Server.Services;

public class <Name>ControllerService : UmbrellaRepositoryDataService<
    <Name>Model,
    Slim<Name>Model,
    PaginatedResultModel<Slim<Name>Model>,
    Create<Name>Model,
    Create<Name>ResultModel,
    Update<Name>Model,
    Update<Name>ResultModel,
    I<Name>Repository,
    <Name>,
    RepoOptions,
    int>, I<Name>Service
{
    public <Name>ControllerService(
        ILogger<<Name>ControllerService> logger,
        IHostEnvironment hostingEnvironment,
        UmbrellaRepositoryDataServiceOptions options,
        IUmbrellaMapper mapper,
        IUmbrellaAuthorizationService authorizationService,
        ISynchronizationManager synchronizationManager,
        Lazy<IDataAccessUnitOfWork> dataAccessUnitOfWork,
        Lazy<I<Name>Repository> repository,
        IDataExpressionFactory dataExpressionFactory)
        : base(logger, hostingEnvironment, options, mapper, authorizationService,
               synchronizationManager, dataAccessUnitOfWork, repository, dataExpressionFactory)
    {
    }

    protected override bool AuthorizationSlimReadChecksEnabled => false;
    protected override bool AuthorizationCreateChecksEnabled => false;
    protected override bool AuthorizationReadChecksEnabled => false;
    protected override bool AuthorizationUpdateChecksEnabled => false;
    protected override bool AuthorizationDeleteChecksEnabled => false;
}
```

**Migrating lifecycle hooks from the old controller:**

If the original controller had overrides such as:

```csharp
// Old controller (Pattern 1):
protected override async Task AfterCreateEntityAsync(<Name> entity, Create<Name>Model model, Create<Name>ResultModel result, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    Guard.IsNotNull(entity);
    Guard.IsNotNull(model);
    Guard.IsNotNull(result);

    result.ImageUrl = await _fileHandler.CreateByGroupIdAndTempFileNameAsync(...);
}
```

Move them verbatim into the controller service. Any extra dependencies (e.g. `I<Name>FileHandler`) that those hooks needed move to the controller service constructor (after the 9 base params):

```csharp
private readonly I<Name>FileHandler _fileHandler;

public <Name>ControllerService(
    // ... 9 base params ...
    I<Name>FileHandler fileHandler)
    : base(...)
{
    _fileHandler = fileHandler;
}
```

**Rules:**
- `public class` (not `internal`).
- `IHostEnvironment` in the constructor (not `IWebHostEnvironment`).
- `UmbrellaRepositoryDataService` generic params (11 total): `TModel` first, then `TSlimModel` — opposite of the controller's order.
- Do not use `object` or `NoOp*` — controller services always use full concrete model types. If the original controller used `object`/NoOp for some positions, use the real model types here (the service supports all operations regardless of which endpoints the controller exposes).

---

## Step 3 -- Rewrite the controller

Replace the existing controller content entirely. The new controller is thin — no overrides, no extra dependencies:

```csharp
using Microsoft.AspNetCore.Authorization;
using <AppName>.Web.[Client.Data|Server].Services.Abstractions;
using <AppName>.Web.Server.Infrastructure.Mvc;
using <AppName>.Web.Shared.Models.Api.<Feature>;
using <AppName>.Web.Shared.Security.Policies;
using Umbrella.Utilities.Data.Pagination;

namespace <AppName>.Web.Server.Controllers.Api;

[Authorize(<AppName>PolicyNames.<Policy>)]
public class <Name>Controller : <AppName>GenericRepositoryDataServiceApiController<
    Slim<Name>Model,
    PaginatedResultModel<Slim<Name>Model>,
    <Name>Model,
    Create<Name>Model,
    Create<Name>ResultModel,
    Update<Name>Model,
    Update<Name>ResultModel,
    int,
    I<Name>Service>
{
    public <Name>Controller(
        ILogger<<Name>Controller> logger,
        IWebHostEnvironment hostingEnvironment,
        Lazy<I<Name>Service> repositoryDataService)
        : base(logger, hostingEnvironment, repositoryDataService)
    {
    }
}
```

**Important:** The model types in the controller (positions T1–T7) must match what the client interface declares. If the original controller used `object` or `NoOp*` for some positions (to disable endpoints), those same values go in the controller's generic params. The controller service uses full concrete types regardless.

---

## Step 4 -- Update DI

**File:** `Web\<AppName>.Web.Server\IServiceCollectionExtensions.cs` — `// Controller Services` section, alphabetical order.

**If the client service interface already exists** (created by `dotnet-rename-client-repository-to-service` or previously scaffolded by `dotnet-scaffold-client-data`):
```csharp
_ = services.ReplaceScoped<I<Name>Service, <Name>ControllerService>();
```

**If no client interface existed before this skill created it:**
```csharp
_ = services.AddScoped<I<Name>Service, <Name>ControllerService>();
```

If the old server DI had any explicit registration for this controller's old repository interface, remove it.

---

## Verification

1. The old `Manage<Name>Controller` no longer inherits `GenericRepositoryApiController` — it inherits `<AppName>GenericRepositoryDataServiceApiController` with 9 type params.
2. No lifecycle hooks remain on the controller — they live in `<Name>ControllerService`.
3. `<Name>ControllerService` is `public class`, lives in `Web\<AppName>.Web.Server\Services\`, inherits `UmbrellaRepositoryDataService` with 11 type params, uses `IHostEnvironment` (not `IWebHostEnvironment`).
4. Any extra dependencies (file handlers, services) from the old controller are now in the controller service constructor, after the 9 base params.
5. Server DI uses `ReplaceScoped` if a client service interface exists, `AddScoped` otherwise.
6. The old controller's `IUmbrellaMapper`, `Lazy<I<Name>Repository>`, and `IUmbrellaRepositoryCoreDataService` constructor dependencies are gone from the controller.
