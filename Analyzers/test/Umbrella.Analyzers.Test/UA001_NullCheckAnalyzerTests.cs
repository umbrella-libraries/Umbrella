namespace Umbrella.Analyzers.Test;

public class UA001_NullCheckAnalyzerTests : AnalyzerTestBase<NullCheckAnalyzer>
{
	private static readonly Microsoft.CodeAnalysis.MetadataReference _expressionReference =
		Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression<>).Assembly.Location);

	[Fact]
	public async Task NullEqualityComparison_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(object o) { if(o == null) { } } }";
		var expected = Diagnostic(NullCheckAnalyzer.Rule, 1, 55);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NullInequalityComparison_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(object o) { if(o != null) { } } }";
		var expected = Diagnostic(NullCheckAnalyzer.Rule, 1, 55);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PatternMatchingNullCheck_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(object o) { if(o is null) { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NullComparisonInDelegate_ShouldTriggerDiagnostic()
	{
		const string source = """
			using System;

			public class TestClass
			{
				public Func<object, bool> M() => value => value == null;
			}
			""";
		var expected = Diagnostic(NullCheckAnalyzer.Rule, 5, 44);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NullComparisonInExpressionTree_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Linq.Expressions;

			public class TestClass
			{
				public Expression<Func<object, bool>> M() => value => value == null;
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_expressionReference]);
	}

	[Fact]
	public async Task NullComparisonInNestedLambdaWithinExpressionTree_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Collections.Generic;
			using System.Linq;
			using System.Linq.Expressions;

			public class TestClass
			{
				public Expression<Func<IEnumerable<object>, bool>> M() => values => values.Any(value => value == null);
			}
			""";

		await VerifyNoDiagnosticsAsync(source, [_expressionReference]);
	}
}