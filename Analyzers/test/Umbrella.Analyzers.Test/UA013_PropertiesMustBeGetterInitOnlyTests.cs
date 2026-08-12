namespace Umbrella.Analyzers.Test;

public class UA013_PropertiesMustBeGetterInitOnlyTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public async Task PropertyWithSet_ShouldTriggerDiagnostic()
	{
		const string source = @"public record UserModel
{
    public required string Name { get; set; }
}";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 3, 28, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PropertyWithInit_ShouldNotTriggerDiagnostic()
	{
		const string source = @"public record UserModel
{
    public required string Name { get; init; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyWithOptOutAttribute_ShouldNotTriggerDiagnostic()
	{
		const string source = @"using Umbrella.Analyzers;

public record UserModel
{
    [UmbrellaAllowMutableProperty(""Two-way UI binding requires a setter."")]
    public required string Name { get; set; }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyWithOptOutAttributeButNoGetter_ShouldTriggerDiagnostic()
	{
		// [UmbrellaAllowMutableProperty] suppresses the setter check but not the missing-getter check
		const string source = @"using Umbrella.Analyzers;

public record UserModel
{
    [UmbrellaAllowMutableProperty(""Two-way UI binding requires a setter."")]
    public required string Name { set; }
}";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 28, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task SimilarlyNamedAttribute_ShouldNotSuppressDiagnostic()
	{
		const string source = @"using System;

public record UserModel
{
    [UmbrellaAllowMutableProperty]
    public required string Name { get; set; }
}

public sealed class UmbrellaAllowMutablePropertyAttribute : Attribute { }";
		var expected = Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 6, 28, "Name", "UserModel");
		await VerifyAnalyzerAsync(source, expected);
	}

	[Fact]
	public async Task PropertyImplementingInterfaceSetter_ShouldNotTriggerUA013()
	{
		const string source = """
			public interface IConcurrencyStamp
			{
				string ConcurrencyStamp { get; set; }
			}

			public record UserModel : IConcurrencyStamp
			{
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyImplementingDerivedInterfaceSetter_ShouldNotTriggerUA013()
	{
		const string source = """
			public interface IConcurrencyStamp
			{
				string ConcurrencyStamp { get; set; }
			}

			public interface IUpdateModel : IConcurrencyStamp
			{
			}

			public record UserModel : IUpdateModel
			{
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutableStampImplementingRedeclaredSetter_ShouldNotTriggerUA013()
	{
		// IConcurrencyStamp re-declares the stamp with a setter, so direct implementers keep the UA013 suppression.
		// This is what allows entities to stay mutable.
		const string source = """
			public interface IReadOnlyConcurrencyStamp
			{
				string ConcurrencyStamp { get; }
			}

			public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp
			{
				new string ConcurrencyStamp { get; set; }
			}

			public record UserModel : IConcurrencyStamp
			{
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InitStampReachedOnlyViaReadOnlyInterface_ShouldNotTriggerUA013()
	{
		// The goal shape: a result model reaching the stamp through the read-only contract declares it init-only.
		const string source = """
			public interface IReadOnlyConcurrencyStamp
			{
				string ConcurrencyStamp { get; }
			}

			public interface IUpdateResultModel : IReadOnlyConcurrencyStamp
			{
			}

			public record UserModel : IUpdateResultModel
			{
				public required string ConcurrencyStamp { get; init; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task MutableStampReachedOnlyViaReadOnlyInterface_ShouldTriggerUA013()
	{
		// No interface in the closure declares a setter, so the suppression does not apply. This is the pressure
		// that moves result models onto init accessors.
		const string source = """
			public interface IReadOnlyConcurrencyStamp
			{
				string ConcurrencyStamp { get; }
			}

			public interface IUpdateResultModel : IReadOnlyConcurrencyStamp
			{
			}

			public record UserModel : IUpdateResultModel
			{
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 12, 25, "ConcurrencyStamp", "UserModel"));
	}

	[Fact]
	public async Task MutableStampViaReadOnlyInterfaceWithAllowMutableProperty_ShouldNotTriggerUA013()
	{
		// The documented escape hatch for a type that genuinely cannot move to init.
		const string source = """
			using Umbrella.Analyzers;

			public interface IReadOnlyConcurrencyStamp
			{
				string ConcurrencyStamp { get; }
			}

			public interface IUpdateResultModel : IReadOnlyConcurrencyStamp
			{
			}

			public record UserModel : IUpdateResultModel
			{
				[UmbrellaAllowMutableProperty("Assigned by a lifecycle hook.")]
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task PropertyImplementingInterfaceSetter_ShouldStillTriggerUA012()
	{
		const string source = """
			public interface IConcurrencyStamp
			{
				string ConcurrencyStamp { get; set; }
			}

			public record UserModel : IConcurrencyStamp
			{
				public string ConcurrencyStamp { get; set; } = "";
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule, 8, 16, "ConcurrencyStamp", "UserModel"));
	}

	[Fact]
	public async Task MutablePropertyImplementingGetterOnlyInterface_ShouldTriggerUA013()
	{
		const string source = """
			public interface IKeyedItem
			{
				int Id { get; }
			}

			public record UserModel : IKeyedItem
			{
				public required int Id { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 8, 22, "Id", "UserModel"));
	}

	[Fact]
	public async Task MutablePropertyNotImplementingInterfaceSetter_ShouldTriggerUA013()
	{
		const string source = """
			public interface IConcurrencyStamp
			{
				string ConcurrencyStamp { get; set; }
			}

			public record UserModel
			{
				public required string ConcurrencyStamp { get; set; }
			}
			""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 8, 25, "ConcurrencyStamp", "UserModel"));
	}

	[Fact]
	public async Task ExpressionBodiedGetter_ShouldNotTriggerDiagnostic()
	{
		const string source = """
			public record UserModel
			{
				public string DisplayName => "User";
			}
			""";

		await VerifyNoDiagnosticsAsync(source);
	}
}
