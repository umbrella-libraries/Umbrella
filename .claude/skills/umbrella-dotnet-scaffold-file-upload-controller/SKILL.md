---
name: umbrella-dotnet-scaffold-file-upload-controller
description: 'Create or migrate a raw-body temporary-file upload controller using UmbrellaFileUploadController, preserving application authorization, client contracts, streaming and extension hooks. Not for multipart form uploads or file-download endpoints.'
---

# Scaffold File Upload Controller

## Scope and discovery

Use this skill for a new upload endpoint or migration of a hand-written one to
`UmbrellaFileUploadController<TUploadType>`. Keep application rules in the concrete
controller and reusable storage orchestration in the base.

Before editing, inspect:

- The current controller and client: route, raw-body format, header names, enum values,
  authorization policy, request limit, response body/Location, metadata and permissions.
- The installed `Umbrella.AspNetCore.WebUtilities.FileSystem` API, provider and temp-handler
  registrations. This API and the non-seekable disk fix shipped in preview-0106;
  verify availability in the installed version rather than copying a local assembly.
- The app's controller bases, error mapping and middleware. Moving away from an app base
  can lose inherited routing, authorization, filters or helpers; preserve relevant behavior.
- Existing upload tests and any content-specific validation. An enum value being defined
  does not imply it is an allowed upload purpose (for example, an `Undefined` sentinel).

If the API is missing, use the available `umbrella-nuget-safe-upgrade` workflow when an
upgrade is in scope. Upgrade related Umbrella packages together and verify restored
versions from the configured feed; do not infer availability from nuget.org alone.
Do not publish packages or commit/push as part of scaffolding unless requested.

## Concrete controller

The base lives in `Umbrella.AspNetCore.WebUtilities.FileSystem.Mvc`. The generic argument
is the application's upload-purpose enum (`where TUploadType : struct, Enum`), not a
MIME type or a framework-owned enum. Preserve existing enum values used by clients.

- Derive directly from this specialized base, or an existing app upload base derived
  from it. Do not use an unrelated generic API base or add an intermediate layer solely
  to satisfy the generic custom-controller skill's conventions.
- Put the route and explicit authorization decision on the concrete controller. The
  Umbrella upload base deliberately has **no `[Authorize]`**. Preserve existing policies;
  use `[AllowAnonymous]` only when anonymous uploading is explicitly intended.
- Put the application's `[RequestSizeLimit(...)]` on the concrete class so it applies
  to the inherited action. Preserve the existing limit; do not adopt a universal 1 GiB
  limit. Check server/proxy limits when end-to-end limits are part of the task.
- Inject `ILogger<ConcreteController>`, `IWebHostEnvironment`,
  `IUmbrellaFileStorageProvider` and `IUmbrellaTempFileHandler`, forwarding them to
  `base(logger, hostingEnvironment, fileProvider, tempFileHandler)`.
- Inherit the non-virtual `[HttpPost] PostAsync`. Do not hide it with another action,
  duplicate its orchestration or register the abstract controller in DI.

Default requests carry raw bytes with `X-FileName`, `X-FileContentType` and
`X-FileUploadType` headers. Success is 201 with the generated temporary filename in
the response body and temporary URL in Location. Header changes require matching
client changes; multipart `IFormFile` requests are not this protocol.

## Extension points

Only override what the application needs:

| Member | Contract |
|---|---|
| `FileNameHeaderName`, `ContentTypeHeaderName`, `UploadTypeHeaderName` | Protected virtual string properties; retain defaults unless the client uses different names. |
| `DevelopmentUploadDelay` | Protected virtual `TimeSpan`; defaults to zero and is used only in Development. Preserve an existing UI-testing delay, not a mandatory two seconds. |
| `IsUploadTypeAllowed(TUploadType)` | Call base to retain defined-enum validation, then reject sentinel or unsupported purposes. |
| `ValidateUploadAsync(context, cancellationToken)` | `Task<IActionResult?>`; return a rejection via Umbrella status helpers or null/base to continue. Inspect metadata before reading the body. |
| `PrepareUploadStreamAsync(context, cancellationToken)` | `Task<UmbrellaFileUploadStreamResult>`; default forwards `Request.Body` unbuffered and unowned. Return `Success(stream, ownsStream)` or `Reject(response)`. |
| `WriteUploadMetadataAsync(context, fileInfo, cancellationToken)` | `Task`; preserve base filename/purpose metadata, temp permissions and metadata persistence. Call base, and persist any extra changes made afterwards. |
| `CreateUploadResult(context, fileInfo)` | `IActionResult`; success-only response customization. Reject in validation/preparation instead: returning an error here still marks storage complete. |

`context` is the immutable `UmbrellaFileUploadContext<TUploadType>` record with
`FileName`, `FileExtension`, `ContentType`, nullable `ContentLength`, `UploadType`,
`TempFileName`, `TempPath` and `TempUrl`. Do not mutate it or trust its declared
content type as content validation. The storage provider still infers content type
from the extension.

The lifecycle is validation, preparation, save, actual-empty-file check, metadata,
then response. Keep application rejections before save where practical. The base
propagates cancellation, maps known exceptions and handles remaining failures.
Do not add a broad catch that converts cancellation to a validation error.

## Streams, limits and cleanup

- Ordinary uploads should remain forward-only. Do not access `Request.Body.Length`
  or `Position`, call `EnableBuffering`, or copy every body to memory.
- Missing `Content-Length` is supported. A declared length can reject early, but cannot
  establish an actual content bound; count bytes while reading when enforcing one.
- Use content-specific buffering only when a rule needs it. For a small bounded
  transcript example, read [references/bounded-content.md](references/bounded-content.md).
  Do not add transcript enums, .vtt rules or a 1 MiB cap to apps that do not need them.
- Rewind owned replacement buffers before returning `Success(..., ownsStream: true)`.
  The base disposes them. If preparation rejects or throws before transfer, the
  override must dispose its own allocation. `Reject` transfers no stream ownership.
- Do not dispose the request body. Any owned wrapper around it must leave it open.
  Earlier middleware must not consume it without restoring readable content.
- After a storage attempt that does not complete, the base tries to delete its generated
  temporary path using an independent bounded cancellation token. Cleanup is best-effort;
  preserve the app's normal temporary-file expiry process. Do not duplicate deletion
  in hooks or broaden cleanup to other files.
- Verify the selected provider supports non-seekable streams; a MemoryStream-only test
  does not prove real HTTP uploads work. Provider-specific buffering may still occur.

## Migration and verification

For migration, map existing guards and special cases to the hooks above, remove the old
action, and remove dependencies only when no longer used. Preserve the client-visible
contract and any inherited app behavior intentionally. For a new endpoint, agree on
the application-specific policy, enum and limits before inventing them.

Read `.ai-shared\bundles\umbrella\analyzer-compatibility.md` when available and build the
affected server with its installed analyzers. Match existing test infrastructure.
Cover the changed contract, including:

- Concrete authorization (401 when required, policy 403 where applicable), routing and
  successful 201 filename/Location responses.
- Missing/blank filename, invalid or disallowed purpose, and declared/actual empty bodies.
- Exact stored bytes, standard metadata and temp permissions for ordinary and lengthless uploads.
- Each content-specific rule; if bounded, test actual length at the limit and one byte
  over without Content-Length as well as declared oversize rejection.
- Owned-stream disposal on success/rejection/failure, request-body lifetime, cancellation
  and failed-upload cleanup for custom hooks. Reuse base tests for unchanged base behavior.

When adding provider or streaming coverage, include a real Kestrel non-seekable request
body: in-memory test hosts may expose seekable streams. Run container-backed tests on a
CI job with the required container support; do not silently skip them on unsupported hosts.
Keep inherited response declarations in mind; use `UmbrellaProducesResponseType` for any
additional contract declarations without introducing a duplicate action.

Report changed files, validation results and any remaining compatibility limits.
Scaffolding an upload endpoint does not authorize replacing storage or widening access.

## Companion workflows

- `umbrella-dotnet-scaffold-file-handler`: application file handlers and registrations.
- `umbrella-dotnet-scaffold-file-authorization-handler`: stored-file authorization,
  separate from controller authentication; do not disable it to make uploads pass.
- `umbrella-dotnet-scaffold-aspnetcore-integration-tests`: test infrastructure when absent.
