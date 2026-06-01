; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UDA001 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — repository methods returning a single item must start with 'FindBy'
UDA002 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — repository methods returning a collection must start with 'FindAllBy'
UDA003 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — repository methods returning a count must start with 'FindCount'
UDA004 | UmbrellaDataAccess | Warning | RepositoryMethodNamingAnalyzer — repository methods returning a boolean must start with 'Exists'
UDA005 | UmbrellaDataAccess | Error | RepositoryIQueryableAnalyzer — repository methods must not return IQueryable<T>
