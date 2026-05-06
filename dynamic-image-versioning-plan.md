Problem: make DynamicImage URLs CDN-friendly and SEO-safe by introducing optional path-based versioning so cache keys can change when the source image changes, while the middleware canonicalizes both versioned and unversioned URLs with a configurable redirect status code instead of 404ing.

Current status:
- Completed:
  - di-version-config: added configuration for URL fingerprinting, configurable canonical redirect status code, and DynamicImage cacheability alignment with FileSystem middleware.
  - di-version-utility: added dual URL parsing/generation support for versioned and unversioned URL shapes using a reserved `_v_...` version segment.
  - di-version-middleware-align: updated DynamicImageMiddleware to canonicalize requests and align cache/header behavior with FileSystem middleware.
  - di-version-cache-key: made resized-image cache identity version-aware only when fingerprinting is enabled.
  - di-version-callers: updated active ASP.NET Core DynamicImage emitters to generate canonical versioned/unversioned URLs.
- Remaining:
  - di-version-tests: add focused tests for parsing, generation, redirect behavior, canonicalization, and cache-key changes.
- Superseded by the completed decomposition above:
  - di-version-url-shape
  - di-version-token
  - di-version-middleware

Key implementation anchors:
- Primary middleware/runtime files:
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Middleware\DynamicImageMiddleware.cs`
  - `Core\src\Umbrella.WebUtilities.DynamicImage\Middleware\Options\DynamicImageMiddlewareOptions.cs`
  - `Core\src\Umbrella.WebUtilities.DynamicImage\Middleware\Options\DynamicImageMiddlewareMapping.cs`
- URL parsing/generation and cache identity:
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\DynamicImageUtility.cs`
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\IDynamicImageUtility.cs`
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\DynamicImageOptions.cs`
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\DynamicImageUrlPathShape.cs`
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\Caching\DynamicImageCache.cs`
- Active URL emitters/callers:
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Mvc\TagHelpers\DynamicImageTagHelperBase.cs`
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Mvc\TagHelpers\DynamicImageTagHelper.cs`
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Mvc\TagHelpers\DynamicImagePictureSourceTagHelper.cs`
  - `AspNetCore\src\Umbrella.AspNetCore.Blazor\Components\DynamicImage\UmbrellaDynamicImage.razor.cs`
  - `AspNetCore\src\Umbrella.AspNetCore.Blazor\Components\DynamicImage\Options\UmbrellaDynamicImageOptions.cs`
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Mvc\TagHelpers\Options\DynamicImageTagHelperOptions.cs`

Important configuration/property names:
- `EnableUrlFingerprinting`
- `CanonicalRedirectStatusCode`
- `MaxAgeSeconds`
- `DynamicImageOptions.VersionToken`
- `DynamicImageOptions.UrlPathShape`
- `DynamicImageUrlPathShape`
- reserved version path segment prefix: `_v_`

Implemented behavior:
- DynamicImage supports both versioned and unversioned URL shapes.
- URL fingerprinting is configurable.
- Canonical redirect status code is configurable.
- When fingerprinting is enabled:
  - unversioned requests redirect to the current canonical versioned URL
  - stale or mismatched versioned requests redirect to the current canonical versioned URL
  - current versioned requests serve normally
- When fingerprinting is disabled:
  - versioned requests redirect to the canonical unversioned URL
  - unversioned requests serve normally
- DynamicImage cache identity includes the version token only for versioned URLs.
- DynamicImage middleware cache/header behavior is aligned with FileSystem middleware:
  - NoCache
  - NoStore
  - Private
  - Public
  - optional MaxAgeSeconds
  - forced must-revalidate for Private/Public

Canonical behavior matrix:

| Fingerprinting | Incoming URL | Result |
| --- | --- | --- |
| Enabled | Unversioned | Redirect to canonical current versioned URL |
| Enabled | Versioned but stale/mismatched | Redirect to canonical current versioned URL |
| Enabled | Versioned and current | Serve normally |
| Disabled | Versioned | Redirect to canonical unversioned URL |
| Disabled | Unversioned | Serve normally |

Versioning/cache details:
- The version token is path-based, not query-string-based.
- The version path segment uses a reserved `_v_...` form so parsing can distinguish it from the source path.
- The version segment affects canonical URL identity and resized-image cache identity, but not source-path resolution.
- Cache-key versioning is conditional:
  - versioned URLs include `VersionToken` in the hash/cache identity
  - unversioned URLs keep legacy cache identity
- DynamicImage cached output remains based on the requested transform plus source path; source replacement with the same URL shape/version behavior relies on canonical versioning and source metadata.

Validation completed during implementation:
- Targeted builds succeeded for the directly impacted projects, including:
  - `DynamicImage\src\Umbrella.DynamicImage.Abstractions\Umbrella.DynamicImage.Abstractions.csproj`
  - `DynamicImage\src\Umbrella.DynamicImage.Caching.Disk\Umbrella.DynamicImage.Caching.Disk.csproj`
  - `DynamicImage\src\Umbrella.DynamicImage.Caching.AzureStorage\Umbrella.DynamicImage.Caching.AzureStorage.csproj`
  - `AspNetCore\src\Umbrella.AspNetCore.WebUtilities.DynamicImage\Umbrella.AspNetCore.WebUtilities.DynamicImage.csproj`
  - `AspNetCore\src\Umbrella.AspNetCore.Blazor\Umbrella.AspNetCore.Blazor.csproj`
- Earlier targeted utility validation also covered:
  - `DynamicImage\test\Umbrella.DynamicImage.Test\Umbrella.DynamicImage.Test.csproj`
  - `dotnet test` filtered to `DynamicImageUtilityTest`
- During the work there were notes about unrelated broader multi-target build noise outside the immediate todo scope; the targeted project validations for the changed areas succeeded.

Recommended remaining test coverage:
- URL parsing:
  - versioned URL shape
  - unversioned URL shape
  - `_v_...` parsing edge cases
  - focal point and `@Nx` density with both shapes
- URL generation:
  - versioned generation when fingerprinting is enabled
  - unversioned generation when fingerprinting is disabled
  - canonical `src` / `srcset` emission from tag helpers and Blazor component
- Canonical redirect behavior:
  - enabled + unversioned => redirect to versioned
  - enabled + stale versioned => redirect to current versioned
  - disabled + versioned => redirect to unversioned
  - configured redirect status code honored
- Cache identity:
  - version token included only for versioned URLs
  - unversioned cache keys remain backward-compatible
- Middleware cache/header alignment:
  - `NoCache`, `NoStore`, `Private`, `Public`
  - `MaxAgeSeconds`
  - `must-revalidate`
  - `Expires`
  - `ETag` / `Last-Modified`
  - `304` behavior
  - `X-Content-Type-Options: nosniff`

Notes:
- Redirect status code should remain configurable rather than fixed to 301.
- The version token is path-based for Azure Front Door friendliness; query-string versioning is out of scope for this change.
- The version segment affects URL/cached-image identity but does not alter the resolved source path.
- DynamicImage is the current scope; applying the same versioning approach to FileSystem middleware can follow later.
