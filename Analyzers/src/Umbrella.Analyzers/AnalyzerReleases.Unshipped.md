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
UA012 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — public instance settable model properties must use required; static, non-public, getter-only, model-interface, [UmbrellaInputModel], and [UmbrellaAllowNonRequiredProperty] cases are excluded
UA013 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — public instance model properties must have a getter and use init when settable; concrete input models and properties with [UmbrellaAllowMutableProperty] may use set; mutable concurrency stamps are handled by UA023
UA014 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — model collection properties must expose a read-only contract or recognized immutable collection type; [UmbrellaAllowMutableProperty] permits justified mutable collections
UA015 | UmbrellaModelStandards | Warning | UmbrellaModelStandardsAnalyzer — [UmbrellaInputModel] types declaring trimmable mutable strings must directly implement IUmbrellaTrimmable; technical mutation, [UmbrellaDoNotTrim], and concurrency stamps are excluded, including stamps reached through the read-only IReadOnlyConcurrencyStamp contract
UA016 | CodeStyle | Error | PublicMethodLoggerAnalyzer — types with public operational instance methods must provide an accessible ILogger; bodyless/generated mappings and [DoesNotReturn] control-flow methods are excluded while developer-authored mapper bodies remain eligible
UA017 | UmbrellaApiStandards | Error | UmbrellaApiStandardsAnalyzer — UmbrellaApiController subclasses must use generic or non-generic [UmbrellaProducesResponseType] attributes instead of raw ASP.NET Core response type attributes
UA018 | UmbrellaSecurity | Error | AuthorizationHandlerAnalyzer — authorization handlers must approve successful cases and must not call context.Fail()
UA019 | Architecture | Warning | ControllerEndpointOverrideAnalyzer — overrides of public HTTP endpoints declared by Umbrella generic controller families must call the exact overridden base endpoint on every normal return path or use [NonAction]
UA020 | DataAccess | Error | EntityQueryParameterAnalyzer — entity values and immediate entity sequences must not be used as criteria on changeable public query contracts
UA021 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — [UmbrellaInputModel] may only be applied directly to a concrete type and never flows through inheritance
UA022 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — concrete model record classes must be sealed unless [UmbrellaAllowUnsealedModel] documents intentional inheritance
UA023 | UmbrellaModelStandards | Error | UmbrellaModelStandardsAnalyzer — non-input model types must use IReadOnlyConcurrencyStamp rather than mutable IConcurrencyStamp
UA024 | UmbrellaApiStandards | Warning | DataExpressionQueryParameterAnalyzer — a single SortExpression<TItem>/FilterExpression<TItem> action parameter is flattened by ApiExplorer and hangs OpenAPI document generation; the collection form and the descriptor types are unaffected
