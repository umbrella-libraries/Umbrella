namespace Umbrella.Analyzers.ModelStandards.Test;

public class UMS001_ModelMustBeRecordTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task ModelClass_ShouldTriggerDiagnostic()
	{
		const string source = @"namespace TestProject;

public class UserModel
{
    public string Name { get; set; }
}";
		var expected = new[]
		{
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 3, 14, "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 5, 19, "Name", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 5, 19, "Name", "UserModel")
		};
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task ModelRecord_ShouldNotTriggerDiagnostic()
	{
		const string source = @"namespace TestProject;

public record UserModel
{
    public required string Name { get; init; }
}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonModelClass_ShouldNotTriggerDiagnostic()
	{
		const string source = @"namespace TestProject;

public class NotAModelType
{
    public string Name { get; set; }
}";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ModelClass_WithOptOutAttribute_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using System;

[UmbrellaExcludeFromModelStandards]
public class UserModel
{
    public string Name { get; set; }
}

public class UmbrellaExcludeFromModelStandardsAttribute : Attribute { }";
		await VerifyNoDiagnosticsAsync(source);
	}
}