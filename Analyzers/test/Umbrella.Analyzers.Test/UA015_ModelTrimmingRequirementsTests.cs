namespace Umbrella.Analyzers.Test;

public class UA015_ModelTrimmingRequirementsTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	private const string TrimmableStub = """
		using Umbrella.Analyzers;

		namespace Umbrella.Utilities.Text
		{
			public interface IUmbrellaTrimmable
			{
				void TrimAllStringProperties();
			}
		}

		""";

	[Fact]
	public async Task ModelRecordWithMutableString_ShouldTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 10, 15, "UserModel"));
	}

	[Fact]
	public async Task ModelClassWithMutableString_ShouldTriggerUA011AndUA015()
	{
		const string source = TrimmableStub + """
			public class UserModel
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule, 10, 14, "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 10, 14, "UserModel"));
	}

	[Fact]
	public async Task NullableMutableString_ShouldTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string? DisplayName { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 10, 15, "UserModel"));
	}

	[Fact]
	public async Task InheritedMutableString_ShouldTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserState
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}

			public record UserModel : UserState;
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 16, 15, "UserModel"));
	}

	[Fact]
	public async Task InitOnlyString_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserModel
			{
				public required string Name { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task GetOnlyAndStaticStrings_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserModel
			{
				public required string Name { get; } = "";
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public static required string Description { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutableNonStringProperty_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required int Age { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ExactTrimmableInterface_ShouldSatisfyRule()
	{
		const string source = TrimmableStub + """
			public partial record UserModel : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonPartialManualImplementation_ShouldSatisfyRule()
	{
		const string source = TrimmableStub + """
			public record UserModel : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }

				public void TrimAllStringProperties()
				{
					Name = Name.Trim();
				}
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InheritedTrimmableInterface_ShouldSatisfyRule()
	{
		const string source = TrimmableStub + """
			public record UserState : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
				public void TrimAllStringProperties() => Name = Name.Trim();
			}

			public record UserModel : UserState;
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedInterfaceWithSameShortName_ShouldNotSatisfyRule()
	{
		const string source = TrimmableStub + """
			namespace TestApp
			{
				public interface IUmbrellaTrimmable
				{
					void TrimAllStringProperties();
				}

				public record UserModel : IUmbrellaTrimmable
				{
					[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
					public required string Name { get; set; }
					public void TrimAllStringProperties() => Name = Name.Trim();
				}
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 17, 16, "UserModel"));
	}

	[Fact]
	public async Task TrimmableInterfaceAbsent_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			using Umbrella.Analyzers;

			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonModelWithMutableString_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmableStub + """
			public record UserState
			{
				[UmbrellaAllowMutableProperty("Mutable property required by the test model.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}
}
