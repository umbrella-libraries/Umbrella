# Extensible temporary uploads

`UmbrellaFileUploadController<TUploadType>` in `Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc` accepts raw HTTP request bodies.
The application supplies its upload-purpose enum and a concrete controller.

```csharp
[Authorize]
[Route("api/[controller]")]
[RequestSizeLimit(1_073_741_824)]
public class FileUploadController : UmbrellaFileUploadController<FileUploadType>
{
    public FileUploadController(
        ILogger<FileUploadController> logger,
        IWebHostEnvironment environment,
        IUmbrellaFileStorageProvider provider,
        IUmbrellaTempFileHandler tempFileHandler)
        : base(logger, environment, provider, tempFileHandler) { }

    protected override TimeSpan DevelopmentUploadDelay => TimeSpan.FromSeconds(2);

    protected override bool IsUploadTypeAllowed(FileUploadType uploadType)
        => base.IsUploadTypeAllowed(uploadType) && uploadType != FileUploadType.Undefined;
}
```

The base deliberately supplies **no authorization, route, or request-size limit attributes**.
Applications choose these on the concrete controller. A zero enum value is valid by default if defined;
override `IsUploadTypeAllowed` to reject an application sentinel.

## Request and response

Default headers are `X-FileName`, `X-FileContentType`, and `X-FileUploadType`.
Override `FileNameHeaderName`, `ContentTypeHeaderName`, and `UploadTypeHeaderName` to change them.
The filename and upload purpose are required; enum names are parsed case-insensitively.
The declared content type is untrusted information available to hooks. Storage continues to infer its
content type from the extension. Successful uploads return 201, the generated temporary filename as
the response body, and the temporary URL in Location.

Ordinary bodies stream directly to the provider, including requests without Content-Length.
There is no request-wide buffering or default development delay.
Provider-specific block buffering can still occur.

## Extension lifecycle

1. `ValidateUploadAsync`: inspect immutable upload context and return null to continue or an
   IActionResult to reject, without reading the body.
2. `PrepareUploadStreamAsync`: return `Success(stream, ownsStream)` or `Reject(response)`.
   The default uses Request.Body with ownsStream false. For content validation, a subclass may read
   into a bounded buffer, validate, rewind, and return it with ownsStream true.
3. The base stores the prepared stream and rejects zero-byte files.
4. `WriteUploadMetadataAsync`: write filename and purpose metadata, apply the temp handler's
   permissions, then flush metadata. Call base from overrides to preserve those operations.
5. `CreateUploadResult`: customise the response after successful storage and metadata processing.
   This is a success hook; use validation/preparation hooks to reject uploads.

The base disposes owned replacement streams after processing, but never directly disposes Request.Body.
Wrappers around Request.Body must leave it open when disposed. Preparation overrides must dispose their
own allocations if they reject or throw before transferring ownership. When streaming directly,
consumption begins at the request body's current position; preceding middleware must not consume it.

## Failures

Cancellation propagates. Other exceptions use the inherited exception mapper, then propagate in
Development or return the standard production 500 response. If storage was attempted but processing
did not finish, the base attempts deletion of its generated temporary path with a separate ten-second
cleanup token. Cleanup/disposal failures are logged without replacing the original response or exception.
Storage authorization still applies during cleanup, so applications should retain their normal
temporary-file expiry process for failures that cannot be cleaned immediately.
