namespace Umbrella.Analyzers.Test;

public class UA008_PublicMethodTryCatchAnalyzerTests : AnalyzerTestBase<PublicMethodTryCatchAnalyzer>
{
	private static readonly Microsoft.CodeAnalysis.MetadataReference _loggingReference =
		Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

	[Fact]
	public async Task PublicMethodWithoutILogger_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public void M() { int x = 0; } }";
		await VerifyNoDiagnosticsAsync(source);
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
		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndLogging_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception ex) { _logger.LogError(ex, ""An error occurred.""); } } }";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task PrivateMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } private void M() { int x = 0; } }";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task InternalMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } internal void M() { int x = 0; } }";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
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
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (ArgumentException ex) { _logger.LogError(ex, ""Argument error.""); } catch (Exception ex) { _logger.LogError(ex, ""An error occurred.""); } } }";
		await VerifyNoDiagnosticsAsync(source, [_loggingReference]);
	}

	[Fact]
	public async Task PublicMethodInClassWithoutILogger_ShouldNotRequireLogging()
	{
		const string source = @"public class TestClass { public void M() { try { int x = 0; } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ExpressionBodiedPublicMethodWithoutILogger_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class TestClass { public string M() => ""test""; }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ExpressionBodiedPublicMethodWithILogger_ShouldTriggerDiagnostic()
	{
		const string source = @"using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public string M() => ""test""; }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 165, "M");
		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndIsEnabledFilter_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception exc) when (_logger.IsEnabled(LogLevel.Error)) { throw; } } }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 177, "M");
		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}

	[Fact]
	public async Task PublicMethodWithILoggerAndBinaryIsEnabledFilter_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; using Microsoft.Extensions.Logging; public class TestClass { private readonly ILogger _logger; public TestClass(ILogger logger) { _logger = logger; } public void M() { try { int x = 0; } catch (Exception exc) when (exc is not InvalidOperationException && _logger.IsEnabled(LogLevel.Error)) { throw; } } }";
		var expected = Diagnostic(PublicMethodTryCatchAnalyzer.Rule, 1, 177, "M");
		await VerifyAnalyzerAsync(source, [_loggingReference], expected);
	}
}
