using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Umbrella.Utilities.Mapping.Mapperly.Analyzers.Test;

public class UMA001_MapperlyRegistrationAnalyzerTests : AnalyzerTestBase<MapperlyRegistrationAnalyzer>
{
	[Fact]
	public async Task VerifyNoDiagnosticsAsyncWhenExactNewInstanceMappingExists()
	{
		await VerifyNoDiagnosticsAsync("""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(TestCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class Person
{
}

public sealed class PersonDto
{
}

[UmbrellaMapperlyCatalogMapping(typeof(Person), typeof(PersonDto), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Consumer
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Person person, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<Person, PersonDto>(person, cancellationToken);
    }
}
""");
	}

	[Fact]
	public async Task VerifyAnalyzerAsyncWhenExactNewInstanceMappingIsMissing()
	{
		await VerifyAnalyzerAsync("""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(TestCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class Person
{
}

public sealed class PersonDto
{
}

[UmbrellaMapperlyCatalogMapping(typeof(Person), typeof(PersonDto), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Account
{
}

public sealed class Consumer
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Account account, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<Account, PersonDto>(account, cancellationToken);
    }
}
""", Diagnostic(MapperlyRegistrationAnalyzer.MissingExactMappingRule, 70, 15));
	}

	[Fact]
	public async Task VerifyAnalyzerAsyncWhenWrongOperationKindIsRegistered()
	{
		await VerifyAnalyzerAsync("""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(TestCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class Person
{
}

public sealed class PersonDto
{
}

[UmbrellaMapperlyCatalogMapping(typeof(Person), typeof(PersonDto), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Consumer
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Person person, PersonDto destination, CancellationToken cancellationToken)
    {
        await mapper.MapAsync(person, destination, cancellationToken);
    }
}
""", Diagnostic(MapperlyRegistrationAnalyzer.MissingExactMappingRule, 66, 15));
	}

	[Fact]
	public async Task VerifyAnalyzerAsyncWhenSourceTypeArgumentIsOpenGeneric()
	{
		await VerifyAnalyzerAsync("""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(TestCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class AppUser
{
}

[UmbrellaMapperlyCatalogMapping(typeof(AppUser), typeof(AppUser), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Consumer<TCreateAccountModel>
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, TCreateAccountModel model, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TCreateAccountModel, AppUser>(model, cancellationToken);
    }
}
""", Diagnostic(MapperlyRegistrationAnalyzer.OpenGenericMapperCallRule, 62, 15));
	}

	[Fact]
	public async Task VerifyAnalyzerAsyncWhenDestinationTypeArgumentIsOpenGeneric()
	{
		await VerifyAnalyzerAsync("""
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(TestCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class AppUser
{
}

[UmbrellaMapperlyCatalogMapping(typeof(AppUser), typeof(AppUser), UmbrellaMapperlyCatalogOperationKind.NewCollection)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Consumer<TSlimAccountModel>
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, IEnumerable<AppUser> results, CancellationToken cancellationToken)
    {
        await mapper.MapAllAsync<AppUser, TSlimAccountModel>(results, cancellationToken);
    }
}
""", Diagnostic(MapperlyRegistrationAnalyzer.OpenGenericMapperCallRule, 62, 15));
	}

	[Fact]
	public async Task VerifyNoDiagnosticsAsyncWhenNoCatalogsAreConfigured()
	{
		await VerifyNoDiagnosticsAsync("""
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public interface IUmbrellaMapperlyCatalog
    {
    }
}

public sealed class Person
{
}

public sealed class PersonDto
{
}

public sealed class Consumer
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Person person, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<Person, PersonDto>(person, cancellationToken);
    }
}
""");
	}

	[Fact]
	public async Task VerifyNoDiagnosticsAsyncWhenMappingComesFromReferencedCatalogAssembly()
	{
		const string referencedAssemblySource = """
using System;

namespace Umbrella.Utilities.Mapping.Mapperly.Abstractions
{
    public enum UmbrellaMapperlyCatalogOperationKind
    {
        NewInstance,
        NewCollection,
        ExistingInstance
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class UmbrellaMapperlyCatalogReferenceAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType) => CatalogType = catalogType;
        public Type CatalogType { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class UmbrellaMapperlyCatalogMappingAttribute : Attribute
    {
        public UmbrellaMapperlyCatalogMappingAttribute(Type sourceType, Type destinationType, UmbrellaMapperlyCatalogOperationKind operationKind)
        {
        }
    }

    public interface IUmbrellaMapperlyCatalog
    {
    }
}

namespace ReferenceMappings
{
    public sealed class Person
    {
    }

    public sealed class PersonDto
    {
    }

    [Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMapping(typeof(Person), typeof(PersonDto), Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogOperationKind.NewInstance)]
    public sealed class ExternalCatalog : Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyCatalog
    {
    }
}
""";

		const string consumerSource = """
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReferenceMappings;
using Umbrella.Utilities.Mapping.Abstractions;
using Umbrella.Utilities.Mapping.Mapperly.Abstractions;

[assembly: UmbrellaMapperlyCatalogReference(typeof(ReferenceMappings.ExternalCatalog))]

namespace Umbrella.Utilities.Mapping.Abstractions
{
    public interface IUmbrellaMapper
    {
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default);
        ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default);
        ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default);
    }
}

public sealed class Consumer
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Person person, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<Person, PersonDto>(person, cancellationToken);
    }
}
""";

		MetadataReference referencedAssembly = CreateMetadataReference(referencedAssemblySource, "ReferenceMappings");

		await VerifyNoDiagnosticsAsync(consumerSource, [referencedAssembly]);
	}

	private static PortableExecutableReference CreateMetadataReference(string source, string assemblyName)
	{
		CSharpCompilation compilation = CreateCompilationForReference(source, assemblyName);

		using var stream = new MemoryStream();
		var emitResult = compilation.Emit(stream);

		Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics.Select(x => x.ToString())));

		_ = stream.Seek(0, SeekOrigin.Begin);

		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	private static CSharpCompilation CreateCompilationForReference(string source, string assemblyName)
	{
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
			assemblyName,
			[CSharpSyntaxTree.ParseText(source, path: $"{assemblyName}.cs")],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
	}
}
