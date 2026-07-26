namespace Umbrella.Analyzers.Test;

public class UA009_ParameterValidationPlacementAnalyzerTests : AnalyzerTestBase<ParameterValidationPlacementAnalyzer>
{
	private static readonly Microsoft.CodeAnalysis.MetadataReference _guardReference =
		Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(CommunityToolkit.Diagnostics.Guard).Assembly.Location);

	[Fact]
	public async Task GuardCall_AfterTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using CommunityToolkit.Diagnostics; class C { void M(string x) { try { } catch { } Guard.IsNotNull(x); } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 84, "M");
		await VerifyAnalyzerAsync(source, [_guardReference], expected);
	}

	[Fact]
	public async Task GuardCall_BeforeTry_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using CommunityToolkit.Diagnostics; class C { void M(string x) { Guard.IsNotNull(x); try { } catch { } } }";
		await VerifyNoDiagnosticsAsync(source, [_guardReference]);
	}

	[Fact]
	public async Task ThrowHelper_AfterTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { try { } catch { } ArgumentNullException.ThrowIfNull(x); } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 62, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task ThrowHelper_BeforeTry_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { ArgumentNullException.ThrowIfNull(x); try { } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DirectThrow_AfterTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { try { } catch { } throw new ArgumentNullException(nameof(x)); } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 62, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task DirectThrow_BeforeTry_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { if (x == null) throw new ArgumentNullException(nameof(x)); try { } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	// New tests enforcing no validation inside try blocks

	[Fact]
	public async Task GuardCall_InsideTry_ShouldTriggerDiagnostic()
	{
		// Guard call occurs inside the try block -> should trigger
		const string source = @"using CommunityToolkit.Diagnostics; class C { void M(string x) { try { Guard.IsNotNull(x); } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 72, "M");
		await VerifyAnalyzerAsync(source, [_guardReference], expected);
	}

	[Fact]
	public async Task ThrowHelper_InsideTry_ShouldTriggerDiagnostic()
	{
		// Throw helper inside try block -> should trigger
		const string source = @"using System; class C { void M(string x) { try { ArgumentNullException.ThrowIfNull(x); } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 50, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task DirectThrow_InsideTry_ShouldTriggerDiagnostic()
	{
		// Direct throw inside try block -> should trigger
		const string source = @"using System; class C { void M(string x) { try { throw new ArgumentNullException(nameof(x)); } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 50, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	// Additional Guard test: nested try with Guard inside inner try
	[Fact]
	public async Task GuardCall_InsideNestedTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using CommunityToolkit.Diagnostics; class C { void M(string x) { try { try { Guard.IsNotNull(x); } catch { } } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 78, "M");
		await VerifyAnalyzerAsync(source, [_guardReference], expected);
	}

	// Additional nested tests for Throw Helper and Direct Throw inside inner try
	[Fact]
	public async Task ThrowHelper_InsideNestedTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { try { try { ArgumentNullException.ThrowIfNull(x); } catch { } } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 56, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task DirectThrow_InsideNestedTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using System; class C { void M(string x) { try { try { throw new ArgumentNullException(nameof(x)); } catch { } } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 56, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task CancellationCheck_InsideTry_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Threading; class C { void M(CancellationToken cancellationToken) { try { cancellationToken.ThrowIfCancellationRequested(); } catch { } } }";
		var expected = Diagnostic(ParameterValidationPlacementAnalyzer.Rule, 1, 87, "M");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task CancellationCheck_BeforeTry_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Threading; class C { void M(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); try { } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UserDefinedGuard_ShouldNotTriggerDiagnostic()
	{
		const string source = @"class Guard { public static void IsNotNull(object value) { } } class C { void M(string x) { try { Guard.IsNotNull(x); } catch { } } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}
