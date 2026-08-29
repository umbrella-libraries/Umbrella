; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UWDI001 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - model types with DynamicImage URL properties must declare matching VersionToken properties when URL fingerprinting is explicitly enabled by local registration or the compiler-visible build property
UWDI002 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - DynamicImage URL assignments must also assign matching VersionToken properties when URL fingerprinting is explicitly enabled by local registration or the compiler-visible build property
UWDI003 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - direct Razor and manually authored C# usages of UmbrellaDynamicImage, UmbrellaFileImagePreviewUpload, and DynamicImage tag helpers must assign VersionToken when bound to DynamicImage URL model properties and URL fingerprinting is explicitly enabled by local registration or the compiler-visible build property
UWDI004 | DynamicImageGeneration | Warning | DynamicImageVersioningAnalyzer - Dynamic Image and file-image-preview usages with Razor variant-shaping values other than literals or enum members, including unqualified enum members without a matching effective `@using static` directive and a runtime-bound EnableFocalPointSelection value, or non-static manually authored C# inputs, are omitted from generated catalogs regardless of whether URL fingerprinting is enabled
