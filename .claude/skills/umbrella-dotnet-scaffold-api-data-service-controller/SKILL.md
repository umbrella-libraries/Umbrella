---
name: umbrella-dotnet-scaffold-api-data-service-controller
description: 'Scaffold a thin API controller backed by a controller service (GenericRepositoryDataServiceApiController pattern). Supports Blazor SSR pre-rendering when the service interface lives in the client data project. Produces 4 artifacts: service interface, server controller service, controller, and DI registration.'
---

# Scaffold API Data Service Controller

## Purpose

Add a thin API controller backed by a *controller service* that inherits `UmbrellaRepositoryDataService`. All CRUD hooks and repository access live in the controller service — the controller itself is a minimal wrapper.

This pattern decouples the service interface from the transport layer, enabling Blazor SSR pre-rendering: the same interface that the controller service implements server-side can also be implemented client-side as an HTTP data service. For pure API projects without Blazor, the interface simply lives in a server-side abstractions folder.

Use `umbrella-dotnet-scaffold-api-repo-controller` instead when no service abstraction is needed, and `umbrella-dotnet-scaffold-custom-api-controller` when the endpoint shape does not fit the generic CRUD patterns at all.

## Discovery (read these before writing anything)

1. Read 1–2 existing controller services in `Web\<AppName>.Web.Server\Services\` (fall back to the legacy `Web\<AppName>.Web.Server\Services\Api\` location in older projects) to confirm the `UmbrellaRepositoryDataService` generic parameter order (11 params) and any project-specific patterns.
2. Read 1–2 existing controllers in `Web\<AppName>.Web.Server\Controllers\Api\` to confirm the project-specific base class wrapper (e.g. `IndyRecordsGenericRepositoryDataServiceApiController`) and its 9 generic parameter order.
3. **Determine interface location**: if `Web\<AppName>.Web.Client.Data\Services\Abstractions\` exists, the interface goes there (Blazor project — enables SSR pre-rendering). Otherwise it goes in `Web\<AppName>.Web.Server\Services\Abstractions\` or equivalent.
4. Read `Web\<AppName>.Web.Server\IServiceCollectionExtensions.cs` — find the `// Controller Services` section to see whether existing registrations use `AddScoped` or `ReplaceScoped`.
5. Read `Web\<AppName>.Web.Shared\Security\Policies\<AppName>PolicyNames.cs` for auth policy constant names.

---

## Step 1 -- Create the service interface

**Blazor project:** `Web\<AppName>.Web.Client.Data\Services\Abstractions\IManage<Name>Service.cs`  
**API-only project:** `Web\<AppName>.Web.Server\Services\Abstractions\IManage<Name>Service.cs`

```csharp
using <AppName>.Web.Shared.Models.Api.Manage<Name>;
using Umbrella.Utilities.Data.Pagination;

namespace <AppName>.Web.[Client.Data|Server].Services.Abstractions;

public interface IManage<Name>Service : IGenericDataService<
    Manage<Name>Model,
    int,
    SlimManage<Name>Model,
    PaginatedResultModel<SlimManage<Name>Model>,
    CreateManage<Name>Model,
    CreateManage<Name>ResultModel,
    UpdateManage<Name>Model,
    UpdateManage<Name>ResultModel>;
```

**`IGenericDataService` generic params in order (8 total):** `TModel`, `TIdentifier`, `TSlimModel`, `TPaginatedResultModel`, `TCreateModel`, `TCreateResultModel`, `TUpdateModel`, `TUpdateResultModel`.

---

## Step 2 -- Create the server controller service

**File:** `Web\<AppName>.Web.Server\Services\Manage<Name>ControllerService.cs`

Concrete controller services live directly in the `Services` folder, alongside the `Services\Abstractions\` interfaces folder — mirroring the client data project layout. (Older projects may have them in `Services\Api\`; new controller services still go in `Services\`.)

```csharp
using <AppName>.Core.Data.Repositories.Abstractions;
using <AppName>.Core.Domain.Entities;
using <AppName>.Web.[Client.Data|Server].Services.Abstractions;
using <AppName>.Web.Shared.Models.Api.Manage<Name>;
using Umbrella.DataAccess.Abstractions;
using Umbrella.DataAccess.Abstractions.Options;
using Umbrella.Utilities.Data.Abstractions;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Security.Abstractions;
using Umbrella.Utilities.Threading.Abstractions;

namespace <AppName>.Web.Server.Services;

public class Manage<Name>ControllerService : UmbrellaRepositoryDataService<
    Manage<Name>Model,
    SlimManage<Name>Model,
    PaginatedResultModel<SlimManage<Name>Model>,
    CreateManage<Name>Model,
    CreateManage<Name>ResultModel,
    UpdateManage<Name>Model,
    UpdateManage<Name>ResultModel,
    I<Name>Repository,
    <Name>,
    RepoOptions,
    int>, IManage<Name>Service
{
    public Manage<Name>ControllerService(
        ILogger<Manage<Name>ControllerService> logger,
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

**Rules:**
- `public class` (not `internal`) — both the server and client resolve it through the shared interface.
- Note `IHostEnvironment` in the constructor (not `IWebHostEnvironment` — that is used by the controller, not the service).
- `UmbrellaRepositoryDataService` generic params in order (11 total): `TModel`, `TSlimModel`, `TPaginatedResultModel`, `TCreateModel`, `TCreateResultModel`, `TUpdateModel`, `TUpdateResultModel`, `TRepository`, `TEntity`, `TRepositoryOptions`, `TEntityKey`. Note: `TModel` comes before `TSlimModel` here — the opposite order from the controller.
- Extra dependencies (e.g. `I<Name>FileHandler`) go after the 9 base constructor params and are stored as `private readonly` fields.
- `AfterCreateEntityAsync`, `AfterUpdateEntityAsync`, `AfterDeleteEntityAsync` are added only when needed. Follow the same guard/cancellation pattern as repositories.
- Controller services always use full concrete model types — no NoOp/`object` patterns.
- Do not override `PostAsync`, `PutAsync`, `DeleteAsync`, `GetAsync`, or `SearchSlimAsync` in the controller service. All CRUD customisation belongs in lifecycle hooks (`BeforeCreateEntityAsync`, `AfterCreateEntityAsync`, etc.) — UA019 enforces this.
- **Layer note:** the controller service lives in the Web layer and may reference Web.Models types — this is intentional, it is the translation boundary. Core.Logic services (see `umbrella-dotnet-scaffold-service`) must never reference Web.Models. Do not place complex domain logic in the controller service; if the logic belongs in Core.Logic, create a dedicated service there and inject it here.

### Lifecycle hook example (file handling)

```csharp
protected override async Task AfterCreateEntityAsync(<Name> entity, CreateManage<Name>Model model, CreateManage<Name>ResultModel result, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    Guard.IsNotNull(entity);
    Guard.IsNotNull(model);
    Guard.IsNotNull(result);

    _ = await _fileHandler.CreateByGroupIdAndTempFileNameAsync(entity.Id, model.ImageProviderFileName, null, cancellationToken);
    UmbrellaVersionedUrl image = await _fileHandler
        .GetVersionedWebFilePathAsync(entity.Id, model.ImageProviderFileName, cancellationToken)
        ?? throw new InvalidOperationException("The saved image could not be resolved.");

    result.ImageUrl = image.Url;
    result.ImageVersionToken = image.VersionToken;
}
```

When Dynamic Image fingerprinting is enabled, the result model declares the matching nullable `ImageVersionToken`; always populate URL/token pairs together.

---

## Step 3 -- Create the controller

**File:** `Web\<AppName>.Web.Server\Controllers\Api\Manage<Name>Controller.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using <AppName>.Web.[Client.Data|Server].Services.Abstractions;
using <AppName>.Web.Server.Infrastructure.Mvc;
using <AppName>.Web.Shared.Models.Api.Manage<Name>;
using <AppName>.Web.Shared.Security.Policies;
using Umbrella.Utilities.Data.Pagination;

namespace <AppName>.Web.Server.Controllers.Api;

[Authorize(<AppName>PolicyNames.<Policy>)]
public class Manage<Name>Controller : <AppName>GenericRepositoryDataServiceApiController<
    SlimManage<Name>Model,
    PaginatedResultModel<SlimManage<Name>Model>,
    Manage<Name>Model,
    CreateManage<Name>Model,
    CreateManage<Name>ResultModel,
    UpdateManage<Name>Model,
    UpdateManage<Name>ResultModel,
    int,
    IManage<Name>Service>
{
    public Manage<Name>Controller(
        ILogger<Manage<Name>Controller> logger,
        IWebHostEnvironment hostingEnvironment,
        Lazy<IManage<Name>Service> repositoryDataService)
        : base(logger, hostingEnvironment, repositoryDataService)
    {
    }
}
```

**Rules:**
- The controller is always thin — no overrides, no extra dependencies. All logic lives in the controller service.
- Generic type params in order (9 total): `TSlimModel`, `TPaginatedResultModel`, `TModel`, `TCreateModel`, `TCreateResultModel`, `TUpdateModel`, `TUpdateResultModel`, `TEntityKey`, `TRepositoryDataService`.
- No DI registration for the controller — auto-discovered by ASP.NET Core.

---

## Step 4 -- Register the controller service in DI

**File:** `Web\<AppName>.Web.Server\IServiceCollectionExtensions.cs` — `// Controller Services` section, alphabetical order.

**If no Blazor client project, or the interface lives in the server project:**
```csharp
_ = services.AddScoped<IManage<Name>Service, Manage<Name>ControllerService>();
```

**If the interface lives in `Web.Client.Data.Services.Abstractions` and a Blazor client has already registered it with `AddScoped`:**
```csharp
_ = services.ReplaceScoped<IManage<Name>Service, Manage<Name>ControllerService>();
```

Check `Web\<AppName>.Web.Client.Data\IServiceCollectionExtensions.cs` to see whether the client already registers `IManage<Name>Service`. If it does, use `ReplaceScoped`; otherwise use `AddScoped`. When the `blazor-scaffold-client-data` skill runs later, it will add the client `AddScoped` and update the server registration to `ReplaceScoped` if needed.

---

## Custom action methods

Custom action methods on Pattern 2 controllers are rare — the controller should remain thin. If the custom logic is non-trivial, add it to the controller service and delegate from the controller. When most of the controller's surface is custom-shaped, use `umbrella-dotnet-scaffold-custom-api-controller` (Variant C, `UmbrellaDataServiceApiController`) instead of adding many custom actions here.

When a genuinely custom endpoint is needed, follow these conventions.

**Attribute:** Use `[UmbrellaProducesResponseType(StatusCodes.StatusXxx)]` (NOT the standard `[ProducesResponseType]`). Umbrella's attribute automatically maps status codes to `UmbrellaProblemDetails` or `UmbrellaValidationProblemDetails` with the correct `application/problem+json` content type.

**Status codes to declare per verb** (401, 403, 500 are already covered at class level):

| HTTP verb | Declare on the custom method |
|---|---|
| `[HttpGet]` | 200, 400, 404 (if not found is possible) |
| `[HttpPost]` | 201, 400, 409 (if conflict is possible), 422 |
| `[HttpPut]` | 200, 400, 404, 409 (conflict/concurrency), 422 |
| `[HttpPatch]` | 200, 400, 404, 409 (conflict/concurrency), 422 |
| `[HttpDelete]` | 204, 400, 404, 409 (conflict/concurrency) |

Note: 405 (Method Not Allowed) is returned by the base class for disabled endpoints — do not declare it on custom methods.

**Error helper methods** inherited from `UmbrellaApiController`:

```csharp
BadRequest(reason, code?)          // 400 — UmbrellaValidationProblemDetails
NotFound(reason, code?)            // 404 — UmbrellaProblemDetails
Conflict(reason, code?)            // 409 — UmbrellaProblemDetails
ConcurrencyConflict(reason)        // 409 — code: HttpProblemCodes.ConcurrencyStampMismatch
InternalServerError(reason, code?) // 500 — UmbrellaProblemDetails
OperationResult(IOperationResult)  // converts OperationResultStatus enum to the correct HTTP response
```

**Example: delegating to the controller service**

Prefer composing the inherited `ExecuteOperationAsync` envelope (from `UmbrellaDataServiceApiController`) with an expression lambda over the service — it owns cancellation, the `IOperationResult` → HTTP mapping, logging with caller info, and the `500` response. Have the service method return an `IOperationResult<T>` (e.g. `NotFound` when the summary is missing):

```csharp
[HttpGet("GetSummary")]
[UmbrellaProducesResponseType(StatusCodes.Status200OK)]
[UmbrellaProducesResponseType(StatusCodes.Status400BadRequest)]
[UmbrellaProducesResponseType(StatusCodes.Status404NotFound)]
public Task<IActionResult> GetSummaryAsync([FromQuery] int id, CancellationToken cancellationToken = default)
    => ExecuteOperationAsync<Manage<Name>SummaryModel>(
        (service, token) => service.GetSummaryAsync(id, token),
        "An error occurred while attempting to load the summary.",
        cancellationToken,
        new { id });
```

Do not use `async` lambdas with `ExecuteOperationAsync` — pass an expression lambda returning the service's `Task` directly, or the call can bind to the wrong overload and lose the typed response body.

---

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Verification

1. Service interface is in the correct location — client data abstractions for Blazor projects, server abstractions for API-only.
2. The controller service file is in `Web\<AppName>.Web.Server\Services\` with namespace `<AppName>.Web.Server.Services`.
3. `IGenericDataService` has 8 type params; the first is `TModel` (full model), second is `TIdentifier` (int), third is `TSlimModel`.
4. `UmbrellaRepositoryDataService` has 11 type params; `TModel` comes before `TSlimModel` (opposite order from the controller's type list).
5. The controller service is `public class` (not `internal`).
6. The controller service constructor uses `IHostEnvironment` (not `IWebHostEnvironment`); the 9 base params are passed to `: base(...)`.
7. The controller is thin — no overrides or extra dependencies.
8. DI registration uses `AddScoped` or `ReplaceScoped` based on whether the client already registers the interface.
9. No standard CRUD method (`PostAsync`, `GetAsync`, `PutAsync`, `DeleteAsync`, `SearchSlimAsync`) is overridden in the controller service without a `base.XxxAsync(...)` call.
10. No `using` directive in the controller service references a `Core.Logic` namespace — domain logic belongs in a dedicated Core.Logic service, not here.

---

## Next steps

After the controller and controller service build and DI is wired, generate integration tests with `umbrella-dotnet-generate-api-data-service-controller-tests` (run `umbrella-dotnet-audit-api-controller-response-contract` first — enablement and authorization flags resolve on the controller service for this pattern).
