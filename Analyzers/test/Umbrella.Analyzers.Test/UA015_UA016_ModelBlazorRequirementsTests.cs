namespace Umbrella.Analyzers.Test;

public class UA015_UA016_ModelBlazorRequirementsTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	private const string TrimmableStub = @"namespace Umbrella.Utilities.Text
{
    public interface IUmbrellaTrimmable { }
}
";

	// UA015 tests

	[Fact]
	public async Task ModelRecord_NonPartial_WhenTrimmablePresent_ReportsDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public record UserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(UmbrellaModelStandardsAnalyzer.ModelRecordMustBePartialRule, 7, 19));
	}

	[Fact]
	public async Task ModelRecord_Partial_WhenTrimmablePresent_NoDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record UserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonModelRecord_NonPartial_WhenTrimmablePresent_NoDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public record PersonRecord
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ModelRecord_NonPartial_WhenTrimmableAbsent_NoDiagnostic()
	{
		const string source = @"namespace TestApp
{
    public record UserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	// UA016 tests

	[Fact]
	public async Task CreateModel_WithStringProperty_NotTrimmable_ReportsDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record CreateUserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(UmbrellaModelStandardsAnalyzer.InputModelMustImplementTrimmableRule, 7, 27));
	}

	[Fact]
	public async Task UpdateModel_WithStringProperty_NotTrimmable_ReportsDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record UpdateUserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(UmbrellaModelStandardsAnalyzer.InputModelMustImplementTrimmableRule, 7, 27));
	}

	[Fact]
	public async Task CreateModel_WithStringProperty_ImplementsTrimmable_NoDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record CreateUserModel : Umbrella.Utilities.Text.IUmbrellaTrimmable
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CreateModel_NoStringProperties_NotTrimmable_NoDiagnostic()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record CreateUserModel
    {
        public required int Age { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UserModel_NotCreateOrUpdate_WithStringProperty_NoDiagnosticForUA016()
	{
		const string source = TrimmableStub + @"namespace TestApp
{
    public partial record UserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CreateModel_WhenTrimmableAbsent_NoDiagnostic()
	{
		const string source = @"namespace TestApp
{
    public partial record CreateUserModel
    {
        public required string Name { get; init; }
    }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
