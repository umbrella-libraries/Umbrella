namespace Umbrella.Analyzers.Test;

public class UA004_AsyncMethodThrowIfCancellationAnalyzerTests : AnalyzerTestBase<AsyncMethodThrowIfCancellationAnalyzer>
{
	[Fact]
	public async Task AsyncMethodWithoutThrowIfCancellationRequested_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public async Task M(CancellationToken cancellationToken) { await Task.Delay(1000); } }";
		var expected = Diagnostic(AsyncMethodThrowIfCancellationAnalyzer.Rule, 1, 44);
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task AsyncMethodWithThrowIfCancellationRequested_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public async Task M(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); await Task.Delay(1000); } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}