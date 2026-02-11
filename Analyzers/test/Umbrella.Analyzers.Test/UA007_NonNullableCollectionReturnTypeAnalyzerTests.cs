namespace Umbrella.Analyzers.Test;

public class UA007_NonNullableCollectionReturnTypeAnalyzerTests : AnalyzerTestBase<NonNullableCollectionReturnTypeAnalyzer>
{
	[Fact]
	public async Task NullableCollectionReturnType_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public IEnumerable<int>? M() { return null; } }";
		var expected = Diagnostic(NonNullableCollectionReturnTypeAnalyzer.Rule, 1, 85, "M", "System.Collections.Generic.IEnumerable<int>?");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NonNullableCollectionReturnType_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public IEnumerable<int> M() { return new List<int>(); } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NullableTupleWithCollection_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public (IEnumerable<int>?, int) M() { return (null, 0); } }";
		var expected = Diagnostic(NonNullableCollectionReturnTypeAnalyzer.Rule, 1, 92, "M", "System.Collections.Generic.IEnumerable<int>?");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NonNullableTupleWithCollection_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Collections.Generic; public class TestClass { public (IEnumerable<int>, int) M() { return (new List<int>(), 0); } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NullableTaskWithCollection_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Threading.Tasks; using System.Collections.Generic; public class TestClass { public Task<IEnumerable<int>?> M() { return null; } }";
		var expected = Diagnostic(NonNullableCollectionReturnTypeAnalyzer.Rule, 1, 121, "M", "System.Collections.Generic.IEnumerable<int>?");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NonNullableTaskWithCollection_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Threading.Tasks; using System.Collections.Generic; public class TestClass { public Task<IEnumerable<int>> M() { return Task.FromResult<IEnumerable<int>>(new List<int>()); } }";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NullableValueTaskWithCollection_ShouldTriggerDiagnostic()
	{
		const string source = @"using System.Threading.Tasks; using System.Collections.Generic; public class TestClass { public ValueTask<IEnumerable<int>?> M() { return default; } }";
		var expected = Diagnostic(NonNullableCollectionReturnTypeAnalyzer.Rule, 1, 126, "M", "System.Collections.Generic.IEnumerable<int>?");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task NonNullableValueTaskWithCollection_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System.Threading.Tasks; using System.Collections.Generic; public class TestClass { public ValueTask<IEnumerable<int>> M() { return new ValueTask<IEnumerable<int>>(new List<int>()); } }";
		await VerifyNoDiagnosticsAsync(source);
	}
}