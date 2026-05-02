namespace Umbrella.Analyzers.Test;

public class UAModelStandards_EdgeCaseTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task EmptyClass_ShouldTriggerDiagnostic()
	{
		const string source = @"public class EmptyModel {}";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 1, 14, "EmptyModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task StringProperty_ShouldNotTriggerCollectionDiagnostic()
	{
		const string source = @"public record UserModel
{
    public required string Name { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NestedModelClass_ShouldTriggerDiagnostic()
	{
		const string source = @"public class OuterModel
{
    public class InnerModel
    {
        public string Name { get; set; }
    }
}";
		var expected = new[]
		{
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 1, 14, "OuterModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 3, 18, "InnerModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 5, 23, "Name", "InnerModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 5, 23, "Name", "InnerModel"),
		};
		await VerifyAnalyzerAsync(source, expected);
	}
}
