namespace Umbrella.Analyzers.Test;

public class UA023_UmbrellaApiStandardsAnalyzerTests : AnalyzerTestBase<UmbrellaApiStandardsAnalyzer>
{
	private const string ControllerStubs = @"using System;
namespace Microsoft.AspNetCore.Mvc
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ProducesResponseTypeAttribute : Attribute
    {
        public ProducesResponseTypeAttribute(int statusCode) { }
        public ProducesResponseTypeAttribute(Type type, int statusCode) { }
    }
}
namespace Umbrella.AspNetCore.WebUtilities.Mvc
{
    public class UmbrellaApiController { }
}
";

	[Fact]
	public async Task MethodWithProducesResponseType_InControllerSubclass_ReportsDiagnostic()
	{
		const string source = ControllerStubs + @"namespace TestApp
{
    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
    {
        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
        public void MyAction() { }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(UmbrellaApiStandardsAnalyzer.UseUmbrellaProducesResponseTypeRule, 19, 10));
	}

	[Fact]
	public async Task MethodWithoutProducesResponseType_InControllerSubclass_NoDiagnostic()
	{
		const string source = ControllerStubs + @"namespace TestApp
{
    public class MyController : Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController
    {
        public void MyAction() { }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MethodWithProducesResponseType_NotInControllerSubclass_NoDiagnostic()
	{
		const string source = ControllerStubs + @"namespace TestApp
{
    public class MyService
    {
        [Microsoft.AspNetCore.Mvc.ProducesResponseType(200)]
        public void MyAction() { }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NoUmbrellaApiControllerInCompilation_NoDiagnostic()
	{
		const string source = @"using System;
namespace Microsoft.AspNetCore.Mvc
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
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
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
