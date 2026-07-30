namespace Umbrella.DataAccess.Analyzers.Test;

public class UDA005_RepositoryIQueryableAnalyzerTests : AnalyzerTestBase<RepositoryIQueryableAnalyzer>
{
	[Theory]
	[InlineData("public IQueryable<object> GetItems() => null!;", "GetItems")]
	[InlineData("public Task<IQueryable<object>> GetItemsAsync() => null!;", "GetItemsAsync")]
	[InlineData("public ValueTask<IQueryable<object>> GetItemsValueAsync() => default;", "GetItemsValueAsync")]
	[InlineData("public Task<Result<IQueryable<object>>> GetWrappedItemsAsync() => null!;", "GetWrappedItemsAsync")]
	[InlineData("public Task<(IQueryable<object> query, int count)> GetTupleAsync() => null!;", "GetTupleAsync")]
	[InlineData("public IOrderedQueryable<object> GetOrderedItems() => null!;", "GetOrderedItems")]
	[InlineData("public override IQueryable<object> Stream() => null!;", "Stream")]
	[InlineData("public IQueryable<object> HelloAsync() => null!;", "HelloAsync")]
	[InlineData("public IQueryable<object> CreateQuery() => null!;", "CreateQuery")]
	public async Task PublicQueryablePayload_ReportsUDA005(string member, string methodName)
	{
		string source = CreateSource(member);
		await VerifyAnalyzerAsync(source, Expected(source, methodName));
	}

	[Fact]
	public async Task ReadOnlyRepository_ReturningQueryable_ReportsUDA005()
	{
		string source = CreateSource(
			"public IQueryable<object> GetItems() => null!;",
			baseType: "ReadOnlyGenericDbRepository<object>");

		await VerifyAnalyzerAsync(source, Expected(source, "GetItems"));
	}

	[Theory]
	[InlineData("public Task<IReadOnlyCollection<object>> FindAllAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("protected IQueryable<object> GetItems() => null!;")]
	[InlineData("public Func<IQueryable<object>> CreateQueryFactory() => null!;")]
	public async Task AllowedReturnShapes_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Fact]
	public async Task NonRepositoryClass_ReturningQueryable_ProducesNoDiagnostic()
	{
		string source = CreateSource(
			"public IQueryable<object> GetItems() => null!;",
			baseType: "object");

		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task UnrelatedTypeNamedIQueryable_ProducesNoDiagnostic()
	{
		const string source = """
namespace Other
{
	public interface IQueryable<T>;
}

namespace Umbrella.DataAccess.EntityFrameworkCore
{
	public abstract class GenericDbRepository<T>;
}

namespace TestApp
{
	public sealed class ThingRepository : Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository<object>
	{
		public Other.IQueryable<object> GetItems() => null!;
	}
}
""";
		await VerifyNoDiagnosticsAsync(source);
	}

	private static string CreateSource(string member, string baseType = "GenericDbRepository<object>")
	{
		return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;

namespace Umbrella.DataAccess.EntityFrameworkCore
{
	public abstract class ReadOnlyGenericDbRepository<TEntity>
	{
		public virtual IQueryable<TEntity> Stream() => null!;
	}

	public abstract class GenericDbRepository<TEntity> : ReadOnlyGenericDbRepository<TEntity>;
}

namespace TestApp
{
	public sealed class Result<T>;

	public sealed class ThingRepository : {{baseType}}
	{
		{{member}}
	}
}
""";
	}

	private static ExpectedDiagnostic Expected(string source, string methodName)
	{
		string[] lines = source.Split('\n');

		for (int i = lines.Length - 1; i >= 0; i--)
		{
			int column = lines[i].IndexOf(methodName, StringComparison.Ordinal);

			if (column >= 0)
				return Diagnostic(RepositoryIQueryableAnalyzer.IQueryableForbiddenRule, i + 1, column + 1, methodName);
		}

		throw new InvalidOperationException($"Method '{methodName}' was not found in the test source.");
	}
}
