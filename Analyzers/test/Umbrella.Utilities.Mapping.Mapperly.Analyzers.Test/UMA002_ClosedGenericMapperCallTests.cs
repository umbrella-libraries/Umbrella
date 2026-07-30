namespace Umbrella.Utilities.Mapping.Mapperly.Analyzers.Test;

public class UMA002_ClosedGenericMapperCallTests : AnalyzerTestBase<MapperlyRegistrationAnalyzer>
{
	private const string Infrastructure = """
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
        public UmbrellaMapperlyCatalogReferenceAttribute(Type catalogType)
        {
        }
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

public sealed class RequestA
{
}

public sealed class RequestB
{
}

public sealed class Response
{
}

""";

	[Fact]
	public async Task ClosedDerivedTypeWithRegisteredMapping_ProducesNoDiagnostic()
	{
		const string source = Infrastructure + """
[UmbrellaMapperlyCatalogMapping(typeof(RequestA), typeof(Response), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public virtual async Task ExecuteAsync(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase<RequestA, Response>
{
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ClosedDerivedTypeWithoutRegisteredMapping_ProducesConcreteUMA001()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public virtual async Task ExecuteAsync(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase<RequestA, Response>
{
}
""";

		(int line, int column) = FindLocation(source, "mapper.MapAsync");
		await VerifyAnalyzerAsync(
			source,
			Diagnostic(
				MapperlyRegistrationAnalyzer.MissingExactMappingRule,
				line,
				column,
				"new-instance",
				"RequestA",
				"Response"));
	}

	[Fact]
	public async Task MultipleClosedDerivedTypes_ReportOnlyMissingConcreteMapping()
	{
		const string source = Infrastructure + """
[UmbrellaMapperlyCatalogMapping(typeof(RequestA), typeof(Response), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public virtual async Task ExecuteAsync(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

public sealed class ConsumerA : ConsumerBase<RequestA, Response>
{
}

public sealed class ConsumerB : ConsumerBase<RequestB, Response>
{
}
""";

		(int line, int column) = FindLocation(source, "mapper.MapAsync");
		await VerifyAnalyzerAsync(
			source,
			Diagnostic(
				MapperlyRegistrationAnalyzer.MissingExactMappingRule,
				line,
				column,
				"new-instance",
				"RequestB",
				"Response"));
	}

	[Fact]
	public async Task ClosedDerivedTypeThatOverridesContainingMethod_IsExcluded()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public virtual async Task ExecuteAsync(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase<RequestA, Response>
{
    public override Task ExecuteAsync(IUmbrellaMapper mapper, RequestA request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task OnlyOpenDerivedType_RemainsUMA002()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public virtual async Task ExecuteAsync(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, TResponse>(request, cancellationToken);
    }
}

public class OpenConsumer<TRequest> : ConsumerBase<TRequest, Response>
{
}
""";

		(int line, int column) = FindLocation(source, "mapper.MapAsync");
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.OpenGenericMapperCallRule, line, column));
	}

	[Fact]
	public async Task MethodTypeParameterThatCannotBeSubstituted_RemainsUMA002()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase
{
    public virtual async Task ExecuteAsync<TRequest>(IUmbrellaMapper mapper, TRequest request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<TRequest, Response>(request, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase
{
}
""";

		(int line, int column) = FindLocation(source, "mapper.MapAsync");
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.OpenGenericMapperCallRule, line, column));
	}

	[Fact]
	public async Task NestedGenericAndArrayTypeArguments_AreSubstituted()
	{
		const string source = Infrastructure + """
public sealed class Envelope<T>
{
}

[UmbrellaMapperlyCatalogMapping(typeof(Envelope<RequestA[]>), typeof(Response), UmbrellaMapperlyCatalogOperationKind.NewInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest>
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, Envelope<TRequest[]> request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<Envelope<TRequest[]>, Response>(request, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase<RequestA>
{
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task CollectionAndExistingInstanceOperations_AreValidatedForClosedDerivedTypes()
	{
		const string source = Infrastructure + """
[UmbrellaMapperlyCatalogMapping(typeof(RequestA), typeof(Response), UmbrellaMapperlyCatalogOperationKind.NewCollection)]
[UmbrellaMapperlyCatalogMapping(typeof(RequestA), typeof(Response), UmbrellaMapperlyCatalogOperationKind.ExistingInstance)]
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public abstract class ConsumerBase<TRequest, TResponse>
{
    public async Task ExecuteAsync(IUmbrellaMapper mapper, IEnumerable<TRequest> requests, TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        await mapper.MapAllAsync<TRequest, TResponse>(requests, cancellationToken);
        await mapper.MapAsync<TRequest, TResponse>(request, response, cancellationToken);
    }
}

public sealed class Consumer : ConsumerBase<RequestA, Response>
{
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task ConcreteMapperImplementationCallWithoutRegistration_ProducesUMA001()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Mapper : IUmbrellaMapper
{
    public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

public sealed class Consumer
{
    public async Task ExecuteAsync(Mapper mapper, RequestA request, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<RequestA, Response>(request, cancellationToken);
    }
}
""";

		(int line, int column) = FindLocation(source, "mapper.MapAsync");
		await VerifyAnalyzerAsync(source, Diagnostic(MapperlyRegistrationAnalyzer.MissingExactMappingRule, line, column));
	}

	[Fact]
	public async Task UnrelatedSameNamedOverloadOnMapperImplementation_IsIgnored()
	{
		const string source = Infrastructure + """
public sealed class TestCatalog : IUmbrellaMapperlyCatalog
{
}

public sealed class Mapper : IUmbrellaMapper
{
    public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<IReadOnlyCollection<TDestination>> MapAllAsync<TSource, TDestination>(IEnumerable<TSource> source, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<TDestination> MapAsync<TSource, TDestination>(TSource source, TDestination destination, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public ValueTask<TDestination> MapAsync<TSource, TDestination>(int source, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

public sealed class Consumer
{
    public async Task ExecuteAsync(Mapper mapper, CancellationToken cancellationToken)
    {
        await mapper.MapAsync<RequestA, Response>(42, cancellationToken);
    }
}
""";

		await VerifyNoDiagnosticsAsync(source);
	}

	private static (int Line, int Column) FindLocation(string source, string marker)
	{
		int index = source.IndexOf(marker, StringComparison.Ordinal);
		Assert.True(index >= 0);

		int line = source.Take(index).Count(x => x == '\n') + 1;
		int lastNewLine = source.LastIndexOf('\n', index);
		int column = index - lastNewLine;
		return (line, column);
	}
}
