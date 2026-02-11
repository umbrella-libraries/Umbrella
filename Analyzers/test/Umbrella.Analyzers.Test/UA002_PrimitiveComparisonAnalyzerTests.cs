namespace Umbrella.Analyzers.Test;

public class UA002_PrimitiveComparisonAnalyzerTests : AnalyzerTestBase<PrimitiveComparisonAnalyzer>
{
	[Fact]
	public async Task PrimitiveEqualityComparison_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int x) { if(x == 5) { } } }";
		var expected = Diagnostic(PrimitiveComparisonAnalyzer.Rule, 1, 52);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PrimitiveInequalityComparison_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int x) { if(x != 5) { } } }";
		var expected = Diagnostic(PrimitiveComparisonAnalyzer.Rule, 1, 52);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PatternMatchingPrimitiveCheck_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int x) { if(x is 5) { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}