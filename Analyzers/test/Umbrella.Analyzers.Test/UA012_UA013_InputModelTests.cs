namespace Umbrella.Analyzers.Test;

public class UA012_UA013_InputModelTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task InputModelRecord_ShouldAllowNonRequiredSettableProperty()
	{
		const string source = """
			using Umbrella.Analyzers;

			[UmbrellaInputModel]
			public record UserModel
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InputModelAttribute_OnAbstractBase_ShouldNotApplyToDerivedModel()
	{
		const string source = """
			using Umbrella.Analyzers;

			[UmbrellaInputModel]
			public abstract record InputModelBase
			{
				public string Name { get; set; } = "";
			}

			public record UserModel : InputModelBase
			{
				public int Age { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.InputModelMustBeConcreteRule, 4, 24, "InputModelBase"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 6, 16, "Name", "InputModelBase"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 16, "Name", "InputModelBase"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 11, 13, "Age", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 11, 13, "Age", "UserModel"));
	}

	[Fact]
	public async Task InputModelAttribute_OnOnePartialDeclaration_ShouldApplyToAllParts()
	{
		const string source = """
			using Umbrella.Analyzers;

			[UmbrellaInputModel]
			public partial record UserModel;

			public partial record UserModel
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InputModel_WriteOnlyProperty_ShouldTriggerUA013()
	{
		const string source = """
			using Umbrella.Analyzers;

			[UmbrellaInputModel]
			public record UserModel
			{
				public string Name { set { } }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 16, "Name", "UserModel"));
	}

	[Fact]
	public async Task InputModel_MutableCollection_ShouldStillTriggerUA014()
	{
		const string source = """
			using System.Collections.Generic;
			using Umbrella.Analyzers;

			[UmbrellaInputModel]
			public record UserModel
			{
				public List<string> Items { get; set; } = [];
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule, 7, 22, "Items", "UserModel"));
	}

	[Fact]
	public async Task ModelInterface_ShouldNotTriggerUA012OrUA013()
	{
		const string source = """
			public interface IUserModel
			{
				string Name { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ModelInterface_MutableCollection_ShouldStillTriggerUA014()
	{
		const string source = """
			using System.Collections.Generic;

			public interface IUserModel
			{
				List<string> Items { get; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule, 5, 15, "Items", "IUserModel"));
	}

	[Fact]
	public async Task InputModel_ShouldStillTriggerUA015()
	{
		const string source = """
			using Umbrella.Analyzers;

			namespace Umbrella.Utilities.Text
			{
				public interface IUmbrellaTrimmable
				{
					void TrimAllStringProperties();
				}
			}

			[UmbrellaInputModel]
			public record UserModel
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 12, 15, "UserModel"));
	}

	[Fact]
	public async Task MutablePropertyAttribute_ShouldSuppressUA013AndUA014()
	{
		const string source = """
			using System.Collections.Generic;
			using Umbrella.Analyzers;

			public record UserModel
			{
				[UmbrellaAllowMutableProperty("The collection is edited in place.")]
				public required List<string> Items { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task SimilarlyNamedInputModelAttribute_ShouldNotSuppressDiagnostics()
	{
		const string source = """
			using System;

			[UmbrellaInputModel]
			public record UserModel
			{
				public string Name { get; set; } = "";
			}

			public sealed class UmbrellaInputModelAttribute : Attribute
			{
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 6, 16, "Name", "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 16, "Name", "UserModel"));
	}
}
