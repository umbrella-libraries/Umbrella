; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UA019 | MapperlyRegistration | Error | MapperlyRegistrationAnalyzer - exact IUmbrellaMapper calls must have an exact Mapperly registration
UA020 | MapperlyRegistration | Warning | MapperlyRegistrationAnalyzer - open generic IUmbrellaMapper calls cannot be fully validated
