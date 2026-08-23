namespace Umbrella.Analyzers.Test;

public class UA017_UmbrellaApiStandardsAnalyzerTests : AnalyzerTestBase<UmbrellaApiStandardsAnalyzer>
{
	private const string ControllerStubs = """
		using System;
		namespace Microsoft.AspNetCore.Mvc
		{
		    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
		    public class ProducesResponseTypeAttribute : Attribute
		    {
		        public ProducesResponseTypeAttribute(int statusCode) { }
		    }

		    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
		    public class ProducesResponseTypeAttribute<T> : Attribute
		    {
		        public ProducesResponseTypeAttribute(int statusCode) { }
		    }

		    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
		    public sealed class NonActionAttribute : Attribute { }

		    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
		    public sealed class NonControllerAttribute : Attribute { }
		}

		namespace Umbrella.AspNetCore.WebUtilities.Mvc
		{
		    public abstract class UmbrellaApiController { }

		    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
		    public sealed class UmbrellaProducesResponseTypeAttribute : Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute
		    {
		        public UmbrellaProducesResponseTypeAttribute(int statusCode) : base(statusCode) { }
		    }

		    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
		    public sealed class UmbrellaProducesResponseTypeAttribute<T> : Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute<T>
		    {
		        public UmbrellaProducesResponseTypeAttribute(int statusCode) : base(statusCode) { }
		    }
		}

		""";

	[Fact]
	public void Rule_ShouldHaveErrorSeverity()
	{
		Assert.Equal(
			Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
			UmbrellaApiStandardsAnalyzer.UseUmbrellaProducesResponseTypeRule.DefaultSeverity);
	}

	[Fact]
	public async Task NonGenericRawAttributeOnAction_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(200)",
				"Method",
				"MyAction"));
	}

	[Fact]
	public async Task GenericRawAttributeOnAction_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType<string>(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType<string>(200)",
				"Method",
				"MyAction"));
	}

	[Fact]
	public async Task RawAttributeOnController_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    [Microsoft.AspNetCore.Mvc.ProducesResponseType(500)]
			    public abstract class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(500)",
				"Controller",
				"MyController"));
	}

	[Fact]
	public async Task MultipleRawAttributes_ShouldReportEachAttribute()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(404)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(200)",
				"Method",
				"MyAction"),
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(404)",
				"Method",
				"MyAction"));
	}

	[Fact]
	public async Task UmbrellaAttributes_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    [Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaProducesResponseType(500)]
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaProducesResponseType(200)]
			        [Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaProducesResponseType<string>(201)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomAttributeDerivedFromRawAttribute_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public sealed class CustomProducesResponseTypeAttribute : Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute
			    {
			        public CustomProducesResponseTypeAttribute(int statusCode) : base(statusCode) { }
			    }

			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [TestApp.CustomProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "TestApp.CustomProducesResponseType(200)", "Method", "MyAction"));
	}

	[Fact]
	public async Task SameNamedUnrelatedAttribute_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public sealed class ProducesResponseTypeAttribute : System.Attribute
			    {
			        public ProducesResponseTypeAttribute(int statusCode) { }
			    }

			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [TestApp.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RawAttributeOnIndirectGenericControllerDescendant_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class ControllerBase<T> : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			    }

			    public class MyController : ControllerBase<string>
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(200)",
				"Method",
				"MyAction"));
	}

	[Fact]
	public async Task RawAttributeOnAbstractControllerAction_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class MyControllerBase : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public abstract void MyAction();
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(
				source,
				"Microsoft.AspNetCore.Mvc.ProducesResponseType(200)",
				"Method",
				"MyAction"));
	}

	[Fact]
	public async Task NonActionOverride_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class MyControllerBase : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.NonAction]
			        public virtual void Helper() { }
			    }

			    public class MyController : MyControllerBase
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public override void Helper() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonActionCandidates_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        private void PrivateHelper() { }

			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public static void StaticHelper() { }

			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void GenericHelper<T>() { }

			        [Microsoft.AspNetCore.Mvc.NonAction]
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void NonActionHelper() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonControllerType_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    [Microsoft.AspNetCore.Mvc.NonController]
			    [Microsoft.AspNetCore.Mvc.ProducesResponseType(500)]
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RawAttributeOutsideUmbrellaControllerHierarchy_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    [Microsoft.AspNetCore.Mvc.ProducesResponseType(500)]
			    public class MyController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task GeneratedController_ShouldNotReportDiagnostic()
	{
		const string source = """
			// <auto-generated/>
			""" + ControllerStubs + """
			namespace TestApp
			{
			    [Microsoft.AspNetCore.Mvc.ProducesResponseType(500)]
			    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MissingUmbrellaApiController_ShouldNotReportDiagnostic()
	{
		const string source = """
			using System;
			namespace Microsoft.AspNetCore.Mvc
			{
			    public class ProducesResponseTypeAttribute : Attribute
			    {
			        public ProducesResponseTypeAttribute(int statusCode) { }
			    }
			}
			namespace TestApp
			{
			    public class MyController
			    {
			        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
			        public void MyAction() { }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	private static ExpectedDiagnostic ExpectedAt(
		string source,
		string marker,
		string targetKind,
		string targetName)
	{
		int index = source.IndexOf(marker, StringComparison.Ordinal);
		string precedingText = source[..index];
		int line = precedingText.Count(static character => character == '\n') + 1;
		int column = index - precedingText.LastIndexOf('\n');

		return Diagnostic(
			UmbrellaApiStandardsAnalyzer.UseUmbrellaProducesResponseTypeRule,
			line,
			column,
			targetKind,
			targetName);
	}
}
