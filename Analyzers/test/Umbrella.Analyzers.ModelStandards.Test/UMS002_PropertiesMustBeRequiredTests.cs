namespace Umbrella.Analyzers.ModelStandards.Test;

public class UMS002_PropertiesMustBeRequiredTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
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
		const string source = @"using System;

public record UserModel
{
    [UmbrellaAllowOptionalProperty]
    public string Name { get; init; }
}

public class UmbrellaAllowOptionalPropertyAttribute : Attribute { }";
		await VerifyNoDiagnosticsAsync(source);
	}
}