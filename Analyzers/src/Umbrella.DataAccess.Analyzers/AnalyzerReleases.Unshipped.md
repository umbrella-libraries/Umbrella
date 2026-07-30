; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UDA001 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — single-result repository queries must start with 'Find'
UDA002 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — collection repository queries must start with 'FindAll'
UDA003 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — count repository queries must start with 'Find' and identify the count
UDA004 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — boolean existence queries must start with 'Exists'
UDA005 | UmbrellaDataAccess | Error | RepositoryIQueryableAnalyzer — public repository methods must not expose IQueryable<T>, including nested and derived query contracts
