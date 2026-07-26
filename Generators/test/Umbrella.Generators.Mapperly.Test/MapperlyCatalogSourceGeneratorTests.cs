using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Umbrella.Generators.Mapperly.Test;

public class MapperlyCatalogSourceGeneratorTests
{
	[Fact]
	public void GenerateCatalogEmitsExpectedServiceAndMappingRegistrations()
	{
		const string source = """
using System.Collections.Generic;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

public sealed class Source
{
}

public sealed class Destination
{
}

public sealed class Mapper : IUmbrellaMapperlyMapper<Source, Destination>
{
    public Destination Map(Source source) => new();
    public void Map(Source source, Destination destination)
    {
    }

    public IReadOnlyCollection<Destination> MapAll(IEnumerable<Source> source) => System.Array.Empty<Destination>();
}
""";

		string generatedSource = GenerateSource(source);

		Assert.Contains("[assembly: global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogReference(typeof(global::Umbrella.Generated.Mapping.Mapperly.TestConsumerUmbrellaMapperlyCatalog))]", generatedSource, StringComparison.Ordinal);
		Assert.Contains("[global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMapping(typeof(global::Source), typeof(global::Destination), global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogOperationKind.NewInstance)]", generatedSource, StringComparison.Ordinal);
		Assert.Contains("[global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMapping(typeof(global::Source), typeof(global::Destination), global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogOperationKind.NewCollection)]", generatedSource, StringComparison.Ordinal);
		Assert.Contains("[global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMapping(typeof(global::Source), typeof(global::Destination), global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogOperationKind.ExistingInstance)]", generatedSource, StringComparison.Ordinal);
		Assert.Contains("sealed class TestConsumerUmbrellaMapperlyCatalog", generatedSource, StringComparison.Ordinal);
		Assert.Contains("ServiceCollectionServiceExtensions.AddSingleton<global::Mapper>(services);", generatedSource, StringComparison.Ordinal);
		Assert.Contains("builder.AddNewInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
		Assert.Contains("builder.AddNewCollection<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
		Assert.Contains("builder.AddExistingInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
	}

	[Fact]
	public void GenerateCatalogPrefersAsyncRegistrationsWhenBothVariantsExist()
	{
		const string source = """
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

public sealed class Source
{
}

public sealed class Destination
{
}

public sealed class Mapper : IUmbrellaMapperlyNewInstanceMapper<Source, Destination>, IUmbrellaMapperlyNewInstanceAsyncMapper<Source, Destination>
{
    public Destination Map(Source source) => new();

    public ValueTask<Destination> MapAsync(Source source, CancellationToken cancellationToken) => new(new Destination());
}
""";

		string generatedSource = GenerateSource(source);

		Assert.Contains("builder.AddAsyncNewInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
		Assert.DoesNotContain("builder.AddNewInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
	}

	[Fact]
	public void GenerateCatalogPrefersAsyncRegistrationsWhenBothVariantsExistForNewCollection()
	{
		const string source = """
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

public sealed class Source
{
}

public sealed class Destination
{
}

public sealed class Mapper : IUmbrellaMapperlyNewCollectionMapper<Source, Destination>, IUmbrellaMapperlyNewCollectionAsyncMapper<Source, Destination>
{
    public IReadOnlyCollection<Destination> MapAll(IEnumerable<Source> source) => System.Array.Empty<Destination>();

    public ValueTask<IReadOnlyCollection<Destination>> MapAllAsync(IEnumerable<Source> source, CancellationToken cancellationToken) => new(System.Array.Empty<Destination>());
}
""";

		string generatedSource = GenerateSource(source);

		Assert.Contains("builder.AddAsyncNewCollection<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
		Assert.DoesNotContain("builder.AddNewCollection<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
	}

	[Fact]
	public void GenerateCatalogPrefersAsyncRegistrationsWhenBothVariantsExistForExistingInstance()
	{
		const string source = """
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

public sealed class Source
{
}

public sealed class Destination
{
}

public sealed class Mapper : IUmbrellaMapperlyExistingInstanceMapper<Source, Destination>, IUmbrellaMapperlyExistingInstanceAsyncMapper<Source, Destination>
{
    public void Map(Source source, Destination destination) { }

    public ValueTask MapAsync(Source source, Destination destination, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
""";

		string generatedSource = GenerateSource(source);

		Assert.Contains("builder.AddAsyncExistingInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
		Assert.DoesNotContain("builder.AddExistingInstance<global::Mapper, global::Source, global::Destination>();", generatedSource, StringComparison.Ordinal);
	}

	private static string GenerateSource(string source)
	{
		CSharpCompilation compilation = CreateCompilation(source);
		var generator = new MapperlyCatalogSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Empty(diagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		Assert.Empty(runResult.Diagnostics);
		_ = Assert.Single(runResult.Results);
		_ = Assert.Single(runResult.Results[0].GeneratedSources);

		return runResult.Results[0].GeneratedSources[0].SourceText.ToString();
	}

	private static CSharpCompilation CreateCompilation(string source)
	{
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8));

		string[] referencePaths =
		[
			.. AppDomain.CurrentDomain.GetAssemblies()
				.Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
				.Select(x => x.Location),
			.. (((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [])
		];

		MetadataReference[] references = [.. referencePaths
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(x => MetadataReference.CreateFromFile(x))];

		return CSharpCompilation.Create(
			assemblyName: "TestConsumer",
			syntaxTrees: [syntaxTree],
			references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
	}
}
