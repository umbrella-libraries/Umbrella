namespace Umbrella.Analyzers.Test;

public class UA022_ConcreteModelMustBeSealedTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task ConcreteUnsealedModelRecord_ShouldTriggerUA022()
	{
		const string source = "public record UserModel;";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ConcreteModelMustBeSealedRule, 1, 15, "UserModel"));
	}

	[Fact]
	public async Task SealedModelRecord_ShouldNotTriggerDiagnostic()
	{
		const string source = "public sealed record UserModel;";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task AbstractModelRecord_ShouldNotTriggerDiagnostic()
	{
		const string source = "public abstract record UserModelBase;";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RecordStructModel_ShouldNotTriggerDiagnostic()
	{
		const string source = "public record struct UserModel;";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnsealedModel_WithJustifiedOptOut_ShouldNotTriggerDiagnostic()
	{
		const string source = """
using Umbrella.Analyzers;

[UmbrellaAllowUnsealedModel("Extended by specialized response models.")]
public record UserModel;
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PartialUnsealedModel_ShouldTriggerUA022Once()
	{
		const string source = """
public partial record UserModel;
public partial record UserModel;
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ConcreteModelMustBeSealedRule, 1, 23, "UserModel"));
	}

	[Fact]
	public async Task ModelClass_ShouldOnlyTriggerUA011BeforeRecordMigration()
	{
		const string source = "public sealed class UserModel;";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 1, 21, "UserModel"));
	}
}
