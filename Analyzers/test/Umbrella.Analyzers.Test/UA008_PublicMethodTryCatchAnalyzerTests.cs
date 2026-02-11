namespace Umbrella.Analyzers.Test;

public class UA008_PublicMethodTryCatchAnalyzerTests : AnalyzerTestBase<PublicMethodTryCatchAnalyzer>
{
	[Fact]
	public async Task PublicMethodWithoutTryCatch_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M() { int x = 0; } }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 38, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PublicMethodWithTryCatch_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M() { try { int x = 0; } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndNoLogging_ShouldTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch { } } }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 163, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndLogging_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception ex) { _logger.LogError(ex, ""An error occurred.""); } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PrivateMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { private void M() { int x = 0; } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InternalMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { internal void M() { int x = 0; } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicMethodWithMultipleCatchBlocksAndNoLogging_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System; public class TestClass { public void M() { try { int x = 0; } catch (ArgumentException) { } catch (Exception) { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicMethodWithMultipleCatchBlocksAndLogging_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (ArgumentException ex) { _logger.LogWarning(ex, ""Argument error.""); } catch (Exception ex) { _logger.LogError(ex, ""An error occurred.""); } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicMethodInClassWithoutILogger_ShouldNotRequireLogging()
	{
		const string source = @"public class TestClass { public void M() { try { int x = 0; } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}