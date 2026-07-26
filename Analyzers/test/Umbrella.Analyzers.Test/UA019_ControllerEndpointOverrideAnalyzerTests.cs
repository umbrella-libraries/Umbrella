namespace Umbrella.Analyzers.Test;

public class UA019_ControllerEndpointOverrideAnalyzerTests : AnalyzerTestBase<ControllerEndpointOverrideAnalyzer>
{
	private const string ControllerStubs = """
		using System;
		using System.Threading.Tasks;

		namespace Microsoft.AspNetCore.Mvc.Routing
		{
		    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
		    public abstract class HttpMethodAttribute : Attribute { }
		}

		namespace Microsoft.AspNetCore.Mvc
		{
		    public sealed class HttpGetAttribute : Routing.HttpMethodAttribute { }
		    public sealed class HttpPostAttribute : Routing.HttpMethodAttribute { }
		    public sealed class HttpDeleteAttribute : Routing.HttpMethodAttribute { }

		    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
		    public sealed class NonActionAttribute : Attribute { }
		}

		namespace Umbrella.AspNetCore.WebUtilities.Mvc
		{
		    public abstract class UmbrellaApiController { }

		    public abstract class UmbrellaGenericRepositoryApiController<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
		        : UmbrellaApiController
		    {
		        [Microsoft.AspNetCore.Mvc.HttpPost]
		        public virtual Task<int> PostAsync(int value) => Task.FromResult(value);

		        public Task<int> PostAsync() => Task.FromResult(0);

		        [Microsoft.AspNetCore.Mvc.HttpGet]
		        public virtual Task<int> GetAsync(int value) => Task.FromResult(value);

		        [Microsoft.AspNetCore.Mvc.HttpPost]
		        public virtual Task<int> UpsertAsync(int value) => Task.FromResult(value);
		    }

		    public abstract class UmbrellaGenericRepositoryDataServiceApiController<T1, T2, T3, T4, T5, T6, T7, T8, T9>
		        : UmbrellaApiController
		    {
		        [Microsoft.AspNetCore.Mvc.HttpDelete]
		        public virtual Task<int> DeleteAsync(int value) => Task.FromResult(value);
		    }
		}

		namespace TestApp
		{
		    public abstract class TestRepositoryController
		        : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaGenericRepositoryApiController<object, object, object, object, object, object, object, object, object, object, object>
		    {
		    }

		    public abstract class TestDataServiceController
		        : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaGenericRepositoryDataServiceApiController<object, object, object, object, object, object, object, object, object>
		    {
		    }
		}

		""";

	[Fact]
	public async Task UmbrellaEndpointOverrideWithoutBaseCall_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task ExpressionBodiedBaseCall_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value) => base.PostAsync(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task AwaitedBaseCall_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override async Task<int> PostAsync(int value)
			        {
			            return await base.PostAsync(value);
			        }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task BaseCallOnEveryReturnBranch_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override async Task<int> PostAsync(int value)
			        {
			            if (value > 0)
			                return await base.PostAsync(value);

			            return await base.PostAsync(0);
			        }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task BaseCallOnOnlyOneReturnBranch_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override async Task<int> PostAsync(int value)
			        {
			            if (value > 0)
			                return await base.PostAsync(value);

			            return value;
			        }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override async Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task ThrowingBranchAndBaseCallOnNormalReturn_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override async Task<int> PostAsync(int value)
			        {
			            if (value < 0)
			                throw new ArgumentOutOfRangeException(nameof(value));

			            return await base.PostAsync(value);
			        }
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task BaseCallInsideLambda_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value)
			        {
			            Func<Task<int>> invokeBase = () => base.PostAsync(value);
			            return Task.FromResult(value);
			        }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task BaseCallInsideLocalFunction_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value)
			        {
			            Task<int> InvokeBase() => base.PostAsync(value);
			            return Task.FromResult(value);
			        }
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task CallToWrongBaseOverload_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value) => base.PostAsync();
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task NonActionEndpointOverride_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    using SkipAction = Microsoft.AspNetCore.Mvc.NonActionAttribute;

			    public class OrderController : TestRepositoryController
			    {
			        [SkipAction]
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InheritedNonActionEndpointOverride_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class DisabledPostController : TestRepositoryController
			    {
			        [Microsoft.AspNetCore.Mvc.NonAction]
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }

			    public class OrderController : DisabledPostController
			    {
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task SameNamedNonActionAttribute_ShouldNotSuppressDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public sealed class NonActionAttribute : Attribute { }

			    public class OrderController : TestRepositoryController
			    {
			        [NonAction]
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task SameNamedMethodOutsideUmbrellaControllerFamily_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class Base
			    {
			        public virtual Task<int> PostAsync(int value) => Task.FromResult(value);
			    }

			    public class OrderController : Base
			    {
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CustomEndpointOnUmbrellaApiController_ShouldNotReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class CustomBaseController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
			    {
			        [Microsoft.AspNetCore.Mvc.HttpPost]
			        public virtual Task<int> PostAsync(int value) => Task.FromResult(value);
			    }

			    public class OrderController : CustomBaseController
			    {
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task FutureHttpEndpointOnRepositoryController_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> UpsertAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> UpsertAsync(int value)", "UpsertAsync", "OrderController"));
	}

	[Fact]
	public async Task DataServiceEndpointOverride_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestDataServiceController
			    {
			        public override Task<int> DeleteAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public override Task<int> DeleteAsync(int value)", "DeleteAsync", "OrderController"));
	}

	[Fact]
	public async Task AbstractEndpointOverride_ShouldReportDiagnostic()
	{
		const string source = ControllerStubs + """
			namespace TestApp
			{
			    public abstract class OrderController : TestRepositoryController
			    {
			        public abstract override Task<int> PostAsync(int value);
			    }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			ExpectedAt(source, "public abstract override Task<int> PostAsync(int value)", "PostAsync", "OrderController"));
	}

	[Fact]
	public async Task GeneratedEndpointOverride_ShouldNotReportDiagnostic()
	{
		const string source = """
			// <auto-generated/>
			""" + ControllerStubs + """
			namespace TestApp
			{
			    public class OrderController : TestRepositoryController
			    {
			        public override Task<int> PostAsync(int value) => Task.FromResult(value);
			    }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	private static ExpectedDiagnostic ExpectedAt(
		string source,
		string declaration,
		string methodName,
		string controllerName)
	{
		int declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
		int methodIndex = declarationIndex + declaration.IndexOf(methodName, StringComparison.Ordinal);
		string precedingText = source[..methodIndex];
		int line = precedingText.Count(static character => character == '\n') + 1;
		int column = methodIndex - precedingText.LastIndexOf('\n');

		return Diagnostic(
			ControllerEndpointOverrideAnalyzer.Rule,
			line,
			column,
			methodName,
			controllerName);
	}
}
