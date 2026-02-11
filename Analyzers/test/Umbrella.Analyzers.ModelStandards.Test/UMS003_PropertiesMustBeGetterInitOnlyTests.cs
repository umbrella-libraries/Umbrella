namespace Umbrella.Analyzers.ModelStandards.Test;

public class UMS003_PropertiesMustBeGetterInitOnlyTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task PropertyWithSet_ShouldTriggerDiagnostic()
	{
		const string source = @"public record UserModel
{
    public required string Name { get; set; }
}";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 3, 28, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PropertyWithInit_ShouldNotTriggerDiagnostic()
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
    [UmbrellaAllowMutableProperty]
    public required string Name { get; set; }
}

public class UmbrellaAllowMutablePropertyAttribute : Attribute { }";
		await VerifyNoDiagnosticsAsync(source);
	}
}