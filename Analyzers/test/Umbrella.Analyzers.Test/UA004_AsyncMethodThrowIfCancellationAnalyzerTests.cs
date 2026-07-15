namespace Umbrella.Analyzers.Test;

public class UA004_AsyncMethodThrowIfCancellationAnalyzerTests : AnalyzerTestBase<AsyncMethodThrowIfCancellationAnalyzer>
{
	[Fact]
	public async Task CanonicalCancellationTokenWithoutThrow_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) { await Task.Delay(1000, cancellationToken); } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task CanonicalCancellationTokenWithThrowFirst_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); await Task.Delay(1000, cancellationToken); } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Theory]
	[InlineData("System.Threading.CancellationToken", "")]
	[InlineData("CT", "using CT = System.Threading.CancellationToken;")]
	public async Task SemanticallyResolvedCancellationTokenWithoutThrow_ShouldTriggerDiagnostic(string parameterType, string alias)
	{
		string source = $"{alias} using System.Threading.Tasks; public class TestClass {{ public async Task M({parameterType} cancellationToken = default) {{ await Task.Delay(1000, cancellationToken); }} }}";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Theory]
	[InlineData("CancellationToken token = default")]
	[InlineData("CancellationToken cancellationToken")]
	[InlineData("CancellationToken cancellationToken = default(CancellationToken)")]
	[InlineData("CancellationToken cancellationToken = new()")]
	public async Task NonCanonicalCancellationTokenParameter_ShouldNotTriggerDiagnostic(string parameter)
	{
		string source = $"using System.Threading; using System.Threading.Tasks; public class TestClass {{ public async Task M({parameter}) {{ await Task.Yield(); }} }}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedCancellationTokenType_ShouldNotTriggerDiagnostic()
	{
		const string source = "using System.Threading.Tasks; namespace Example { public struct CancellationToken { public void ThrowIfCancellationRequested() { } } public class TestClass { public async Task M(CancellationToken cancellationToken = default) { await Task.Yield(); } } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ThrowCalledOnDifferentReceiver_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) { CancellationToken.None.ThrowIfCancellationRequested(); await Task.Yield(); } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task ThrowAfterAnotherStatement_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task ExpressionBodiedMethod_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) => await Task.Delay(1000, cancellationToken); }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task EmptyMethodBody_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public async Task M(CancellationToken cancellationToken = default) { } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "M"));
	}

	[Fact]
	public async Task NonPublicOverrideInterfaceAndPartialMethods_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			public abstract class BaseClass
			{
				public abstract Task OverrideAsync(CancellationToken cancellationToken = default);
			}

			public interface IWorker
			{
				Task InterfaceAsync(CancellationToken cancellationToken = default);
			}

			public partial class TestClass : BaseClass, IWorker
			{
				private async Task PrivateAsync(CancellationToken cancellationToken = default) => await Task.Yield();
				protected async Task ProtectedAsync(CancellationToken cancellationToken = default) => await Task.Yield();
				internal async Task InternalAsync(CancellationToken cancellationToken = default) => await Task.Yield();
				public override async Task OverrideAsync(CancellationToken cancellationToken = default) => await Task.Yield();
				public async Task InterfaceAsync(CancellationToken cancellationToken = default) => await Task.Yield();

				public partial Task PartialAsync(CancellationToken cancellationToken = default);
				public async partial Task PartialAsync(CancellationToken cancellationToken = default) => await Task.Yield();
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DirectAndIndirectBlazorComponentMethods_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Microsoft.AspNetCore.Components
			{
				public abstract class ComponentBase
				{
				}
			}

			public class DirectComponent : Microsoft.AspNetCore.Components.ComponentBase
			{
				public async Task ButtonClickAsync(CancellationToken cancellationToken = default) => await Task.Yield();
				public async Task SubmitFormAsync(CancellationToken cancellationToken = default) => await Task.Yield();
			}

			public abstract class IntermediateComponent : Microsoft.AspNetCore.Components.ComponentBase
			{
			}

			public class IndirectComponent : IntermediateComponent
			{
				public async Task UploadCallbackAsync(CancellationToken cancellationToken = default) => await Task.Yield();
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task JSInvokableAndTestEntryPointMethods_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;

			namespace Microsoft.JSInterop
			{
				public sealed class JSInvokableAttribute : Attribute
				{
				}
			}

			namespace Xunit
			{
				public class FactAttribute : Attribute
				{
				}
			}

			namespace NUnit.Framework
			{
				public class TestAttribute : Attribute
				{
				}
			}

			namespace Microsoft.VisualStudio.TestTools.UnitTesting
			{
				public class TestMethodAttribute : Attribute
				{
				}
			}

			public class TestClass
			{
				[Microsoft.JSInterop.JSInvokable]
				public async Task JSAsync(CancellationToken cancellationToken = default) => await Task.Yield();

				[Xunit.Fact]
				public async Task XunitAsync(CancellationToken cancellationToken = default) => await Task.Yield();

				[NUnit.Framework.Test]
				public async Task NUnitAsync(CancellationToken cancellationToken = default) => await Task.Yield();

				[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
				public async Task MSTestAsync(CancellationToken cancellationToken = default) => await Task.Yield();
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task GeneratedMethod_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			// <auto-generated/>
			using System.Threading;
			using System.Threading.Tasks;

			public class TestClass
			{
				public async Task GeneratedAsync(CancellationToken cancellationToken = default) => await Task.Yield();
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PublicVirtualMethod_ShouldTriggerDiagnostic()
	{
		const string source = "using System.Threading; using System.Threading.Tasks; public class TestClass { public virtual async Task RunAsync(CancellationToken cancellationToken = default) { await Task.Yield(); } }";

		await VerifyAnalyzerAsync(source, ExpectedAt(source, "RunAsync"));
	}

	private static ExpectedDiagnostic ExpectedAt(string source, string methodName)
	{
		int column = source.IndexOf(methodName + "(", StringComparison.Ordinal) + 1;
		return Diagnostic(AsyncMethodThrowIfCancellationAnalyzer.Rule, 1, column, methodName);
	}
}
