namespace Umbrella.Analyzers.Test;

public class UA018_AuthorizationHandlerAnalyzerTests : AnalyzerTestBase<AuthorizationHandlerAnalyzer>
{
	private const string AuthorizationStubs = @"using System.Threading.Tasks;
namespace Microsoft.AspNetCore.Authorization
{
    public class AuthorizationHandlerContext
    {
        public void Fail() { }
        public void Fail(AuthorizationFailureReason reason) { }
        public void Succeed(object requirement) { }
    }
    public sealed class AuthorizationFailureReason { }
    public interface IAuthorizationHandler
    {
        Task HandleAsync(AuthorizationHandlerContext context);
    }
    public abstract class AuthorizationHandler<TRequirement>
    {
        protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement);
    }
    public abstract class AuthorizationHandler<TRequirement, TResource>
    {
        protected abstract Task HandleRequirementAsync(AuthorizationHandlerContext context, TRequirement requirement, TResource resource);
    }
    public class TestRequirement { }
}
";

	[Fact]
	public async Task ContextFailZeroArgs_InRequirementHandler_ReportsDiagnostic()
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
		await VerifyAnalyzerAsync(source, FailDiagnostic(source));
	}

	[Fact]
	public async Task ContextFailWithReason_InResourceHandler_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public sealed class Resource { }

    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement, Resource>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement,
            Resource resource)
        {
            context.Fail(new Microsoft.AspNetCore.Authorization.AuthorizationFailureReason());
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyAnalyzerAsync(source, FailDiagnostic(source));
	}

	[Fact]
	public async Task ContextFail_InDirectAuthorizationHandlerImplementation_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyHandler : Microsoft.AspNetCore.Authorization.IAuthorizationHandler
    {
        public System.Threading.Tasks.Task HandleAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context)
        {
            context.Fail();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyAnalyzerAsync(source, FailDiagnostic(source));
	}

	[Fact]
	public async Task ContextFail_InHandlerHelperMethod_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            Deny(context);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private static void Deny(Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context)
        {
            context.Fail();
        }
    }
}";
		await VerifyAnalyzerAsync(source, FailDiagnostic(source));
	}

	[Fact]
	public async Task ContextFail_InIndirectHandler_ReportsDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public abstract class HandlerBase : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
    }

    public class MyHandler : HandlerBase
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            var authorizationContext = context;
            authorizationContext.Fail();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyAnalyzerAsync(source, FailDiagnostic(source, "authorizationContext.Fail()"));
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
	public async Task ContextFail_InLookalikeHandleRequirementAsync_NoDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public class MyService
    {
        public System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            context.Fail();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ContextFail_InNonHandlerService_NoDiagnostic()
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
	public async Task UnrelatedFailMethod_InAuthorizationHandler_NoDiagnostic()
	{
		const string source = AuthorizationStubs + @"namespace TestApp
{
    public sealed class FakeContext
    {
        public void Fail() { }
    }

    public class MyHandler : Microsoft.AspNetCore.Authorization.AuthorizationHandler<Microsoft.AspNetCore.Authorization.TestRequirement>
    {
        protected override System.Threading.Tasks.Task HandleRequirementAsync(
            Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext context,
            Microsoft.AspNetCore.Authorization.TestRequirement requirement)
        {
            new FakeContext().Fail();
            return System.Threading.Tasks.Task.CompletedTask;
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

	private static ExpectedDiagnostic FailDiagnostic(string source, string invocation = "context.Fail")
	{
		int index = source.IndexOf(invocation, StringComparison.Ordinal);
		Assert.True(index >= 0, $"Could not find '{invocation}' in the test source.");

		int line = source[..index].Count(character => character == '\n') + 1;
		int previousNewLine = source.LastIndexOf('\n', index);
		int column = index - previousNewLine;

		return Diagnostic(AuthorizationHandlerAnalyzer.DoNotCallContextFailRule, line, column);
	}
}
