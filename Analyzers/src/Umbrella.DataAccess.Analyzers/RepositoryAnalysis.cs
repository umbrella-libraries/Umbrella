using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Umbrella.DataAccess.Analyzers;

internal sealed class RepositoryAnalysis
{
	private static readonly ImmutableArray<string> _repositoryBaseTypeMetadataNames =
	[
		"Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository`1",
		"Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository`2",
		"Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository`3",
		"Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository`4",
		"Umbrella.DataAccess.EntityFrameworkCore.GenericDbRepository`5",
		"Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository`1",
		"Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository`2",
		"Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository`3",
		"Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository`4",
		"Umbrella.DataAccess.EntityFrameworkCore.ReadOnlyGenericDbRepository`5",
		"Umbrella.DataAccess.EF6.GenericDbRepository`1",
		"Umbrella.DataAccess.EF6.GenericDbRepository`2",
		"Umbrella.DataAccess.EF6.GenericDbRepository`3",
		"Umbrella.DataAccess.EF6.GenericDbRepository`4",
		"Umbrella.DataAccess.EF6.GenericDbRepository`5",
		"Umbrella.DataAccess.EF6.ReadOnlyGenericDbRepository`1",
		"Umbrella.DataAccess.EF6.ReadOnlyGenericDbRepository`2",
		"Umbrella.DataAccess.EF6.ReadOnlyGenericDbRepository`3",
		"Umbrella.DataAccess.EF6.ReadOnlyGenericDbRepository`4",
		"Umbrella.DataAccess.EF6.ReadOnlyGenericDbRepository`5"
	];

	private static readonly ImmutableArray<string> _commandVerbPrefixes =
	[
		"Add",
		"Apply",
		"Archive",
		"Assign",
		"Attach",
		"Clear",
		"Create",
		"Delete",
		"Detach",
		"Execute",
		"Export",
		"Generate",
		"Handle",
		"Import",
		"Increment",
		"Insert",
		"Publish",
		"Record",
		"Refresh",
		"Reload",
		"Remove",
		"Reset",
		"Restore",
		"Run",
		"Save",
		"Send",
		"Set",
		"Synchronize",
		"Sync",
		"Unassign",
		"Update",
		"Upsert"
	];

	private readonly ImmutableArray<INamedTypeSymbol> _repositoryBaseTypes;
	private readonly INamedTypeSymbol? _taskOfT;
	private readonly INamedTypeSymbol? _valueTaskOfT;
	private readonly INamedTypeSymbol? _queryableOfT;
	private readonly INamedTypeSymbol? _enumerable;
	private readonly INamedTypeSymbol? _enumerableOfT;
	private readonly INamedTypeSymbol? _paginatedResultModelOfT;

	private RepositoryAnalysis(Compilation compilation)
	{
		var repositoryBaseTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

		foreach (string metadataName in _repositoryBaseTypeMetadataNames)
		{
			if (compilation.GetTypeByMetadataName(metadataName) is { } repositoryBaseType)
				repositoryBaseTypes.Add(repositoryBaseType);
		}

		_repositoryBaseTypes = repositoryBaseTypes.ToImmutable();
		_taskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
		_valueTaskOfT = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");
		_queryableOfT = compilation.GetTypeByMetadataName("System.Linq.IQueryable`1");
		_enumerable = compilation.GetTypeByMetadataName("System.Collections.IEnumerable");
		_enumerableOfT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
		_paginatedResultModelOfT = compilation.GetTypeByMetadataName("Umbrella.Utilities.Data.Pagination.PaginatedResultModel`1");
	}

	public static RepositoryAnalysis Create(Compilation compilation) => new(compilation);

	public bool IsRepositoryType(INamedTypeSymbol type)
	{
		for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
		{
			if (_repositoryBaseTypes.Any(x => SymbolEqualityComparer.Default.Equals(x, current.OriginalDefinition))
				|| IsSupportedRepositoryBaseType(current.OriginalDefinition))
			{
				return true;
			}
		}

		return false;
	}

	public RepositoryReturnTypeCategory ClassifyReturnType(ITypeSymbol returnType)
	{
		if (ContainsQueryable(returnType))
			return RepositoryReturnTypeCategory.Ignored;

		ITypeSymbol? payload = UnwrapAsyncReturnType(returnType);

		if (payload is null)
			return RepositoryReturnTypeCategory.Ignored;

		return ClassifyPayload(payload);
	}

	public bool ContainsQueryable(ITypeSymbol returnType)
	{
		ITypeSymbol? payload = UnwrapAsyncReturnType(returnType);
		return payload is not null && ContainsQueryableCore(payload, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
	}

	public static bool IsCommandMethodName(string methodName) =>
		_commandVerbPrefixes.Any(prefix => StartsWithWord(methodName, prefix));

	public static bool IsValidName(string methodName, RepositoryReturnTypeCategory category) =>
		category switch
		{
			RepositoryReturnTypeCategory.SingleItem =>
				StartsWithWord(methodName, "Find") && !StartsWithWord(methodName, "FindAll"),
			RepositoryReturnTypeCategory.Collection => StartsWithWord(methodName, "FindAll"),
			RepositoryReturnTypeCategory.Count =>
				StartsWithWord(methodName, "Find")
				&& methodName.IndexOf("Count", StringComparison.Ordinal) >= 0,
			RepositoryReturnTypeCategory.Exists => StartsWithWord(methodName, "Exists"),
			_ => true
		};

	private RepositoryReturnTypeCategory ClassifyPayload(ITypeSymbol type)
	{
		type = UnwrapNullable(type);

		if (type.SpecialType == SpecialType.System_Boolean)
			return RepositoryReturnTypeCategory.Exists;

		if (type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64)
			return RepositoryReturnTypeCategory.Count;

		if (IsCollection(type))
			return RepositoryReturnTypeCategory.Collection;

		if (type is INamedTypeSymbol { IsTupleType: true } tuple)
		{
			return tuple.TupleElements.Any(x => ClassifyPayload(x.Type) == RepositoryReturnTypeCategory.Collection)
				? RepositoryReturnTypeCategory.Collection
				: RepositoryReturnTypeCategory.SingleItem;
		}

		if (type is INamedTypeSymbol { TypeKind: not TypeKind.Delegate, Arity: 1 } wrapper)
		{
			RepositoryReturnTypeCategory wrappedCategory = ClassifyPayload(wrapper.TypeArguments[0]);

			if (wrappedCategory is RepositoryReturnTypeCategory.Collection
				or RepositoryReturnTypeCategory.Count
				or RepositoryReturnTypeCategory.Exists)
			{
				return wrappedCategory;
			}
		}

		return RepositoryReturnTypeCategory.SingleItem;
	}

	private bool IsCollection(ITypeSymbol type)
	{
		if (type.SpecialType == SpecialType.System_String)
			return false;

		if (type is IArrayTypeSymbol)
			return true;

		if (type is not INamedTypeSymbol named)
			return false;

		if (IsOrImplements(named, _enumerable) || IsOrImplements(named, _enumerableOfT))
			return true;

		for (INamedTypeSymbol? current = named; current is not null; current = current.BaseType)
		{
			if (_paginatedResultModelOfT is not null
				&& SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, _paginatedResultModelOfT))
			{
				return true;
			}
		}

		return false;
	}

	private bool ContainsQueryableCore(ITypeSymbol type, HashSet<ITypeSymbol> visited)
	{
		type = UnwrapNullable(type);

		if (!visited.Add(type))
			return false;

		if (type is IArrayTypeSymbol array)
			return ContainsQueryableCore(array.ElementType, visited);

		if (type is not INamedTypeSymbol named)
			return false;

		if (IsOrImplements(named, _queryableOfT)
			|| IsQueryableContract(named))
		{
			return true;
		}

		if (named.TypeKind == TypeKind.Delegate)
			return false;

		foreach (ITypeSymbol typeArgument in named.TypeArguments)
		{
			if (ContainsQueryableCore(typeArgument, visited))
				return true;
		}

		return false;
	}

	private ITypeSymbol? UnwrapAsyncReturnType(ITypeSymbol type)
	{
		if (type.SpecialType == SpecialType.System_Void)
			return null;

		if (type is not INamedTypeSymbol named)
			return type;

		if (named.Arity == 0
			&& named.Name is "Task" or "ValueTask"
			&& named.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
		{
			return null;
		}

		if ((_taskOfT is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _taskOfT))
			|| (_valueTaskOfT is not null && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, _valueTaskOfT)))
		{
			return named.TypeArguments[0];
		}

		return type;
	}

	private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
		type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: type;

	private static bool IsOrImplements(INamedTypeSymbol type, INamedTypeSymbol? contract)
	{
		if (contract is null)
			return false;

		if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, contract))
			return true;

		return type.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, contract));
	}

	private static bool IsSupportedRepositoryBaseType(INamedTypeSymbol type)
	{
		if (type.Name is not ("GenericDbRepository" or "ReadOnlyGenericDbRepository"))
			return false;

		string containingNamespace = type.ContainingNamespace.ToDisplayString();
		return containingNamespace is "Umbrella.DataAccess.EntityFrameworkCore" or "Umbrella.DataAccess.EF6";
	}

	private static bool IsQueryableContract(INamedTypeSymbol type)
	{
		if (type is { Name: "IQueryable", Arity: 1 }
			&& type.ContainingNamespace.ToDisplayString() == "System.Linq")
		{
			return true;
		}

		return type.AllInterfaces.Any(x =>
			x is { Name: "IQueryable", Arity: 1 }
			&& x.ContainingNamespace.ToDisplayString() == "System.Linq");
	}

	private static bool StartsWithWord(string value, string prefix)
	{
		if (!value.StartsWith(prefix, StringComparison.Ordinal))
			return false;

		return value.Length == prefix.Length || char.IsUpper(value[prefix.Length]);
	}
}

internal enum RepositoryReturnTypeCategory
{
	Ignored,
	SingleItem,
	Collection,
	Count,
	Exists
}
