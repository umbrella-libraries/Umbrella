; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UWDI001 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - model types with DynamicImage URL properties must declare matching VersionToken properties when URL fingerprinting is explicitly and statically enabled
UWDI002 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - DynamicImage URL assignments must also assign matching VersionToken properties when URL fingerprinting is explicitly and statically enabled
UWDI003 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - UmbrellaDynamicImage and DynamicImage tag helper usages must assign VersionToken when bound to DynamicImage URL model properties and URL fingerprinting is explicitly and statically enabled
UWDI004 | DynamicImageGeneration | Warning | DynamicImageVersioningAnalyzer - DynamicImage usages with non-static variant-shaping inputs reduce source-generated variant discovery and validation coverage regardless of whether URL fingerprinting is enabled
