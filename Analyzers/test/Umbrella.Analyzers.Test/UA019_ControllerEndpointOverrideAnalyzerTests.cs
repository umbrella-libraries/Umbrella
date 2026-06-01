namespace Umbrella.Analyzers.Test;

public class UA019_ControllerEndpointOverrideAnalyzerTests : AnalyzerTestBase<ControllerEndpointOverrideAnalyzer>
{
	[Fact]
	public async Task OverridePostAsync_WithoutBaseCall_ShouldTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void PostAsync() { }
}

public class OrderController : B
{
    public override void PostAsync()
    {
    }
}";
		var expected = Diagnostic(ControllerEndpointOverrideAnalyzer.Rule, 8, 26, "PostAsync", "OrderController");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task OverridePostAsync_WithBaseCall_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void PostAsync() { }
}

public class OrderController : B
{
    public override void PostAsync()
    {
        base.PostAsync();
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverridePostAsync_WithNonActionAttribute_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class NonActionAttribute : System.Attribute { }

public class B
{
    public virtual void PostAsync() { }
}

public class OrderController : B
{
    [NonAction]
    public override void PostAsync()
    {
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverridePostAsync_InNonControllerClass_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void PostAsync() { }
}

public class OrderService : B
{
    public override void PostAsync()
    {
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverrideNonCrudMethod_InController_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void ConfigureAsync() { }
}

public class OrderController : B
{
    public override void ConfigureAsync()
    {
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverrideDeleteAsync_WithoutBaseCall_ShouldTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void DeleteAsync() { }
}

public class ItemController : B
{
    public override void DeleteAsync()
    {
    }
}";
		var expected = Diagnostic(ControllerEndpointOverrideAnalyzer.Rule, 8, 26, "DeleteAsync", "ItemController");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task OverrideSearchSlimAsync_WithoutBaseCall_ShouldTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void SearchSlimAsync() { }
}

public class ProductController : B
{
    public override void SearchSlimAsync()
    {
    }
}";
		var expected = Diagnostic(ControllerEndpointOverrideAnalyzer.Rule, 8, 26, "SearchSlimAsync", "ProductController");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task OverridePostAsync_ExpressionBodyWithBaseCall_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void PostAsync() { }
}

public class OrderController : B
{
    public override void PostAsync() => base.PostAsync();
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverridePostAsync_AwaitBaseCall_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual System.Threading.Tasks.Task PostAsync() => System.Threading.Tasks.Task.CompletedTask;
}

public class OrderController : B
{
    public override async System.Threading.Tasks.Task PostAsync()
    {
        await base.PostAsync();
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OverrideGetAsync_WithoutBaseCall_ShouldTriggerDiagnostic()
	{
		const string source = @"public class B
{
    public virtual void GetAsync() { }
}

public class ContactController : B
{
    public override void GetAsync()
    {
    }
}";
		var expected = Diagnostic(ControllerEndpointOverrideAnalyzer.Rule, 8, 26, "GetAsync", "ContactController");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task OverridePostAsync_WithNonActionAttributeFullName_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public class NonActionAttribute : System.Attribute { }

public class B
{
    public virtual void PostAsync() { }
}

public class OrderController : B
{
    [NonActionAttribute]
    public override void PostAsync()
    {
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
