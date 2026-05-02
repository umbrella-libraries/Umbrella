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

	[Fact]
	public async Task ExpressionBodiedPublicMethod_ShouldTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public string M() => ""test""; }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 40, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task ExpressionBodiedPublicMethodWithILogger_ShouldTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public string M() => ""test""; }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 165, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndExceptionFilterLogging_ShouldNotTriggerDiagnostic()
	{
		// The Umbrella pattern calls the logger inside the when(...) filter expression rather than
		// the catch block body. catch (Exception exc) when (_logger.IsEnabled(...)) { throw; }
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception exc) when (_logger.IsEnabled(LogLevel.Error)) { throw; } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndBinaryExceptionFilterLogging_ShouldNotTriggerDiagnostic()
	{
		// The Umbrella pattern combines a type guard and a logger call with && in the when(...) filter.
		// catch (Exception exc) when (exc is not NavigationException && _logger.IsEnabled(...))
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception exc) when (exc is not InvalidOperationException && _logger.IsEnabled(LogLevel.Error)) { throw; } } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}