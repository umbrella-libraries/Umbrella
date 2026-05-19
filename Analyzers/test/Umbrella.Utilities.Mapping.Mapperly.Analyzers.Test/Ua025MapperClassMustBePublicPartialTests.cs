namespace Umbrella.Utilities.Mapping.Mapperly.Analyzers.Test;

public class Ua025MapperClassMustBePublicPartialTests : AnalyzerTestBase<MapperlyRegistrationAnalyzer>
{
	private const string MapperStub = @"using System;
namespace Riok.Mapperly.Abstractions
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapperAttribute : Attribute { }
}
";

	[Fact]
	public async Task InternalPartialClassWithMapperAttribute_ReportsDiagnostic()
	{
		const string source = MapperStub + @"namespace TestApp
{
    [Riok.Mapperly.Abstractions.Mapper]
    internal partial class MyMapper { }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.MapperClassMustBePublicPartialRule, 10, 28));
	}

	[Fact]
	public async Task PublicNonPartialClassWithMapperAttribute_ReportsDiagnostic()
	{
		const string source = MapperStub + @"namespace TestApp
{
    [Riok.Mapperly.Abstractions.Mapper]
    public class MyMapper { }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.MapperClassMustBePublicPartialRule, 10, 18));
	}

	[Fact]
	public async Task InternalNonPartialClassWithMapperAttribute_ReportsDiagnostic()
	{
		const string source = MapperStub + @"namespace TestApp
{
    [Riok.Mapperly.Abstractions.Mapper]
    internal class MyMapper { }
}";
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.MapperClassMustBePublicPartialRule, 10, 20));
	}

	[Fact]
	public async Task PublicPartialClassWithMapperAttribute_NoDiagnostic()
	{
		const string source = MapperStub + @"namespace TestApp
{
    [Riok.Mapperly.Abstractions.Mapper]
    public partial class MyMapper { }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task InternalPartialClassWithoutMapperAttribute_NoDiagnostic()
	{
		const string source = MapperStub + @"namespace TestApp
{
    internal partial class MyMapper { }
}";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task NoMapperAttributeInCompilation_NoDiagnostic()
	{
		const string source = @"namespace TestApp
{
    internal partial class MyMapper { }
}";
		await VerifyNoDiagnosticsAsync(source);
	}
}
