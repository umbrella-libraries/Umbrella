# Optional bounded content preparation

Use this pattern only when application-specific inspection requires a small,
bounded buffer. It is not the default upload path. For large content inspection,
choose a streaming validator or bounded temporary-file strategy appropriate to
the app rather than raising an in-memory limit.

For example, an app that already supports transcript purposes could reject a
non-.vtt extension or a declared oversize length in `ValidateUploadAsync`, then
enforce the actual byte limit in preparation. Use the app's enum and constants,
not new framework-wide transcript rules.

This illustrative override assumes the concrete controller defines
`NeedsContentInspection(UploadPurpose)` and a positive `MaximumInspectedBytes`.
The key behavior is the explicit ownership handoff, not the illustrative names:

```csharp
protected override async Task<UmbrellaFileUploadStreamResult> PrepareUploadStreamAsync(
    UmbrellaFileUploadContext<UploadPurpose> context,
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(context);

    if (!NeedsContentInspection(context.UploadType))
        return await base.PrepareUploadStreamAsync(context, cancellationToken);

    var prepared = new MemoryStream();
    bool ownershipTransferred = false;

    try
    {
        byte[] chunk = new byte[81920];
        int bytesRead;

        while ((bytesRead = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (bytesRead > MaximumInspectedBytes - prepared.Length)
                return UmbrellaFileUploadStreamResult.Reject(
                    BadRequest("The uploaded content exceeds the permitted size."));

            await prepared.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        if (prepared.Length is 0)
            return UmbrellaFileUploadStreamResult.Reject(
                BadRequest("The uploaded file has no content."));

        // Apply any actual content-inspection rules here before transferring ownership.
        // Keep the buffer open if a reader is used, and rewind after inspection.
        prepared.Position = 0;
        var result = UmbrellaFileUploadStreamResult.Success(prepared, ownsStream: true);
        ownershipTransferred = true;
        return result;
    }
    finally
    {
        if (!ownershipTransferred)
            await prepared.DisposeAsync();
    }
}
```

The size test runs before writing each chunk, including when Content-Length is
missing. MemoryStream capacity and the read chunk can add allocation overhead;
the limit bounds accepted content bytes, not the exact process memory footprint.
The original request body remains server-owned on every path.

Test accepted content at the limit, a one-byte overflow, empty content and
cancellation. If content validation reads the prepared stream, test that the
stored file still contains every original byte after it is rewound.
