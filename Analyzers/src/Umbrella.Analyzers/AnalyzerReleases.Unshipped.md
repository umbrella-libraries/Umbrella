; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UA001 | CodeStyle | Error | NullCheckAnalyzer
UA002 | CodeStyle | Error | PrimitiveComparisonAnalyzer
UA003 | CodeStyle | Error | AsyncMethodCancellationAnalyzer — changeable public async methods require the canonical CancellationToken parameter; middleware entry points are excluded
UA004 | CodeStyle | Error | AsyncMethodThrowIfCancellationAnalyzer
UA005 | CodeStyle | Error | EnumerableParameterAnalyzer
UA006 | CodeStyle | Error | ReadOnlyCollectionReturnTypeAnalyzer
UA007 | CodeStyle | Error | NonNullableCollectionReturnTypeAnalyzer
UA008 | CodeStyle | Error | PublicMethodTryCatchAnalyzer — logger-owning operational methods require state-aware exception handling; bodyless/generated mappings and [DoesNotReturn] control-flow methods are excluded while developer-authored mapper bodies remain eligible
UA009 | CodeStyle | Error | ParameterValidationPlacementAnalyzer
UA010 | CodeStyle | Error | PrimaryConstructorUsageAnalyzer
UA011 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model/QueryResult types must be records; ASP.NET Core Razor Pages PageModel descendants are excluded
UA012 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model properties must use required
UA013 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model properties must use { get; init; }
UA014 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model collection properties must use IReadOnlyCollection&lt;T&gt;
UA015 | UmbrellaModelStandards | Warning | UmbrellaModelStandardsAnalyzer — model classes and records with public mutable string properties must implement IUmbrellaTrimmable
UA017 | UmbrellaApiStandards | Warning | UmbrellaApiStandardsAnalyzer — UmbrellaApiController subclasses must use generic or non-generic [UmbrellaProducesResponseType] attributes instead of raw ASP.NET Core response type attributes
UA018 | UmbrellaSecurity | Error | AuthorizationHandlerAnalyzer — authorization handlers must approve successful cases and must not call context.Fail()
UA019 | Architecture | Warning | ControllerEndpointOverrideAnalyzer — overrides of public HTTP endpoints declared by Umbrella generic controller families must call the exact overridden base endpoint on every normal return path or use [NonAction]
UA020 | DataAccess | Error | EntityQueryParameterAnalyzer — entity values and immediate entity sequences must not be used as criteria on changeable public query contracts
UA021 | CodeStyle | Error | PublicMethodLoggerAnalyzer — types with public operational instance methods must provide an accessible ILogger; bodyless/generated mappings and [DoesNotReturn] control-flow methods are excluded while developer-authored mapper bodies remain eligible
