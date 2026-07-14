namespace Umbrella.Analyzers.Test;

public class UA002_PrimitiveComparisonAnalyzerTests : AnalyzerTestBase<PrimitiveComparisonAnalyzer>
{
	private static readonly Microsoft.CodeAnalysis.MetadataReference _expressionReference =
		Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression<>).Assembly.Location);

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

	[Fact]
	public async Task VariableEqualityAndInequalityComparisons_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int x, int y) { if(x == y) { } if(x != y) { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ConstVariableComparison_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int x) { const int expected = 5; if(x == expected) { } } }";
		var expected = Diagnostic(PrimitiveComparisonAnalyzer.Rule, 1, 76);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PrimitiveComparisonInExpressionTree_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Linq.Expressions;

			public class TestClass
			{
				public Expression<Func<int, bool>> M() => value => value == 5;
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_expressionReference]);
	}
}