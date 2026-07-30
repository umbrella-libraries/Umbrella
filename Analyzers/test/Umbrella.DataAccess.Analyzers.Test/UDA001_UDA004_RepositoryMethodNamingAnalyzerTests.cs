using Microsoft.CodeAnalysis;

namespace Umbrella.DataAccess.Analyzers.Test;

public class UDA001_UDA004_RepositoryMethodNamingAnalyzerTests : AnalyzerTestBase<RepositoryMethodNamingAnalyzer>
{
	[Fact]
	public async Task SingleItemQuery_NamedGetBy_ReportsUDA001()
	{
		string source = CreateSource("public Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindByRule, source, "GetByIdAsync"));
	}

	[Theory]
	[InlineData("public Task<object?> HelloAsync() => Task.FromResult<object?>(null);", "UDA001")]
	[InlineData("public Task<IReadOnlyCollection<object>> HelloAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);", "UDA002")]
	[InlineData("public Task<int> HelloAsync() => Task.FromResult(0);", "UDA003")]
	[InlineData("public Task<bool> HelloAsync() => Task.FromResult(true);", "UDA004")]
	[InlineData("public Task<object?> BananaAsync() => Task.FromResult<object?>(null);", "UDA001")]
	public async Task ArbitraryMethodName_IsValidatedFromItsReturnShape(string member, string diagnosticId)
	{
		ArgumentNullException.ThrowIfNull(member);

		string source = CreateSource(member);
		var rule = diagnosticId switch
		{
			"UDA001" => RepositoryMethodNamingAnalyzer.FindByRule,
			"UDA002" => RepositoryMethodNamingAnalyzer.FindAllByRule,
			"UDA003" => RepositoryMethodNamingAnalyzer.FindCountRule,
			"UDA004" => RepositoryMethodNamingAnalyzer.ExistsRule,
			_ => throw new InvalidOperationException($"Unexpected diagnostic ID '{diagnosticId}'.")
		};

		string methodName = member.Contains("HelloAsync", StringComparison.Ordinal) ? "HelloAsync" : "BananaAsync";
		await VerifyAnalyzerAsync(source, Expected(rule, source, methodName));
	}

	[Theory]
	[InlineData("public Task<object?> FindByIdAsync(int id) => Task.FromResult<object?>(null);")]
	[InlineData("public Task<object?> FindAsync() => Task.FromResult<object?>(null);")]
	[InlineData("public Task<(int outstanding, int total)> FindActionMetricsByUserIdAsync() => Task.FromResult((0, 0));")]
	public async Task ValidSingleItemQueryNames_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Theory]
	[InlineData("public Task<object> AddItemAsync() => Task.FromResult(new object());")]
	[InlineData("public Task<object> CreateItemAsync() => Task.FromResult(new object());")]
	[InlineData("public Task<IOperationResult<object>> UpdateItemAsync() => Task.FromResult<IOperationResult<object>>(null!);")]
	[InlineData("public Task<object> ReloadAsync() => Task.FromResult(new object());")]
	[InlineData("public Task<IReadOnlyCollection<object>> ExportItemsAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("public Task<IReadOnlyCollection<object>> SaveItemsAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("public Task<int> IncrementCountAsync() => Task.FromResult(1);")]
	[InlineData("public Task<bool> SetActiveAsync() => Task.FromResult(true);")]
	[InlineData("public Task<object> ExecuteAsync() => Task.FromResult(new object());")]
	public async Task CommandMethods_ReturningPayloads_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Theory]
	[InlineData("public Task<object?> UpdaterAsync() => Task.FromResult<object?>(null);", "UpdaterAsync")]
	[InlineData("public Task<object?> SavedSearchAsync() => Task.FromResult<object?>(null);", "SavedSearchAsync")]
	public async Task CommandPrefixMustBeACompletePascalCaseWord(string member, string methodName)
	{
		string source = CreateSource(member);
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindByRule, source, methodName));
	}

	[Fact]
	public async Task QueryReturningOperationResult_NamedGetBy_ReportsUDA001()
	{
		string source = CreateSource("public Task<IOperationResult<object>> GetByIdAsync(int id) => Task.FromResult<IOperationResult<object>>(null!);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindByRule, source, "GetByIdAsync"));
	}

	[Fact]
	public async Task CollectionQuery_NamedGetAll_ReportsUDA002()
	{
		string source = CreateSource("public Task<IReadOnlyCollection<object>> GetAllAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindAllByRule, source, "GetAllAsync"));
	}

	[Theory]
	[InlineData("public Task<IReadOnlyCollection<object>> FindAllByStatusAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("public Task<IReadOnlyCollection<object>> FindAllNameByIdListAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("public Task<IReadOnlyCollection<object>> FindAllMostPopularSlimHitAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);")]
	[InlineData("public Task<IReadOnlyDictionary<int, object>> FindAllAggregateDataAsync() => Task.FromResult<IReadOnlyDictionary<int, object>>(new Dictionary<int, object>());")]
	[InlineData("public Task<(IReadOnlyCollection<object> items, int total)> FindAllRecentAsync() => Task.FromResult<(IReadOnlyCollection<object>, int)>(([], 0));")]
	public async Task ValidCollectionQueryNamesAndShapes_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Fact]
	public async Task CollectionQuery_WithoutAll_ReportsUDA002()
	{
		string source = CreateSource("public Task<IReadOnlyCollection<object>> FindMostRecentSlimAsync() => Task.FromResult<IReadOnlyCollection<object>>([]);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindAllByRule, source, "FindMostRecentSlimAsync"));
	}

	[Fact]
	public async Task DerivedPaginatedResult_IsClassifiedAsCollection()
	{
		const string additionalTypes = """
namespace TestApp
{
	public sealed record ThingPage : PaginatedResultModel<object>;
}
""";
		string source = CreateSource(
			"public Task<ThingPage> GetPageAsync() => Task.FromResult(new ThingPage());",
			additionalTypes: additionalTypes);

		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindAllByRule, source, "GetPageAsync"));
	}

	[Fact]
	public async Task CountQuery_NamedCountBy_ReportsUDA003()
	{
		string source = CreateSource("public Task<int> CountByStatusAsync() => Task.FromResult(0);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindCountRule, source, "CountByStatusAsync"));
	}

	[Theory]
	[InlineData("public Task<int> FindCountByStatusAsync() => Task.FromResult(0);")]
	[InlineData("public Task<int> FindUnreadMessageCountByRecipientIdAsync() => Task.FromResult(0);")]
	public async Task ValidCountQueryNames_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Fact]
	public async Task BooleanQuery_NamedIsActive_ReportsUDA004()
	{
		string source = CreateSource("public Task<bool> IsActiveAsync() => Task.FromResult(true);");
		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.ExistsRule, source, "IsActiveAsync"));
	}

	[Fact]
	public async Task BooleanQuery_NamedExists_ProducesNoDiagnostic()
	{
		await VerifyNoDiagnosticsAsync(CreateSource("public Task<bool> ExistsByEmailAsync() => Task.FromResult(true);"));
	}

	[Fact]
	public async Task BooleanCommand_ProducesNoDiagnostic()
	{
		await VerifyNoDiagnosticsAsync(CreateSource("public Task<bool> UpdateStatusAsync() => Task.FromResult(true);"));
	}

	[Fact]
	public async Task AbstractDeveloperOwnedQuery_IsAnalyzed()
	{
		string source = CreateSource(
			"public abstract Task<object?> GetByIdAsync(int id);",
			typeDeclaration: "public abstract class ThingRepository");

		await VerifyAnalyzerAsync(source, Expected(RepositoryMethodNamingAnalyzer.FindByRule, source, "GetByIdAsync"));
	}

	[Theory]
	[InlineData("public override Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);")]
	[InlineData("public static Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);")]
	[InlineData("protected Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);")]
	[InlineData("public Task DeleteByIdAsync(int id) => Task.CompletedTask;")]
	[InlineData("public void Reset() { }")]
	public async Task ExcludedMethodShapes_ProduceNoDiagnostic(string member)
	{
		await VerifyNoDiagnosticsAsync(CreateSource(member));
	}

	[Fact]
	public async Task SameShortBaseTypeNameInAnotherNamespace_ProducesNoDiagnostic()
	{
		const string source = """
using System.Threading.Tasks;

namespace Other
{
	public abstract class GenericDbRepository<T>;
}

namespace TestApp
{
	public sealed class ThingRepository : Other.GenericDbRepository<object>
	{
		public Task<object?> GetByIdAsync() => Task.FromResult<object?>(null);
	}
}
""";
		await VerifyNoDiagnosticsAsync(source);
	}

	[Fact]
	public async Task IQueryableReturn_IsLeftExclusivelyToUDA005()
	{
		await VerifyNoDiagnosticsAsync(CreateSource("public IQueryable<object> GetItems() => null!;"));
	}

	private static string CreateSource(
		string member,
		string typeDeclaration = "public class ThingRepository",
		string baseType = "GenericDbRepository<object>",
		string additionalTypes = "")
	{
		return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbrella.DataAccess.EntityFrameworkCore;
using Umbrella.Utilities.Data.Pagination;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.DataAccess.EntityFrameworkCore
{
	public abstract class ReadOnlyGenericDbRepository<TEntity>
	{
		public virtual Task<object?> GetByIdAsync(int id) => Task.FromResult<object?>(null);
	}

	public abstract class GenericDbRepository<TEntity> : ReadOnlyGenericDbRepository<TEntity>;
}

namespace Umbrella.Utilities.Data.Pagination
{
	public record PaginatedResultModel<T>;
}

namespace Umbrella.Utilities.Primitives.Abstractions
{
	public interface IOperationResult<T>;
}

{{additionalTypes}}

namespace TestApp
{
	{{typeDeclaration}} : {{baseType}}
	{
		{{member}}
	}
}
""";
	}

	private static ExpectedDiagnostic Expected(DiagnosticDescriptor rule, string source, string methodName)
	{
		string[] lines = source.Split('\n');

		for (int i = lines.Length - 1; i >= 0; i--)
		{
			int column = lines[i].IndexOf(methodName, StringComparison.Ordinal);

			if (column >= 0)
				return Diagnostic(rule, i + 1, column + 1, methodName);
		}

		throw new InvalidOperationException($"Method '{methodName}' was not found in the test source.");
	}
}
