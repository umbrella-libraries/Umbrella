; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UA001 | CodeStyle | Error | NullCheckAnalyzer
UA002 | CodeStyle | Error | PrimitiveComparisonAnalyzer
UA003 | CodeStyle | Error | AsyncMethodCancellationAnalyzer
UA004 | CodeStyle | Error | AsyncMethodThrowIfCancellationAnalyzer
UA005 | CodeStyle | Error | EnumerableParameterAnalyzer
UA006 | CodeStyle | Error | ReadOnlyCollectionReturnTypeAnalyzer
UA007 | CodeStyle | Error | NonNullableCollectionReturnTypeAnalyzer
UA008 | CodeStyle | Error | PublicMethodTryCatchAnalyzer
UA009 | CodeStyle | Error | ParameterValidationPlacementAnalyzer
UA010 | CodeStyle | Error | PrimaryConstructorUsageAnalyzer
UA011 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model/QueryResult types must be records
UA012 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model properties must use required
UA013 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model properties must use { get; init; }
UA014 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model collection properties must use IReadOnlyCollection&lt;T&gt;
UA015 | UmbrellaModelStandards | Warning | UmbrellaModelStandardsAnalyzer — model records must be partial when IUmbrellaTrimmable is present
UA016 | UmbrellaModelStandards | Warning | UmbrellaModelStandardsAnalyzer — Create/Update model records with string properties must implement IUmbrellaTrimmable
UA017 | UmbrellaApiStandards | Warning | UmbrellaApiStandardsAnalyzer — UmbrellaApiController subclasses must use generic or non-generic [UmbrellaProducesResponseType] attributes instead of raw ASP.NET Core response type attributes
UA018 | UmbrellaSecurity | Error | AuthorizationHandlerAnalyzer — context.Fail() must not be called in HandleRequirementAsync
UA019 | Architecture | Warning | ControllerEndpointOverrideAnalyzer — controller CRUD endpoint overrides must call base method or be suppressed with [NonAction]
UA020 | DataAccess | Error | EntityQueryParameterAnalyzer — entity types must not be used as parameters to query/lookup methods
UA021 | CodeStyle | Error | PublicMethodLoggerAnalyzer — types with public operational instance methods must provide an accessible ILogger
