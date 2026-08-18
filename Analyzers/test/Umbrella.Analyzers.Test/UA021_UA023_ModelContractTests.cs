namespace Umbrella.Analyzers.Test;

public class UA021_UA023_ModelContractTests : AnalyzerTestBase<UmbrellaModelStandardsAnalyzer>
{
	[Fact]
	public void InputModelAttribute_ShouldNotBeInherited()
	{
		var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
			typeof(UmbrellaInputModelAttribute),
			typeof(AttributeUsageAttribute))!;

		Assert.False(usage.Inherited);
	}

	[Fact]
	public async Task ConcreteInputModel_ShouldAllowMutableConcurrencyStamp()
	{
		const string source = """
using Umbrella.Analyzers;

namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

[UmbrellaInputModel]
public sealed record UpdateUserModel : Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp
{
	public required string ConcurrencyStamp { get; set; }
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NonInputModel_WithMutableConcurrencyStamp_ShouldTriggerUA023()
	{
		const string source = """
namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

public sealed record UserModel : Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp
{
	public required string ConcurrencyStamp { get; set; }
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.NonInputModelMustUseReadOnlyConcurrencyStampRule, 7, 22, "UserModel"));
	}

	[Fact]
	public async Task NonInputModel_WithCustomMutableConcurrencyContract_ShouldTriggerUA023()
	{
		const string source = """
namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

public interface ICustomUpdateContract : Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp;

public sealed record UserModel : ICustomUpdateContract
{
	public required string ConcurrencyStamp { get; set; }
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.NonInputModelMustUseReadOnlyConcurrencyStampRule, 9, 22, "UserModel"));
	}

	[Fact]
	public async Task ReadOnlyConcurrencyStampModel_ShouldNotTriggerDiagnostic()
	{
		const string source = """
namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

public sealed record UserModel : Umbrella.Utilities.Data.Concurrency.IReadOnlyConcurrencyStamp
{
	public required string ConcurrencyStamp { get; init; }
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ReadOnlyConcurrencyStampModel_WithMutableSetter_ShouldTriggerUA013()
	{
		const string source = """
namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

public sealed record UserModel : Umbrella.Utilities.Data.Concurrency.IReadOnlyConcurrencyStamp
{
	public required string ConcurrencyStamp { get; set; }
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 9, 25, "ConcurrencyStamp", "UserModel"));
	}

	[Fact]
	public async Task PartialAbstractInputModel_ShouldReportTypeRulesOnce()
	{
		const string source = """
using Umbrella.Analyzers;

namespace Umbrella.Utilities.Data.Concurrency
{
	public interface IReadOnlyConcurrencyStamp { string ConcurrencyStamp { get; } }
	public interface IConcurrencyStamp : IReadOnlyConcurrencyStamp { new string ConcurrencyStamp { get; set; } }
}

[UmbrellaInputModel]
public abstract partial record UserModel : Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp;
public abstract partial record UserModel;
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.InputModelMustBeConcreteRule, 10, 32, "UserModel"),
			Diagnostic(UmbrellaModelStandardsAnalyzer.NonInputModelMustUseReadOnlyConcurrencyStampRule, 10, 32, "UserModel"));
	}

	[Fact]
	public async Task NonInputModel_InterfaceRequiredSetter_ShouldStillTriggerUA013()
	{
		const string source = """
public interface IMutableName
{
	string Name { get; set; }
}

public sealed record UserModel : IMutableName
{
	public required string Name { get; set; }
}
""";

		await VerifyAnalyzerAsync(
			source,
			Diagnostic(UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule, 8, 25, "Name", "UserModel"));
	}
}
