namespace Umbrella.Analyzers.Test;

public class UA003_AsyncMethodCancellationAnalyzerTests : AnalyzerTestBase<AsyncMethodCancellationAnalyzer>
{
	[Fact]
	public async Task AsyncMethodWithoutCancellationToken_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public async Task M() { await Task.Delay(1000); } }";
		var expected = Diagnostic(AsyncMethodCancellationAnalyzer.Rule, 1, 44);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task AsyncMethodWithCancellationToken_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public async Task M(CancellationToken cancellationToken) { await Task.Delay(1000, cancellationToken); } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}