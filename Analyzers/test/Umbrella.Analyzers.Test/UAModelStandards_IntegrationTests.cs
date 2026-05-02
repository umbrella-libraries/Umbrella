namespace Umbrella.Analyzers.Test;

public class UAModelStandards_IntegrationTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task MultipleViolations_ShouldTriggerMultipleDiagnostics()
	{
		const string source = @"using System.Collections.Generic;

public class UserModel
{
    public string Name { get; set; }
    public List<string> Tags { get; set; }
}";
		var expected = new[]
		{
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 3, 14, "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 5, 19, "Name", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 5, 19, "Name", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 6, 25, "Tags", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 25, "Tags", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule, 6, 25, "Tags", "UserModel"),
		};
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task CompliantRecord_ShouldNotTriggerAnyDiagnostics()
	{
		const string source = @"using System.Collections.Generic;

public record UserModel
{
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> Tags { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
