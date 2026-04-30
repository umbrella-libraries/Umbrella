namespace Umbrella.Analyzers.Test;

public class UA005_EnumerableParameterAnalyzerTests : AnalyzerTestBase<EnumerableParameterAnalyzer>
{
	[Fact]
	public async Task ConcreteListParameter_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public void M(List<int> items) { } }";
		var expected = Diagnostic(EnumerableParameterAnalyzer.Rule, 1, 84, "items", "System.Collections.Generic.List<int>");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task IEnumerableParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public void M(IEnumerable<int> items) { } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task IReadOnlyCollectionParameter_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public void M(IReadOnlyCollection<int> items) { } }";
		var expected = Diagnostic(EnumerableParameterAnalyzer.Rule, 1, 99, "items", "System.Collections.Generic.IReadOnlyCollection<int>");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NonCollectionParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M(int count) { } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}
