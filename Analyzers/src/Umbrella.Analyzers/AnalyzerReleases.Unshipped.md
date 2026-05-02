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