namespace Umbrella.Analyzers.Test;

public class UA012_PropertiesMustBeRequiredTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task PropertyWithoutRequired_ShouldTriggerDiagnostic()
	{
		const string source = @"public record UserModel
{
    public string Name { get; init; }
}";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 3, 19, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PropertyWithRequired_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public record UserModel
{
    public required string Name { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyWithOptOutAttribute_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using Umbrella.Analyzers;

public record UserModel
{
    [UmbrellaAllowNonRequiredProperty(""Populated by the serializer after construction."")]
    public string Name { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task SimilarlyNamedAttribute_ShouldNotSuppressDiagnostic()
	{
		const string source = @"using System;

public record UserModel
{
    [UmbrellaAllowNonRequiredProperty]
    public string Name { get; init; }
}

public sealed class UmbrellaAllowNonRequiredPropertyAttribute : Attribute { }";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 6, 19, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}
}
