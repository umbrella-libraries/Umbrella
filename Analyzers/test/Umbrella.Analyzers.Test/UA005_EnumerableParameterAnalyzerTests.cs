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
}