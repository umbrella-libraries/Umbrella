namespace Umbrella.Analyzers.Test;

public class UA020_EntityQueryParameterAnalyzerTests : AnalyzerTestBase<EntityQueryParameterAnalyzer>
{
	// Lines 1-2 of every source string.
	// Line 1: IEntity<T> stub in the correct namespace so GetTypeByMetadataName resolves it.
	// Line 2: A concrete entity class implementing it.
	// Test-specific code always starts at line 3.
	private const string Preamble =
		"namespace Umbrella.DataAccess.Abstractions { public interface IEntity<TEntityKey> where TEntityKey : System.IEquatable<TEntityKey> { TEntityKey Id { get; set; } } }\n" +
		"public class Lead : Umbrella.DataAccess.Abstractions.IEntity<System.Guid> { public System.Guid Id { get; set; } public string? Name { get; set; } }\n";

	[Fact]
	public async Task FindMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public class LeadRepository { public Lead? FindByEmail(Lead lead) => null; }"
		//  30 chars before method body ─────────────────────────────────────────^
		//  "public Lead? FindByEmail(Lead " = 30 chars → 'lead' at col 61
		const string source = Preamble +
			"public class LeadRepository { public Lead? FindByEmail(Lead lead) => null; }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 61, "lead", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task GetMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public class LeadRepository { public Lead? GetAllByStatus(Lead status) => null; }"
		//  "public Lead? GetAllByStatus(Lead " = 33 chars → 'status' at col 64
		const string source = Preamble +
			"public class LeadRepository { public Lead? GetAllByStatus(Lead status) => null; }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 64, "status", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task SearchMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public class LeadRepository { public void SearchLeads(Lead criteria) { } }"
		//  "public void SearchLeads(Lead " = 29 chars → 'criteria' at col 60
		const string source = Preamble +
			"public class LeadRepository { public void SearchLeads(Lead criteria) { } }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 60, "criteria", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task FetchMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public class LeadRepository { public Lead? FetchByStatus(Lead lead) => null; }"
		//  "public Lead? FetchByStatus(Lead " = 32 chars → 'lead' at col 63
		const string source = Preamble +
			"public class LeadRepository { public Lead? FetchByStatus(Lead lead) => null; }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 63, "lead", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task InterfaceQueryMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public interface ILeadRepository { Lead? FindByEmail(Lead lead); }"
		//  "Lead? FindByEmail(Lead " = 23 chars after 35-char prefix → 'lead' at col 59
		const string source = Preamble +
			"public interface ILeadRepository { Lead? FindByEmail(Lead lead); }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 59, "lead", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task FindMethod_EntityParamAmongOthers_ShouldTriggerOnlyForEntityParam()
	{
		// "public class LeadRepository { public void FindLeads(Lead criteria, string sortOrder) { } }"
		//  "public void FindLeads(Lead " = 27 chars after 30 → 'criteria' at col 58
		const string source = Preamble +
			"public class LeadRepository { public void FindLeads(Lead criteria, string sortOrder) { } }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 58, "criteria", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task FindMethod_WithPrimitiveParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = Preamble +
			"public class LeadRepository { public Lead? FindByEmail(string email) => null; }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task FindMethod_WithGuidParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = Preamble +
			"public class LeadRepository { public Lead? FindById(System.Guid id) => null; }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task SaveMethod_WithEntityParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = Preamble +
			"public class LeadRepository { public void SaveEntityAsync(Lead entity) { } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DeleteMethod_WithEntityParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = Preamble +
			"public class LeadRepository { public void DeleteAsync(Lead entity) { } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UpdateMethod_WithEntityParameter_ShouldNotTriggerDiagnostic()
	{
		const string source = Preamble +
			"public class LeadRepository { public void UpdateAsync(Lead entity) { } }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonEntityType_QueryMethod_ShouldNotTriggerDiagnostic()
	{
		// FilterModel does not implement IEntity<T> — no diagnostic expected.
		const string source = Preamble +
			"public class FilterModel { public string? Email { get; set; } } " +
			"public class LeadRepository { public Lead? FindByFilter(FilterModel filter) => null; }";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task QueryMethod_OutsideRepository_ShouldStillTriggerDiagnostic()
	{
		// The rule is not scoped to repository classes — it fires anywhere.
		// "public class LeadService { public void FindLeads(Lead criteria) { } }"
		//  "public void FindLeads(Lead " = 27 chars after 27-char prefix → 'criteria' at col 55
		const string source = Preamble +
			"public class LeadService { public void FindLeads(Lead criteria) { } }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 55, "criteria", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task LookupMethod_WithEntityParameter_ShouldTriggerDiagnostic()
	{
		// "public class LeadRepository { public Lead? LookupByName(Lead lead) => null; }"
		//  "public Lead? LookupByName(Lead " = 31 chars after 30-char prefix → 'lead' at col 62
		const string source = Preamble +
			"public class LeadRepository { public Lead? LookupByName(Lead lead) => null; }";

		var expected = Diagnostic(EntityQueryParameterAnalyzer.Rule, 3, 62, "lead", "Lead");
		await VerifyAnalyzerAsync(source, expected);
	}
}
