# HTTP byte ranges

ASP.NET Core File System middleware automatically supports single byte ranges for Disk, Azure Blob Storage, SharePoint, and Dataverse. This enables video seeking and partial downloads for any content type. Existing mappings and URLs need no changes. Legacy OWIN middleware is unchanged.

Successful full responses and partial responses advertise `Accept-Ranges: bytes`. A request such as `Range: bytes=1000-1999` returns `206 Partial Content`, `Content-Range: bytes 1000-1999/<file length>`, and a 1000-byte body. Open-ended and suffix ranges are also supported. Valid unsatisfiable ranges return an empty `416` with `Content-Range: bytes */<file length>`.

Malformed ranges, unsupported units, and requests for multiple ranges receive the full response. `HEAD` ignores Range and does not open a content stream. Conditional cache responses take precedence, and `If-None-Match` takes precedence over `If-Modified-Since`. `If-Range` requires an exact strong ETag or matching Last-Modified date; a missing or mismatched validator results in a full response.

## Provider efficiency

| Provider | Behavior |
| --- | --- |
| Disk | Seeks directly to the offset and limits the bytes read. |
| Azure Blob Storage | Requests the requested offset and length through the Blob SDK. |
| SharePoint | Gets the Graph download URL, then sends Range directly to it. If the upstream server returns the full content, skips the prefix with bounded memory and returns only the requested bytes. |
| Dataverse | Retrieves and decodes the existing base64 value, then exposes the requested slice. Browser seeking works, but storage transfer and memory use can still equal the full file size. |

SharePoint uses a shared download HTTP client separate from the Graph client. `UmbrellaSharePointFileStorageProviderOptions.DownloadHttpClient` allows supplying a reusable client for transport configuration or testing. A supplied client is caller-owned and must not attach Graph credentials, authorization headers, or cookies. Download URLs are preauthenticated, used only server-side, and must not be logged.

## Custom providers

Implement `IUmbrellaRangeReadableFileInfo` in addition to the existing file contract and provide:

```csharp
Task<Stream> ReadRangeAsStreamAsync(
    long offset,
    long length,
    int? bufferSizeOverride = null,
    CancellationToken cancellationToken = default);
```

Authorize the read before returning the stream. Require a nonnegative offset, positive length, and a range entirely within the file. Validate without overflowing `offset + length`. The caller owns the returned stream and its underlying resources. Return only the specified bytes, propagate cancellation, and throw on premature EOF.

`UmbrellaFileRangeStream.Validate` checks bounds. After positioning or requesting the source, `new UmbrellaFileRangeStream(source, length, optionalResponseOwner)` creates a bounded, forward-only stream and owns its resources. Existing custom providers remain compatible and continue returning full-file responses until they implement this optional contract.

## Verification

`FileSystemMiddlewareTest` exercises HTTP status, headers, conditional requests, and failure behavior. `UmbrellaFileRangeTest` exercises bounded reads, Disk seeking, mocked Azure and SharePoint transfers, and the Dataverse base64 format without requiring remote credentials.

For a manual playback check, serve a large MP4 through an existing File System mapping, load it in an HTML video element, and seek to an unbuffered position. Browser network tools should show a new Range request and a `206` response with the matching Content-Range. Check Disk and each configured remote provider; for Dataverse expect full-value retrieval internally.
