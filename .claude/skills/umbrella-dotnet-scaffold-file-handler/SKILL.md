---
name: umbrella-dotnet-scaffold-file-handler
description: 'Scaffold a file handler (interface, implementation, DirectoryNames constant, DI registration) in the Core.Logic project, following the Umbrella UmbrellaFileHandler pattern. Authorization is separate — see umbrella-dotnet-scaffold-file-authorization-handler.'
---

# Scaffold File Handler

## Purpose

Add a new file handler to the `Core.<AppName>.Core.Logic` project. File handlers plug into the Umbrella file storage infrastructure and are responsible for **storage operations only**: saving, retrieving, deleting files, generating web-relative URLs, caching file lookups, and optional post-save processing (e.g. image resizing).

Authorization is decoupled and lives in a separate `UmbrellaFileAuthorizationHandler` — see the `umbrella-dotnet-scaffold-file-authorization-handler` skill. A file handler can exist without a matching authorization handler if access control is handled elsewhere, but be aware that the default storage provider behaviour is to deny access for directories with no registered auth handler.

A file handler can represent any logical grouping of files — files attached to a database record, files in a SharePoint-style folder, user uploads, generated reports, or anything else. The group ID (`int`) is whatever identifier separates one group of files from another for this particular handler.

## How the provider uses the handler

The provider uses the handler's `DirectoryName` to construct the file path (`/<directoryName>/<groupId>/<fileName>`). The `DirectoryName` property on the handler **must exactly match** the constant registered in `DirectoryNames`, as authorization lookup (in the separate auth handler) uses the same value.

## Discovery (read these before writing anything)

1. Read existing file handler implementations in `Core\<AppName>.Core.Logic\FileSystem\` to understand the pattern.
2. Read the interfaces in `Core\<AppName>.Core.Logic\FileSystem\Abstractions\`.
3. Read `Core\<AppName>.Core.Common\FileSystem\Constants\DirectoryNames.cs` to see existing constants and the `All` collection.
4. Read `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs` to see where handlers are registered.

---

## Step 1 -- Add the DirectoryNames constant

**File:** `Core\<AppName>.Core.Common\FileSystem\Constants\DirectoryNames.cs`

Add a new `public const string` entry using lowercase, hyphenated naming (kebab-case):

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

---

## Step 2 -- Create the interface

**File location:** `Core\<AppName>.Core.Logic\FileSystem\Abstractions\I<Name>FileHandler.cs`

```csharp
using Umbrella.FileSystem.Abstractions;

namespace <AppName>.Core.Logic.FileSystem.Abstractions;

public interface I<Name>FileHandler : IUmbrellaFileHandler<int>;
```

**Rules:**
- Empty marker interface — all behaviour comes from the base interface and implementation
- Always extends `IUmbrellaFileHandler<int>`
- Needs the explicit `using Umbrella.FileSystem.Abstractions;` — not in global usings for this project

---

## Step 3 -- Create the implementation

**File location:** `Core\<AppName>.Core.Logic\FileSystem\<Name>FileHandler.cs`

**Minimal pattern (no post-save processing):**

```csharp
using <AppName>.Core.Common.FileSystem.Constants;
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
}
```

**With post-save image resizing:**

```csharp
using <AppName>.Core.Common.FileSystem.Constants;
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
- Base 5 constructor params in this exact order: `ILogger<T>`, `IHybridCache`, `ICacheKeyUtility`, `IUmbrellaFileStorageProvider`, `IUmbrellaFileStorageProviderOptions` — all passed to `: base(...)`
- Extra dependencies go AFTER the 5 base params and are stored as `private readonly` fields
- `DirectoryName` must return `DirectoryNames.<Name>` — the same constant added in Step 1
- Do NOT add `AuthorizeAsync` here — authorization belongs in a separate `UmbrellaFileAuthorizationHandler` (see `umbrella-dotnet-scaffold-file-authorization-handler`)
- Override `AfterSavingAsync` only when post-save processing is needed; always call `cancellationToken.ThrowIfCancellationRequested()` first
- `using Umbrella.DynamicImage.Abstractions;` is only needed when overriding `AfterSavingAsync` for image work — confirm the package is referenced in the `.csproj`

---

## Step 4 -- Register in DI

**File:** `Core\<AppName>.Core.Logic\IServiceCollectionExtensions.cs`

Add one line in the `// File Handlers` section, in alphabetical order:

```csharp
_ = services.AddSingleton<I<Name>FileHandler, <Name>FileHandler>();
```

File handlers are always `AddSingleton` — they are stateless and safe to share across requests.

---

## Verification

1. `DirectoryNames.<Name>` constant exists and is added to the `All` collection.
2. The interface is an empty marker extending `IUmbrellaFileHandler<int>` with the `using Umbrella.FileSystem.Abstractions;` directive.
3. The implementation is `internal sealed`, the constructor passes exactly the 5 base params to `: base(...)`, and `DirectoryName` returns the correct constant.
4. There is no `AuthorizeAsync` on the file handler — authorization is in a separate handler.
5. `AddSingleton<I<Name>FileHandler, <Name>FileHandler>()` is present in `IServiceCollectionExtensions.cs`.
6. If authorization is needed, the `umbrella-dotnet-scaffold-file-authorization-handler` skill has been used to create a matching `<Name>FileAuthorizationHandler` with the same `DirectoryName`.
