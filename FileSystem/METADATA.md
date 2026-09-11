# File metadata providers

File content and metadata can use different storage backends. Configure a metadata provider on each storage-provider instance; existing `IUmbrellaFileInfo` calls continue to work.

| File provider | Default metadata backend |
| --- | --- |
| Disk | JSON `.umfsmeta` sidecar |
| Azure Blob Storage | Native blob metadata |
| Dataverse | Configured typed column mappings |
| SharePoint | Unsupported (`NotSupportedException`) |

## SharePoint with application-owned database metadata

Register a singleton implementation of `IUmbrellaFileMetadataProvider`, then select it in the existing options builder:

```csharp
services.AddSingleton<IUmbrellaFileMetadataProvider, ApplicationFileMetadataProvider>();

services.AddUmbrellaSharePointFileStorageProvider((serviceProvider, options) =>
{
    options.GraphServiceClient = serviceProvider.GetRequiredService<GraphServiceClient>();
    options.DownloadHttpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
        .CreateClient("SharePointDownloads");
    options.SiteId = configuration["SharePoint:SiteId"]!;
    options.DriveName = configuration["SharePoint:DriveName"]!;
    options.MetadataProvider = serviceProvider.GetRequiredService<IUmbrellaFileMetadataProvider>();
    options.MetadataNamespace = "tenant-a/contracts-library";
});
```

Keep the existing file authorization handler registrations. This example does not enable unrestricted file access.

The same options are available on Disk, Azure, and Dataverse. The backend and namespace are captured during provider initialization; configure a new storage-provider instance to change either. Multiple instances may share a thread-safe metadata provider while using different namespaces.

## Implementing the backend

`IUmbrellaFileMetadataProvider.CreateSession` receives `UmbrellaFileMetadataContext` and returns an independent session without performing I/O. External persistence should identify a file by **both** `StorageNamespace` and `SubPath`. Do not use only the filename, provider type, or physical SharePoint path.

Derive the session from `UmbrellaFileMetadataSession` to reuse conversion, caching, deferred writes, and retry handling. This application-level adapter illustrates the boundary with your database service; `IApplicationMetadataStore` below is an example contract, not an Umbrella dependency:

```csharp
public sealed class ApplicationFileMetadataProvider(
    IApplicationMetadataStore store,
    IGenericTypeConverter converter) : IUmbrellaFileMetadataProvider
{
    public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
        => new Session(store, converter, context);

    public Task DeleteFileAsync(UmbrellaFileMetadataContext context,
        CancellationToken cancellationToken = default)
        => store.DeleteFileAsync(context.StorageNamespace!, context.SubPath, cancellationToken);

    public Task DeleteDirectoryAsync(string? storageNamespace, string subPath,
        CancellationToken cancellationToken = default)
        => store.DeleteDirectoryAsync(storageNamespace!, subPath, cancellationToken);

    private sealed class Session(IApplicationMetadataStore store,
        IGenericTypeConverter converter, UmbrellaFileMetadataContext context)
        : UmbrellaFileMetadataSession(converter)
    {
        protected override Task<Dictionary<string, object?>> ReadCoreAsync(
            CancellationToken cancellationToken)
            => store.ReadAsync(context.StorageNamespace!, context.SubPath, cancellationToken);

        protected override Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes,
            bool clear, CancellationToken cancellationToken)
            => store.ApplyAsync(context.StorageNamespace!, context.SubPath,
                changes, clear, cancellationToken);
    }
}

public interface IApplicationMetadataStore
{
    Task<Dictionary<string, object?>> ReadAsync(string storageNamespace, string subPath,
        CancellationToken cancellationToken);
    Task ApplyAsync(string storageNamespace, string subPath,
        IReadOnlyDictionary<string, object?> changes, bool clear,
        CancellationToken cancellationToken);
    Task DeleteFileAsync(string storageNamespace, string subPath,
        CancellationToken cancellationToken);
    Task DeleteDirectoryAsync(string storageNamespace, string subPath,
        CancellationToken cancellationToken);
}
```

The database service owns its schema, serialization, and concurrency policy. Apply each mutation batch in a database transaction: clear first when requested, then apply values; null means remove the key. Retries must be idempotent. Preserve unrelated values when `clear` is false. Generic values reach `TryConvertValue` intact; override it and `ConvertToString` when the backend needs its own representation. Native Dataverse uses this to preserve typed columns.

Storage providers are singletons. A database service should use `IDbContextFactory<TContext>` or create a DI scope inside each operation. Do not inject a scoped `DbContext` into the singleton or retain one in a metadata session. Dispose operation-owned resources before returning. Sessions are intended for sequential use by one file-info instance, not concurrent mutation.

Directory cleanup must match the exact directory and its descendants at slash boundaries, in the specified namespace only. For example, `/contracts` must not match `/contracts-archive`. Escape SQL pattern characters if implementing this with `LIKE`; a raw prefix comparison is insufficient. File and directory deletion must succeed when no metadata exists.

## Behaviour and failure handling

- Metadata is available after a newly created file has first been written. Calls with `writeChanges: false` stage changes locally; getters include those changes. `WriteMetadataChangesAsync` authorizes and persists the batch. Failed writes retain the batch for retry.
- Reads can be used inside file authorization handlers without recursively invoking authorization. Missing-file cleanup also permits the delete handler to read surviving metadata. Cleanup never substitutes create authorization for delete authorization.
- Copy authorizes the destination before writing content: Create for a new destination, Update for an existing destination. Metadata persistence uses that same authorization; ordinary later metadata edits still require Update.
- Supported copy operations replace destination metadata with persisted source values, including clearing the destination when the source has none. Pending source changes are not committed. Pending destination changes are replaced. Copying nonempty metadata to an unsupported backend fails rather than silently losing it.
- Move completes content and metadata copying before deleting the source. Native Azure copies await server-side completion; copies involving custom Azure metadata stream content to avoid implicitly copying inactive native metadata.
- Content updates retain the active metadata. File deletion cleans metadata after content deletion; retry path-based deletion if cleanup fails after the content is gone. Directory cleanup runs only after the entire content deletion succeeds. Native Dataverse continues retaining mapped columns when deleting only file content.
- Content storage and an external database do not share a transaction. Partial failures are surfaced as exceptions with file/operation context. A destination may contain copied content after metadata copying fails. Directory failures can leave partially removed content while metadata remains. Reconciliation, retry scheduling, and monitoring belong to the application; there is no automatic rollback or background worker.
- Cancellation is propagated. When an Azure server copy is canceled locally, the remote operation may still finish; the source is not deleted by the interrupted move.

## Switching backends and identity

A configured backend is exclusive: no native read fallback, merging, dual writes, or automatic migration occurs. Migrate any existing metadata separately before switching, and retain a stable namespace across application restarts. Inactive sidecars or mapped columns are not individually changed. Physical removal or replacement of content may inherently remove attached native metadata.

Logical paths use Umbrella's existing slash normalization and lowercase rules, before SharePoint translation. Dataverse additionally canonicalizes record IDs to hyphenated GUID (`D`) format for creation, lookup, enumeration, and cleanup, regardless of the accepted input GUID format. An external rename or deletion performed outside Umbrella does not update database metadata. Reusing a path can reuse its surviving metadata; applications that allow out-of-band changes must reconcile these records. This implementation does not track SharePoint drive-item identities or broaden Dataverse's unsupported copy/move operations.
