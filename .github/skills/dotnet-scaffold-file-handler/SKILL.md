---
name: dotnet-scaffold-file-handler
description: 'Scaffold a file handler (interface, implementation, DirectoryNames constant, DI registration) in the Core.Logic project, following the Umbrella UmbrellaFileHandler pattern.'
---

# Scaffold File Handler

## Purpose

Add a new file handler to the `Core.<AppName>.Core.Logic` project. File handlers plug into the Umbrella file storage infrastructure: when a file is accessed via `IUmbrellaFileStorageProvider`, the provider extracts the directory name from the file's path and delegates authorization to the matching registered handler. Handlers are also used directly to save, retrieve, and delete files by group ID.

A file handler can represent any logical grouping of files -- files attached to a database record, files in a SharePoint-style folder, user uploads, generated reports, or anything else. The group ID (`int`) is whatever identifier separates one group of files from another for this particular handler.

## How the provider finds the handler

The provider extracts the first path segment of a file's `SubPath` as the directory name (e.g., `/user-document/42/cv.pdf` -> `user-document`), then looks up the registered `IUmbrellaFileAuthorizationHandler` whose `DirectoryName` matches (case-insensitive). The `AuthorizeAsync` method on that handler is called to permit or deny the operation. If no handler is found and `AllowUnhandledFileAuthorizationChecks` is false (the default), access is denied. This means the `DirectoryName` property on the handler **must exactly match** the constant registered in `DirectoryNames`.

## Discovery (read these before writing anything)

1. Read existing file handler implementations in `Core\<AppName>.Core.Logic\FileSystem\` to understand the pattern.
2. Read the interfaces in `Core\<AppName>.Core.Logic\FileSystem\Abstractions\`.
3. Read `Core\<AppName>.Core.Common\FileSystem\Constants\DirectoryNames.cs` to see existing constants and the `All` collection.
4. Read `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs` to see where handlers are registered.

---

## Step 1 -- Add the DirectoryNames constant

**File:** `Core\<AppName>.Core.Common\FileSystem\Constants\DirectoryNames.cs`

Add a new `public const string` entry using lowercase, hyphenated naming (kebab-case). The name should describe what the directory stores, not how it is implemented:

```csharp
public const string <Name> = "<name-in-kebab-case>";
```

Also add the new constant to the `All` collection:

```csharp
public static readonly IReadOnlyCollection<string> All = [
    // existing entries ...
    <Name>
];
```

The string value becomes the first path segment under the file storage root for all files belonging to this handler (e.g., `"user-document"` -> files stored at `/user-document/<groupId>/<fileName>`). The group ID segment identifies the specific container within that directory -- it could be a database record ID, a user ID, a folder reference, or any other discriminator that makes sense for this use case.

---

## Step 2 -- Create the interface

**File location:** `Core\<AppName>.Core.Logic\FileSystem\Abstractions\I<Name>FileHandler.cs`

```csharp
using Umbrella.FileSystem.Abstractions;

namespace <AppName>.Core.Logic.FileSystem.Abstractions;

public interface I<Name>FileHandler : IUmbrellaFileHandler<int>;
```

**Rules:**
- Empty marker interface -- all behavior is on the base interface and implementation
- Always extends `IUmbrellaFileHandler<int>` where `int` is the group ID type used in this project
- Needs the explicit `using Umbrella.FileSystem.Abstractions;` -- this namespace is not in global usings for this project

---

## Step 3 -- Create the implementation

**File location:** `Core\<AppName>.Core.Logic\FileSystem\<Name>FileHandler.cs`

**Minimal pattern (no post-save processing):**

```csharp
using System.Security.Claims;
using <AppName>.Core.Common.FileSystem.Constants;
using <AppName>.Core.Logic.Exceptions;
using <AppName>.Core.Logic.FileSystem.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;

namespace <AppName>.Core.Logic.FileSystem;

internal sealed class <Name>FileHandler : UmbrellaFileHandler<int>, I<Name>FileHandler
{
    public <Name>FileHandler(
        ILogger<<Name>FileHandler> logger,
        IHybridCache cache,
        ICacheKeyUtility cacheKeyUtility,
        IUmbrellaFileStorageProvider fileProvider,
        IUmbrellaFileStorageProviderOptions options)
        : base(logger, cache, cacheKeyUtility, fileProvider, options)
    {
    }

    public override string DirectoryName => DirectoryNames.<Name>;

    public override Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guard.IsNotNull(fileInfo);

        try
        {
            bool canAccess = ClaimsPrincipal.Current?.Identity?.IsAuthenticated is true;

            return Task.FromResult(canAccess);
        }
        catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.Name }))
        {
            throw new <AppName>CoreLogicException("There has been a problem accessing the file.", exc);
        }
    }
}
```

**With post-save image resizing:**

```csharp
using System.Security.Claims;
using <AppName>.Core.Common.FileSystem.Constants;
using <AppName>.Core.Logic.Exceptions;
using <AppName>.Core.Logic.FileSystem.Abstractions;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Caching.Abstractions;

namespace <AppName>.Core.Logic.FileSystem;

internal sealed class <Name>FileHandler : UmbrellaFileHandler<int>, I<Name>FileHandler
{
    private readonly IDynamicImageResizer _dynamicImageResizer;
    private readonly IDynamicImageUtility _dynamicImageUtility;

    public <Name>FileHandler(
        ILogger<<Name>FileHandler> logger,
        IHybridCache cache,
        ICacheKeyUtility cacheKeyUtility,
        IUmbrellaFileStorageProvider fileProvider,
        IUmbrellaFileStorageProviderOptions options,
        IDynamicImageResizer dynamicImageResizer,
        IDynamicImageUtility dynamicImageUtility)
        : base(logger, cache, cacheKeyUtility, fileProvider, options)
    {
        _dynamicImageResizer = dynamicImageResizer;
        _dynamicImageUtility = dynamicImageUtility;
    }

    public override string DirectoryName => DirectoryNames.<Name>;

    public override Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guard.IsNotNull(fileInfo);

        try
        {
            bool canAccess = ClaimsPrincipal.Current?.Identity?.IsAuthenticated is true;

            return Task.FromResult(canAccess);
        }
        catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.Name }))
        {
            throw new <AppName>CoreLogicException("There has been a problem accessing the file.", exc);
        }
    }

    protected override async Task AfterSavingAsync(IUmbrellaFileInfo fileInfo, int groupId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] imageBytes = await fileInfo.ReadAsByteArrayAsync(cancellationToken: cancellationToken);
        var format = _dynamicImageUtility.ParseImageFormat(Path.GetExtension(fileInfo.Name));
        var (resizedBytes, _, _) = _dynamicImageResizer.ResizeImage(imageBytes, 1600, 1600, DynamicResizeMode.UseWidth, format, DynamicImageFilterQuality.High, 100);
        await fileInfo.WriteFromByteArrayAsync(resizedBytes, cancellationToken: cancellationToken);
    }
}
```

**Rules:**
- Always `internal sealed class` inheriting `UmbrellaFileHandler<int>` and the marker interface
- Base 5 constructor params in this exact order: `ILogger<T>`, `IHybridCache`, `ICacheKeyUtility`, `IUmbrellaFileStorageProvider`, `IUmbrellaFileStorageProviderOptions` -- all passed to `: base(...)`
- Extra dependencies go AFTER the 5 base params and are stored as `private readonly` fields
- `DirectoryName` must return `DirectoryNames.<Name>` -- the same constant added in Step 1
- `AuthorizeAsync` must always call `cancellationToken.ThrowIfCancellationRequested()` and `Guard.IsNotNull(fileInfo)` first, then use the try/catch pattern
- The default authorization check is `ClaimsPrincipal.Current?.Identity?.IsAuthenticated is true` -- tighten this if the use case requires finer-grained access control (e.g., ownership checks for write operations)
- Override `AfterSavingAsync` only if post-save processing is needed (e.g., image resizing); always call `cancellationToken.ThrowIfCancellationRequested()` at the start
- `using Umbrella.DynamicImage.Abstractions;` is only needed when `AfterSavingAsync` is overridden for image work -- check the project `.csproj` to confirm the package is referenced before using it

### Authorization per operation type

When authorization needs to vary by operation, switch on `operationType`:

```csharp
bool canAccess = operationType switch
{
    UmbrellaFileOperationType.Read => ClaimsPrincipal.Current?.Identity?.IsAuthenticated is true,
    UmbrellaFileOperationType.Create or UmbrellaFileOperationType.Update or UmbrellaFileOperationType.Delete
        => ClaimsPrincipal.Current?.IsInRole("Admin") is true,
    _ => false
};
```

---

## Step 4 -- Register in DI

**File:** `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs`

Add one line in the `// File Handlers` section, in alphabetical order:

```csharp
_ = services.AddSingleton<I<Name>FileHandler, <Name>FileHandler>();
```

File handlers are always `AddSingleton` -- they are stateless and safe to share across requests. This is different from services (`AddScoped`).

---

## Verification

1. Confirm `DirectoryNames.<Name>` constant exists in `Core.Common` and is added to the `All` collection.
2. Confirm the interface is an empty marker extending `IUmbrellaFileHandler<int>` with the `using Umbrella.FileSystem.Abstractions;` directive.
3. Confirm the implementation is `internal sealed`, the constructor passes exactly the 5 base params to `: base(...)`, and `DirectoryName` returns the correct `DirectoryNames` constant.
4. Confirm `AuthorizeAsync` calls `ThrowIfCancellationRequested`, `Guard.IsNotNull(fileInfo)`, and wraps the access check in try/catch with `Logger.WriteError`.
5. Confirm `AddSingleton<I<Name>FileHandler, <Name>FileHandler>()` is present in `IServiceCollectionExtensions.cs`.
