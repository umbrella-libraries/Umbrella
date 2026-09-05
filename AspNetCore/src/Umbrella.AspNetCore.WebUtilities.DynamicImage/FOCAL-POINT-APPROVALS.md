# Focal-point approvals

Images without explicit focal coordinates need no signing configuration, including `CropFocalPoint` with its implicit center. Generated catalogs continue to validate dimensions, resize modes, and formats.

With ASP.NET Core middleware validation enabled, explicit X/Y pairs require approval bound to the source path, file version, and canonical coordinates. Missing, invalid, tampered, or stale approvals return `404` before cache access or `304` responses. Signed focal requests never redirect to a newly approved file version.

## Configure once on the server

Keep the existing middleware registration and generated catalogs. Configure signing through the ASP.NET Core registration:

```csharp
services.AddUmbrellaAspNetCoreWebUtilitiesDynamicImage(
    focalPointSigningOptionsBuilder: (_, options) =>
    {
        options.ActiveKeyId = "2026-09";
        options.Keys = new Dictionary<string, string>
        {
            ["2026-09"] = configuration["DynamicImage:SigningKey"]!
        };
    });
```

Use a persistent base64-encoded key containing at least 32 cryptographically random bytes in server secret configuration. Generate it once, not on each startup. Share keys and route configuration across instances. Signing and both renderers default to removing `/files` from source URLs. If renderers use a custom `StripPrefix`, set the same signing option; null or empty disables stripping in the signer. Use distinct keys for independently trusted applications.

Rotation adds a new key identifier and makes it active. Retain previous verification keys to keep existing URLs usable. Removing a key rejects its approvals at the origin. Tokens are deterministic with no automatic expiry. Previously approved coordinates remain valid for the same image version while their key is retained. Cached browser/CDN responses cannot be revoked by removing an origin key.

Empty signing configuration is valid for ordinary images. Invalid configured keys fail service construction; issuing an explicit focal approval without a key throws an actionable error. Missing keys/services never permit unsigned focal requests under validation.

## Resolve and bind one descriptor

Inject `IDynamicImageDescriptorFactory` into server-side model enrichment:

```csharp
model.Image = await imageDescriptorFactory.GetImageAsync(
    fileHandler, entity.Id, entity.ImageFileName,
    entity.ImageFocalPointX, entity.ImageFocalPointY,
    cancellationToken);
```

Alternatively call `Create(versionedUrl, x, y)` with an already resolved `UmbrellaVersionedUrl`. Missing files stay null. Sign trusted loaded or successfully saved metadata; never expose an unrestricted signing endpoint. Approval restricts transforms and does not replace file-access authorization. No entity interface, database migration, approval persistence, or registry is required.

The model exposes a nullable `DynamicImageDescriptor`, carrying URL, version, optional `DynamicImageFocalPoint`, and opaque approval. Finite coordinates in `[0,1]` use invariant `G4` URL precision.

```razor
@if (Model.Image is not null)
{
    <UmbrellaDynamicImage Image="@Model.Image" WidthRequest="400" HeightRequest="200"
                          ResizeMode="DynamicResizeMode.CropFocalPoint" />
}
```

```cshtml
<dynamic-image image="@Model.Image" width-request="400" height-request="200"
               resize-mode="DynamicResizeMode.CropFocalPoint" />
```

Supply the descriptor or individual metadata inputs, not both. For incremental migration, retain URL, version, X and Y inputs and add `FocalPointApproval` (`focal-point-approval` in MVC). UWDI006 warns about missing approval inputs in authored Razor; middleware remains authoritative. Descriptor bindings need no separate version-token input.

Nested sources inherit approval only with the same image, focal point, and version. An independent image needs its own descriptor. WebAssembly receives descriptors, never keys, and can construct responsive URLs locally. Each transform still must match the generated catalog.

## Interactive editing

`UmbrellaFileImagePreviewUpload` accepts `Image` and retains `OnFocalPointChanged`. With literal `EnableFocalPointSelection="true"`, its adjacent canvas crops the already loaded `ScaleDown` selector image. Selection and clearing create no focal-crop HTTP requests. Load the rebuilt `_content/Umbrella.AspNetCore.Blazor/dist/umbrella-blazor.js` bundle.

The preview communicates framing from a downscaled image; sharpness and edge rounding can differ from final output. Its bitmap is not upscaled. It cannot infer original file dimensions from that downscaled source: if both requested dimensions exceed the original, the server's existing no-upscale behavior can retain the full image while the local preview shows the requested crop. Noninteractive previews still render approved server crops.

Persist coordinates through the application's existing authorized save operation and return a fresh descriptor. Use `UpdateImage(descriptor)` to replace upload-preview metadata atomically, or the existing `Update` method with its optional final `focalPointApproval` argument. Programmatic updates do not invoke the selection callback.

## Compatibility

Existing unsigned explicit-focal URLs under validation require migration. Ordinary images, catalogs, and file-provider/cache-policy mappings retain their existing role. Approval is excluded from transformed-image cache identity. Disabling overall middleware validation preserves unrestricted behavior and is not the recommended migration path. Legacy ASP.NET middleware does not implement approval enforcement; shared APIs remain portable.
