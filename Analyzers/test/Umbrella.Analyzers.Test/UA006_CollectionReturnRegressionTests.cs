namespace Umbrella.Analyzers.Test;

public class UA006_CollectionReturnRegressionTests : AnalyzerTestBase<ReadOnlyCollectionReturnTypeAnalyzer>
{
	[Theory]
	[InlineData("IEnumerable<int>")]
	[InlineData("IReadOnlyCollection<int>")]
	[InlineData("IReadOnlyList<int>")]
	[InlineData("IReadOnlySet<int>")]
	[InlineData("IReadOnlyDictionary<int, string>")]
	[InlineData("IAsyncEnumerable<int>")]
	public async Task ReadOnlyCollectionContractReturn_ShouldNotTriggerDiagnostic(string returnType)
	{
		string source = $"using System.Collections.Generic; public abstract class TestClass {{ public abstract {returnType} M(); }}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task KnownReadOnlyConcreteCollectionReturns_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Collections.ObjectModel;

			namespace System.Collections.Immutable
			{
				public class ImmutableList<T> : IEnumerable<T>
				{
				}
			}

			namespace System.Collections.Frozen
			{
				public class FrozenSet<T> : IEnumerable<T>
				{
				}
			}

			public abstract class TestClass
			{
				public abstract ReadOnlyCollection<int> ReadOnly();
				public abstract System.Collections.Immutable.ImmutableList<int> Immutable();
				public abstract System.Collections.Frozen.FrozenSet<int> Frozen();
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutableArrayReturn_ShouldTriggerDiagnostic()
	{
		const string source = "public abstract class TestClass { public abstract int[] M(); }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M", "int[]"));
	}

	[Fact]
	public async Task DirectAndWrappedByteArrayReturns_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Threading.Tasks; public abstract class TestClass { public abstract byte[] Direct(); public abstract Task<byte[]> Wrapped(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task JaggedAndMultidimensionalByteArrayReturns_ShouldTriggerDiagnostics()
	{
		const string source = "public abstract class TestClass { public abstract byte[][] Jagged(); public abstract byte[,] Multidimensional(); }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "Jagged", "byte[][]"),
			ExpectedAt(source, "Multidimensional", "byte[*,*]"));
	}

	[Fact]
	public async Task DirectWrappedAndExtensionBlockServiceCollectionReturns_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Microsoft.Extensions.DependencyInjection
			{
				public sealed class ServiceDescriptor
				{
				}

				public interface IServiceCollection : System.Collections.Generic.IList<ServiceDescriptor>
				{
				}
			}

			public abstract class TestClass
			{
				public abstract Microsoft.Extensions.DependencyInjection.IServiceCollection Direct();
				public abstract Task<Microsoft.Extensions.DependencyInjection.IServiceCollection> Wrapped();
			}

			public static class ServiceCollectionExtensions
			{
				extension(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
				{
					public Microsoft.Extensions.DependencyInjection.IServiceCollection AddFeature() => services;
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedServiceCollectionReturn_ShouldTriggerDiagnostic()
	{
		const string source = """
			namespace Custom
			{
				public interface IServiceCollection : System.Collections.Generic.IList<int>
				{
				}
			}

			public abstract class TestClass
			{
				public abstract Custom.IServiceCollection M();
			}
			""";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M", "Custom.IServiceCollection"));
	}

	[Fact]
	public async Task RecursivelyWrappedMutableCollectionReturn_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; using System.Threading.Tasks; public sealed record Result<T>(T Value); public abstract class TestClass { public abstract Task<Result<List<int>>> M(); }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M", "System.Collections.Generic.List<int>"));
	}

	[Fact]
	public async Task RecursivelyWrappedReadOnlyCollectionReturn_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; using System.Threading.Tasks; public sealed record Result<T>(T Value); public abstract class TestClass { public abstract Task<Result<IReadOnlyList<int>>> M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DelegateGenericArguments_ShouldNotBeInspected()
	{
		const string source = "using System; using System.Collections.Generic; public abstract class TestClass { public abstract Func<List<int>> M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CollectionElementTypes_ShouldNotBeInspectedForDeepImmutability()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract IReadOnlyCollection<List<int>> M(); }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task FilterExpressionArrayReturn_ShouldTriggerDiagnostic()
	{
		const string source = "namespace Umbrella.Utilities.Data.Filtering { public readonly record struct FilterExpression<T>; } public abstract class TestClass { public abstract Umbrella.Utilities.Data.Filtering.FilterExpression<int>[] M(); }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "M", "Umbrella.Utilities.Data.Filtering.FilterExpression<int>[]"));
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
				public List<int> Items { get; set; } = [];

				private List<int> Private() => [];
				protected List<int> Protected() => [];
				internal List<int> Internal() => [];

				public partial List<int> Partial();
				public partial List<int> Partial() => [];

				public static extern List<int> External();

				[Microsoft.JSInterop.JSInvokable]
				public List<int> JSInvoked() => [];

				[Xunit.Fact]
				public List<int> TestEntryPoint() => [];
			}

			public abstract class BaseClass
			{
				public abstract IEnumerable<int> Override();
			}

			public sealed class DerivedClass : BaseClass
			{
				public override List<int> Override() => [];
			}

			public class Component : Microsoft.AspNetCore.Components.ComponentBase
			{
				public List<int> Callback() => [];
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicVirtualAndAbstractMethods_ShouldTriggerDiagnostics()
	{
		const string source = "using System.Collections.Generic; public abstract class TestClass { public abstract List<int> AbstractMethod(); public virtual List<int> VirtualMethod() => []; }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "AbstractMethod", "System.Collections.Generic.List<int>"),
			ExpectedAt(source, "VirtualMethod", "System.Collections.Generic.List<int>"));
	}

	private static ExpectedDiagnostic ExpectedAt(string source, string methodName, string displayType)
	{
		int index = source.IndexOf(methodName + "(", StringComparison.Ordinal);
		int line = source[..index].Count(static character => character == '\n') + 1;
		int lastLineBreak = source.LastIndexOf('\n', index);
		int column = index - lastLineBreak;

		return Diagnostic(ReadOnlyCollectionReturnTypeAnalyzer.Rule, line, column, methodName, displayType);
	}
}
