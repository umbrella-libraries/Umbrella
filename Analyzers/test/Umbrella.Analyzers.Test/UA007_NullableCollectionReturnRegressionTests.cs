namespace Umbrella.Analyzers.Test;

public class UA007_NullableCollectionReturnRegressionTests : AnalyzerTestBase<NonNullableCollectionReturnTypeAnalyzer>
{
	[Theory]
	[InlineData("string")]
	[InlineData("string?")]
	public async Task StringReturn_ShouldNotTriggerDiagnostic(string returnType)
	{
		string source = $"public abstract class TestClass {{ public abstract {returnType} M(); }}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyAccessors_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; public class TestClass { public string? Name { get; set; } public List<int>? Items { get; set; } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RecursivelyWrappedNullableCollectionReturn_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; using System.Threading.Tasks; public sealed record Result<T>(T Value); public abstract class TestClass { public abstract Task<Result<IReadOnlyCollection<int>?>> M(); }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "M", "System.Collections.Generic.IReadOnlyCollection<int>?"));
	}

	[Fact]
	public async Task NullableWrapperWithNonNullableCollectionPayload_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; public sealed record Result<T>(T Value); public abstract class TestClass { public abstract Result<IReadOnlyCollection<int>>? M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DelegateGenericArguments_ShouldNotBeInspected()
	{
		const string source = "using System; using System.Collections.Generic; public abstract class TestClass { public abstract Func<IReadOnlyCollection<int>?> M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CollectionElementNullability_ShouldNotBeInspected()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract IReadOnlyCollection<List<int>?> M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MultipleNullableTupleCollectionPayloads_ShouldTriggerMultipleDiagnostics()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract (IReadOnlyCollection<int>?, IReadOnlyList<string>?) M(); }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "M", "System.Collections.Generic.IReadOnlyCollection<int>?"),
			ExpectedAt(source, "M", "System.Collections.Generic.IReadOnlyList<string>?"));
	}

	[Fact]
	public async Task NullableAsyncEnumerableReturn_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract IAsyncEnumerable<int>? M(); }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "M", "System.Collections.Generic.IAsyncEnumerable<int>?"));
	}

	[Fact]
	public async Task IneligibleMethodSignatures_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Collections.Generic;

			namespace Microsoft.JSInterop
			{
				public sealed class JSInvokableAttribute : Attribute
				{
				}
			}

			namespace Xunit
			{
				public sealed class FactAttribute : Attribute
				{
				}
			}

			namespace Microsoft.AspNetCore.Components
			{
				public abstract class ComponentBase
				{
				}
			}

			public partial class TestClass
			{
				private List<int>? Private() => null;
				protected List<int>? Protected() => null;
				internal List<int>? Internal() => null;

				public partial List<int>? Partial();
				public partial List<int>? Partial() => null;

				public static extern List<int>? External();

				[Microsoft.JSInterop.JSInvokable]
				public List<int>? JSInvoked() => null;

				[Xunit.Fact]
				public List<int>? TestEntryPoint() => null;
			}

			public abstract class BaseClass
			{
				public abstract IEnumerable<int> Override();
			}

			public sealed class DerivedClass : BaseClass
			{
				public override List<int>? Override() => null;
			}

			public class Component : Microsoft.AspNetCore.Components.ComponentBase
			{
				public List<int>? Callback() => null;
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicVirtualAndAbstractMethods_ShouldTriggerDiagnostics()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract List<int>? AbstractMethod(); public virtual List<int>? VirtualMethod() => null; }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "AbstractMethod", "System.Collections.Generic.List<int>?"),
			ExpectedAt(source, "VirtualMethod", "System.Collections.Generic.List<int>?"));
	}

	private static ExpectedDiagnostic ExpectedAt(string source, string methodName, string displayType)
	{
		int index = source.IndexOf(methodName + "(", StringComparison.Ordinal);
		int line = source[..index].Count(static character => character == '\n') + 1;
		int lastLineBreak = source.LastIndexOf('\n', index);
		int column = index - lastLineBreak;

		return Diagnostic(NonNullableCollectionReturnTypeAnalyzer.Rule, line, column, methodName, displayType);
	}
}
