; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UMA001 | MapperlyRegistration | Error | MapperlyRegistrationAnalyzer - exact IUmbrellaMapper calls must have an exact Mapperly registration
UMA002 | MapperlyRegistration | Warning | MapperlyRegistrationAnalyzer - open generic IUmbrellaMapper calls cannot be fully validated
UMA003 | UmbrellaMapperStandards | Warning | MapperlyRegistrationAnalyzer - Mapperly mapper classes must be public partial class
