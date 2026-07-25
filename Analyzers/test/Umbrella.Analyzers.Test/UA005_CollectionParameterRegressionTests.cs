namespace Umbrella.Analyzers.Test;

public class UA005_CollectionParameterRegressionTests : AnalyzerTestBase<EnumerableParameterAnalyzer>
{
	[Theory]
	[InlineData("List<int>", "System.Collections.Generic.List<int>")]
	[InlineData("ICollection<int>", "System.Collections.Generic.ICollection<int>")]
	[InlineData("IList<int>", "System.Collections.Generic.IList<int>")]
	[InlineData("ISet<int>", "System.Collections.Generic.ISet<int>")]
	[InlineData("IDictionary<int, string>", "System.Collections.Generic.IDictionary<int, string>")]
	[InlineData("Dictionary<int, string>", "System.Collections.Generic.Dictionary<int, string>")]
	[InlineData("System.Collections.IList", "System.Collections.IList")]
	public async Task MutableCollectionParameter_ShouldTriggerDiagnostic(string parameterType, string displayType)
	{
		string source = $"using System.Collections.Generic; public class TestClass {{ public void M({parameterType} items) {{ }} }}";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "items", displayType));
	}

	[Theory]
	[InlineData("IEnumerable<int>")]
	[InlineData("IReadOnlyCollection<int>")]
	[InlineData("IReadOnlyList<int>")]
	[InlineData("IReadOnlySet<int>")]
	[InlineData("IReadOnlyDictionary<int, string>")]
	[InlineData("System.Collections.IEnumerable")]
	public async Task ReadOnlyCollectionInterfaceParameter_ShouldNotTriggerDiagnostic(string parameterType)
	{
		string source = $"using System.Collections.Generic; public class TestClass {{ public void M({parameterType} items) {{ }} }}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomReadOnlyInterfaceParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; public interface IReadOnlyItems<T> : IEnumerable<T> { } public class TestClass { public void M(IReadOnlyItems<int> items) { } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task KnownReadOnlyConcreteCollectionParameters_ShouldNotTriggerDiagnostic()
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

			public class TestClass
			{
				public void M(
					ReadOnlyCollection<int> readOnly,
					System.Collections.Immutable.ImmutableList<int> immutable,
					System.Collections.Frozen.FrozenSet<int> frozen)
				{
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomConcreteCollectionParameter_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Collections.Generic; public class CustomCollection<T> : IEnumerable<T> { } public class TestClass { public void M(CustomCollection<int> items) { } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "items", "CustomCollection<int>"));
	}

	[Fact]
	public async Task ArrayParameter_ShouldTriggerDiagnostic()
	{
		const string source = "public class TestClass { public void M(int[] items) { } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "items", "int[]"));
	}

	[Fact]
	public async Task ByteArrayParameters_ShouldNotTriggerDiagnostic()
	{
		const string source = "public class TestClass { public void M(byte[] bytes, byte[]? nullableBytes) { } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task JaggedAndMultidimensionalByteArrayParameters_ShouldTriggerDiagnostics()
	{
		const string source = "public class TestClass { public void M(byte[][] jaggedBytes, byte[,] multidimensionalBytes) { } }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "jaggedBytes", "byte[][]"),
			ExpectedAt(source, "multidimensionalBytes", "byte[*,*]"));
	}

	[Fact]
	public async Task ServiceCollectionParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Microsoft.Extensions.DependencyInjection
			{
				public sealed class ServiceDescriptor
				{
				}

				public interface IServiceCollection : System.Collections.Generic.IList<ServiceDescriptor>
				{
				}
			}

			public static class ServiceCollectionExtensions
			{
				public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddFeature(
					this Microsoft.Extensions.DependencyInjection.IServiceCollection services) => services;
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedServiceCollectionParameter_ShouldTriggerDiagnostic()
	{
		const string source = """
			namespace Custom
			{
				public interface IServiceCollection : System.Collections.Generic.IList<int>
				{
				}
			}

			public class TestClass
			{
				public void M(Custom.IServiceCollection services)
				{
				}
			}
			""";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "services", "Custom.IServiceCollection"));
	}

	[Fact]
	public async Task FilterAndSortExpressionArrays_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			namespace Umbrella.Utilities.Data.Filtering
			{
				public readonly record struct FilterExpression<T>;
			}

			namespace Umbrella.Utilities.Data.Sorting
			{
				public readonly record struct SortExpression<T>;
			}

			public class TestClass
			{
				public void M(
					Umbrella.Utilities.Data.Filtering.FilterExpression<int>[]? filters,
					Umbrella.Utilities.Data.Sorting.SortExpression<int>[] sorters)
				{
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UserDefinedExpressionArray_ShouldTriggerDiagnostic()
	{
		const string source = "namespace Custom { public readonly record struct FilterExpression<T>; } public class TestClass { public void M(Custom.FilterExpression<int>[] filters) { } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "filters", "Custom.FilterExpression<int>[]"));
	}

	[Fact]
	public async Task JaggedAndNullableElementExpressionArrays_ShouldTriggerDiagnostics()
	{
		const string source = "namespace Umbrella.Utilities.Data.Filtering { public readonly record struct FilterExpression<T>; } public class TestClass { public void M(Umbrella.Utilities.Data.Filtering.FilterExpression<int>[][] filters, Umbrella.Utilities.Data.Filtering.FilterExpression<int>?[] nullableFilters) { } }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "filters", "Umbrella.Utilities.Data.Filtering.FilterExpression<int>[][]"),
			ExpectedAt(source, "nullableFilters", "Umbrella.Utilities.Data.Filtering.FilterExpression<int>?[]"));
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
				public TestClass(List<int> items)
				{
				}

				public List<int> Items { get; set; } = [];

				private void Private(List<int> items)
				{
				}

				protected void Protected(List<int> items)
				{
				}

				internal void Internal(List<int> items)
				{
				}

				public partial void Partial(List<int> items);

				public partial void Partial(List<int> items)
				{
				}

				public static extern void External(List<int> items);

				[Microsoft.JSInterop.JSInvokable]
				public void JSInvoked(List<int> items)
				{
				}

				[Xunit.Fact]
				public void TestEntryPoint(List<int> items)
				{
				}
			}

			public class Component : Microsoft.AspNetCore.Components.ComponentBase
			{
				public void Callback(List<int> items)
				{
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicVirtualAbstractAndInterfaceDeclarations_ShouldTriggerDiagnostics()
	{
		const string source = "using System.Collections.Generic; public interface IContract { void InterfaceMethod(List<int> interfaceItems); } public abstract class TestClass { public abstract void AbstractMethod(List<int> abstractItems); public virtual void VirtualMethod(List<int> virtualItems) { } }";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "interfaceItems", "System.Collections.Generic.List<int>"),
			ExpectedAt(source, "abstractItems", "System.Collections.Generic.List<int>"),
			ExpectedAt(source, "virtualItems", "System.Collections.Generic.List<int>"));
	}

	[Fact]
	public async Task GeneratedMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			// <auto-generated/>
			using System.Collections.Generic;

			public class TestClass
			{
				public void M(List<int> items)
				{
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	private static ExpectedDiagnostic ExpectedAt(string source, string parameterName, string displayType)
	{
		int index = source.IndexOf(parameterName, StringComparison.Ordinal);
		int line = source[..index].Count(static character => character == '\n') + 1;
		int lastLineBreak = source.LastIndexOf('\n', index);
		int column = index - lastLineBreak;

		return Diagnostic(EnumerableParameterAnalyzer.Rule, line, column, parameterName, displayType);
	}
}
