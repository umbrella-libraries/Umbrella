; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UMA001 | MapperlyRegistration | Error | MapperlyRegistrationAnalyzer - exact IUmbrellaMapper calls must have an exact Mapperly registration, including when no catalog is configured
UMA002 | MapperlyRegistration | Warning | MapperlyRegistrationAnalyzer - open generic IUmbrellaMapper calls are validated against known closed source constructions and warn only when validation remains incomplete
UMA003 | UmbrellaMapperStandards | Warning | MapperlyRegistrationAnalyzer - Mapperly mapper classes must be partial and accessible to the generated catalog; internal mapper types are supported
