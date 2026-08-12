namespace Umbrella.Analyzers.Test;

public class UA015_ModelTrimmingRequirementsTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	private const string TrimmingStubs = """
		using Umbrella.Analyzers;

		namespace Umbrella.Utilities.Text
		{
			public interface IUmbrellaTrimmable
			{
				void TrimAllStringProperties();
			}

			[System.AttributeUsage(System.AttributeTargets.Property)]
			public sealed class UmbrellaDoNotTrimAttribute : System.Attribute
			{
			}
		}

		namespace Umbrella.Utilities.Data.Concurrency
		{
			// Declared on single lines to hold this block at its current length. Every Diagnostic(...)
			// expectation in this file uses absolute line numbers. Do not reformat or add lines here.
			public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
			public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
		}

		""";

	[Fact]
	public async Task InputModelWithMutableString_ShouldTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserModel
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 24, 15, "UserModel"));
	}

	[Fact]
	public async Task NullableMutableString_ShouldTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserModel
			{
				public string? DisplayName { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 24, 15, "UserModel"));
	}

	[Fact]
	public async Task NonInputModelWithMutableString_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Populated after mapping.")]
				public required string Name { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InputMarkerOnBaseType_ShouldApplyToDerivedType()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserState
			{
			}

			public record UserModel : UserState
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 28, 15, "UserModel"));
	}

	[Fact]
	public async Task InheritedMutableString_ShouldOnlyBeAssessedOnDeclaringType()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserState
			{
				public string Name { get; set; } = "";
			}

			public record UserModel : UserState;
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DerivedTypeAddingMutableString_ShouldRequireDirectImplementation()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserState : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				public void TrimAllStringProperties() { }
			}

			public record UserModel : UserState
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 29, 15, "UserModel"));
	}

	[Fact]
	public async Task DirectTrimmableImplementation_ShouldSatisfyRule()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public partial record UserModel : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DirectInterfaceDerivedFromTrimmable_ShouldSatisfyRule()
	{
		const string source = TrimmingStubs + """
			public interface IUserInput : Umbrella.Utilities.Text.IUmbrellaTrimmable
			{
			}

			[UmbrellaInputModel]
			public partial record UserModel : IUserInput
			{
				public string Name { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InitOnlyString_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserModel
			{
				public required string Name { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutablePropertyOptOut_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UserModel
			{
				[UmbrellaAllowMutableProperty("Populated after mapping.")]
				public string ImageUrl { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task DoNotTrimProperty_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record PasswordModel
			{
				[Umbrella.Utilities.Text.UmbrellaDoNotTrim]
				public string Password { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ConcurrencyStampImplementation_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UpdateModel : Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task StampReachedOnlyViaReadOnlyInterface_ShouldNotTriggerDiagnostic()
	{
		// IUpdateResultModel derives the read-only stamp contract, so the property fills the base interface slot
		// and not the mutable one. This only passes when the exclusion walks inherited interfaces.
		const string source = TrimmingStubs + """
			public interface IUpdateResultModel : Umbrella.Utilities.Data.Concurrency.IReadOnlyConcurrencyStamp
			{
			}

			[UmbrellaInputModel]
			public record UpdateModel : IUpdateResultModel
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task StampImplementingReadOnlyInterfaceDirectly_ShouldNotTriggerDiagnostic()
	{
		const string source = TrimmingStubs + """
			[UmbrellaInputModel]
			public record UpdateModel : Umbrella.Utilities.Data.Concurrency.IReadOnlyConcurrencyStamp
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedReadOnlyConcurrencyInterface_ShouldNotSuppressDiagnostic()
	{
		// A same-named interface in the global namespace must not suppress. Guards against anyone
		// re-implementing the exclusion as a property-name match instead of a symbol match.
		const string source = TrimmingStubs + """
			public interface IReadOnlyConcurrencyStamp
			{
				string ConcurrencyStamp { get; }
			}

			[UmbrellaInputModel]
			public record UpdateModel : IReadOnlyConcurrencyStamp
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 29, 15, "UpdateModel"));
	}

	[Fact]
	public async Task UnrelatedConcurrencyInterface_ShouldNotSuppressDiagnostic()
	{
		const string source = TrimmingStubs + """
			public interface IConcurrencyStamp
			{
				string ConcurrencyStamp { get; set; }
			}

			[UmbrellaInputModel]
			public record UpdateModel : IConcurrencyStamp
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.MutableStringModelMustImplementTrimmableRule, 29, 15, "UpdateModel"));
	}

	[Fact]
	public async Task TrimmableInterfaceAbsent_ShouldNotTriggerDiagnostic()
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
}
