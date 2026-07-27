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
