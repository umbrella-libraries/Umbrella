; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UA015 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - model types with DynamicImage URL properties must declare matching VersionToken properties when URL fingerprinting is explicitly enabled
UA016 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - DynamicImage URL assignments must also assign matching VersionToken properties when URL fingerprinting is explicitly enabled
UA017 | DynamicImageVersioning | Error | DynamicImageVersioningAnalyzer - UmbrellaDynamicImage and DynamicImage tag helper usages must assign VersionToken when bound to DynamicImage URL model properties and URL fingerprinting is explicitly enabled
