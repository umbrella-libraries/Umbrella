---
name: umbrella-dotnet-scaffold-file-authorization-handler
description: 'Scaffold a file authorization handler in the Core.Logic project, following the Umbrella UmbrellaFileAuthorizationHandler pattern. Authorization is decoupled from the file handler — use this skill alongside umbrella-dotnet-scaffold-file-handler when access control is needed.'
---

# Scaffold File Authorization Handler

## Purpose

Add a file authorization handler to the `Core.<AppName>.Core.Logic` project. Authorization handlers are responsible for **access control only**: given a file and an operation type, they decide whether the current user may proceed.

Authorization is decoupled from the file handler (`UmbrellaFileHandler<int>`). A file handler can exist without a matching authorization handler if access is controlled elsewhere. However, the default storage provider behaviour is to **deny** access for any directory that has no registered auth handler, so omit one only when you have a deliberate reason.

The `IUmbrellaFileAuthorizationHandlerRegistry` links auth handlers to file directories by matching each handler's `DirectoryName` against the first path segment of the accessed file's path. This means the `DirectoryName` on the auth handler **must exactly match** the `DirectoryNames` constant used by the corresponding file handler.

## Discovery (read these before writing anything)

1. Read existing auth handler implementations in `Core\<AppName>.Core.Logic\FileSystem\AuthHandlers\` to understand the pattern.
2. Confirm the `DirectoryNames` constant already exists for the target directory (added by the `umbrella-dotnet-scaffold-file-handler` skill).
3. Read `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs` to see the `// Auth Handlers` registration section.

---

## Step 1 -- Create the implementation

**File location:** `Core\<AppName>.Core.Logic\FileSystem\AuthHandlers\<Name>FileAuthorizationHandler.cs`

```csharp
using System.Security.Claims;
using <AppName>.Core.Common.FileSystem.Constants;
using <AppName>.Core.Logic.Exceptions;
using Umbrella.FileSystem.Abstractions;

namespace <AppName>.Core.Logic.FileSystem.AuthHandlers;

internal sealed class <Name>FileAuthorizationHandler : UmbrellaFileAuthorizationHandler
{
    public <Name>FileAuthorizationHandler(ILogger<<Name>FileAuthorizationHandler> logger)
        : base(logger)
    {
    }

    public override string DirectoryName => DirectoryNames.<Name>;

    public override Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guard.IsNotNull(fileInfo);

        try
        {
            // Fail closed until the directory's explicit access policy is implemented.
            bool canAccess = false;

            return Task.FromResult(canAccess);
        }
        catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.Name }))
        {
            throw new <AppName>CoreLogicException("There has been a problem accessing the file.", exc);
        }
    }
}
```

**Rules:**
- Always `internal sealed class` inheriting `UmbrellaFileAuthorizationHandler`
- Constructor takes only `ILogger<T>` — passed to `: base(logger)`; no storage dependencies
- No specific interface — auth handlers do not get their own marker interface; they all implement `IUmbrellaFileAuthorizationHandler` through the base class
- `DirectoryName` must return `DirectoryNames.<Name>` — the same constant used by the matching file handler
- `AuthorizeAsync` must always call `cancellationToken.ThrowIfCancellationRequested()` and `Guard.IsNotNull(fileInfo)` first, then wrap the access check in the try/catch pattern
- New handlers fail closed by default. Replace `false` only after deriving the directory's explicit access policy.
- Use the shown `catch (Exception exc) when (Logger.WriteError(...))` form. Do not substitute direct `Logger.LogError` or `Logger.LogCritical` calls; UA008 accepts structured calls to those methods, but they are not the preferred repository pattern.

### Authorization per operation type

When authorization needs to vary by operation, switch on `operationType`:

```csharp
bool canAccess = operationType switch
{
    UmbrellaFileOperationType.Read => ClaimsPrincipal.Current?.IsInRole("Admin") is true,
    UmbrellaFileOperationType.Create or UmbrellaFileOperationType.Update or UmbrellaFileOperationType.Delete
        => ClaimsPrincipal.Current?.IsInRole("Admin") is true,
    _ => false
};
```

### Sensitive or user-specific files

Authentication alone is never sufficient for private, user-specific, tenant-specific, school-specific, or provider-specific files. For those directories:

- Check authentication first, then authorize each operation explicitly; keep the default branch denied.
- For reads, require a concrete privileged role or compare trusted file metadata with the caller's user/tenant/provider claim. Missing or malformed metadata and missing claims must deny access.
- For create, update, and delete, enumerate the permitted management roles rather than treating every authenticated caller as a manager.
- Keep every successful path traceable to an explicit role or metadata match. Do not add an unconditional early `return true`.
- Do not consult `AsyncLocal`, `ThreadStatic`, ambient context objects, mutable singleton state, or an “internal operation” flag. Authorization must depend only on the authenticated principal, operation, and trusted file information.
- Do not weaken authorization to accommodate file-handler lifecycle code. Required ownership metadata should be attached to the temporary file before it is moved into the protected directory; see `umbrella-dotnet-scaffold-file-handler`.
- Cover the real HTTP file endpoint with integration tests for unauthenticated callers, authenticated callers without a permitted role, matching and mismatching owners/tenants, missing claims/metadata, privileged administrators, and every applicable operation. For sensitive Dynamic Image mappings, also assert `Cache-Control: no-store` on successful responses.

---

## Step 2 -- Register in DI

**File:** `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs`

Add one line in the `// Auth Handlers` section, in alphabetical order:

```csharp
_ = services.AddSingleton<IUmbrellaFileAuthorizationHandler, <Name>FileAuthorizationHandler>();
```

**Key differences from file handler registration:**
- Always registers against `IUmbrellaFileAuthorizationHandler` — never a specific interface
- The framework collects all `IUmbrellaFileAuthorizationHandler` registrations via `IEnumerable<>` and builds the registry automatically
- Still `AddSingleton` — auth handlers are stateless

---

## Analyzer compatibility

Before finishing, read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` and build the affected projects with their installed analyzers enabled. Treat diagnostics introduced by the generated or changed code as defects in this workflow.

## Verification

1. The implementation is `internal sealed` and inherits `UmbrellaFileAuthorizationHandler`.
2. Constructor takes only `ILogger<T>` and passes it to `: base(logger)` — no extra dependencies.
3. There is no specific interface file for this handler — it registers directly as `IUmbrellaFileAuthorizationHandler`.
4. `DirectoryName` returns `DirectoryNames.<Name>` — the same constant as the matching file handler.
5. `AuthorizeAsync` calls `ThrowIfCancellationRequested`, `Guard.IsNotNull(fileInfo)`, and wraps the access check in try/catch with `Logger.WriteError`.
6. `AddSingleton<IUmbrellaFileAuthorizationHandler, <Name>FileAuthorizationHandler>()` is present in `IServiceCollectionExtensions.cs`.
7. Sensitive directories fail closed and contain no ambient-state or internal-operation bypass.
8. Each successful authorization path is backed by an explicit role or trusted metadata match and is verified through the HTTP file endpoint.
