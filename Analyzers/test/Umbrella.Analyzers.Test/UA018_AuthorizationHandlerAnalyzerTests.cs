namespace Umbrella.Analyzers.Test;

public class UA018_AuthorizationHandlerAnalyzerTests : AnalyzerTestBase<AuthorizationHandlerAnalyzer>
{
	private const string AuthorizationStubs = @"using System.Threading.Tasks;
namespace Microsoft.AspNetCore.Authorization
{
    public class AuthorizationHandlerContext
    {
        public void Fail() { }
        public void Fail(string reason) { }
        public void Succeed(object requirement) { }
    }
    public abstract class AuthorizationHandler<TRequirement>
    {
        protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement);
    }
    public class TestRequirement { }
}
";

	[Fact]
	public async Task ContextFailZeroArgs_InsideHandleRequirementAsync_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            context.Fail();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(AuthorizationHandlerAnalyzer.DoNotCallContextFailRule, 24, 13));
	}

	[Fact]
	public async Task ContextFailWithArg_InsideHandleRequirementAsync_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            context.Fail(""reason"");
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(AuthorizationHandlerAnalyzer.DoNotCallContextFailRule, 24, 13));
	}

	[Fact]
	public async Task ContextSucceed_InsideHandleRequirementAsync_NoDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            context.Succeed(requirement);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ContextFail_OutsideHandleRequirementAsync_NoDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyService
    {
        public void SomeMethod(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context)
        {
            context.Fail();
        }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NoAuthorizationHandlerContextInCompilation_NoDiagnostic()
	{
		const string source = @"namespace TestApp
{
    public class FakeContext
    {
        public void Fail() { }
    }
    public class MyHandler
    {
        public void HandleRequirementAsync(FakeContext context)
        {
            context.Fail();
        }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
