namespace Umbrella.Analyzers.Test;

public class UA011_ModelMustBeRecordTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
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
	public async Task ModelClass_WithInputModelAttribute_ShouldStillTriggerUA011()
	{
		const string source = @"using Umbrella.Analyzers;

[UmbrellaInputModel]
public class UserModel
{
    public string Name { get; set; }
}";
		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 4, 14, "UserModel"));
	}

	[Fact]
	public async Task QueryResultClass_ShouldTriggerDiagnostic()
	{
		const string source = @"namespace TestProject;

public class SlimCareerQueryResult
{
    public string Name { get; set; }
}";
		var expected = new[]
		{
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 3, 14, "SlimCareerQueryResult"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 5, 19, "Name", "SlimCareerQueryResult"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 5, 19, "Name", "SlimCareerQueryResult")
		};
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task QueryResultRecord_ShouldNotTriggerDiagnostic()
	{
		const string source = @"namespace TestProject;

public record SlimCareerQueryResult
{
    public required string Name { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task RazorPageModelDescendant_ShouldNotTriggerModelStandardsDiagnostics()
	{
		const string source = """
namespace Microsoft.AspNetCore.Mvc.RazorPages
{
	public abstract class PageModel
	{
	}
}

namespace TestProject
{
	using Microsoft.AspNetCore.Mvc.RazorPages;

	public abstract class ApplicationPageModel : PageModel
	{
		public string MutableValue { get; set; } = "";
	}

	public sealed class AccountPageModel : ApplicationPageModel
	{
		public string AnotherMutableValue { get; set; } = "";
	}
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task SimilarlyNamedNonRazorPageBase_ShouldNotBeExcluded()
	{
		const string source = """
namespace Contoso
{
	public abstract class PageModel
	{
	}
}

namespace TestProject
{
	public class AccountModel : Contoso.PageModel
	{
	}
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 3, 24, "PageModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 10, 15, "AccountModel"));
	}

	[Fact]
	public async Task NestedUiStateModel_ShouldTriggerDiagnostic()
	{
		const string source = """
namespace TestProject;

public class QuizPage
{
	protected class QuizQuestionModel
	{
	}
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 5, 18, "QuizQuestionModel"));
	}

	[Fact]
	public async Task PaginatedModelClass_ShouldTriggerDiagnostic()
	{
		const string source = """
namespace TestProject;

public abstract record PaginatedResult<T>;

public class PaginatedUserModel : PaginatedResult<string>
{
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 5, 14, "PaginatedUserModel"));
	}
}
