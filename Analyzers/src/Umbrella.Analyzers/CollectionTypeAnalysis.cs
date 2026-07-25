using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Umbrella.Analyzers;

internal sealed class CollectionTypeAnalysis
{
	private const string FilterExpressionMetadataName = "Umbrella.Utilities.Data.Filtering.FilterExpression`1";
	private const string ServiceCollectionMetadataName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
	private const string SortExpressionMetadataName = "Umbrella.Utilities.Data.Sorting.SortExpression`1";

	private static readonly ImmutableArray<string> _enumerableContractMetadataNames =
	[
		"System.Collections.IEnumerable",
		"System.Collections.Generic.IEnumerable`1",
		"System.Collections.Generic.IAsyncEnumerable`1"
	];

	private static readonly ImmutableArray<string> _mutableContractMetadataNames =
	[
		"System.Collections.IList",
		"System.Collections.IDictionary",
		"System.Collections.Generic.ICollection`1",
		"System.Collections.Generic.IList`1",
		"System.Collections.Generic.ISet`1",
		"System.Collections.Generic.IDictionary`2",
		"System.Collections.Concurrent.IProducerConsumerCollection`1"
	];

	private static readonly ImmutableArray<string> _knownReadOnlyConcreteTypeMetadataNames =
	[
		"System.Collections.ObjectModel.ReadOnlyCollection`1",
		"System.Collections.ObjectModel.ReadOnlyDictionary`2",
		"System.Collections.ObjectModel.ReadOnlyObservableCollection`1",
		"System.Collections.Immutable.ImmutableArray`1",
		"System.Collections.Immutable.ImmutableList`1",
		"System.Collections.Immutable.ImmutableHashSet`1",
		"System.Collections.Immutable.ImmutableSortedSet`1",
		"System.Collections.Immutable.ImmutableDictionary`2",
		"System.Collections.Immutable.ImmutableSortedDictionary`2",
		"System.Collections.Immutable.ImmutableQueue`1",
		"System.Collections.Immutable.ImmutableStack`1",
		"System.Collections.Frozen.FrozenSet`1",
		"System.Collections.Frozen.FrozenDictionary`2"
	];

	private readonly ImmutableArray<INamedTypeSymbol> _enumerableContracts;
	private readonly ImmutableArray<INamedTypeSymbol> _mutableContracts;
	private readonly ImmutableArray<INamedTypeSymbol> _knownReadOnlyConcreteTypes;
	private readonly INamedTypeSymbol? _filterExpressionType;
	private readonly INamedTypeSymbol? _serviceCollectionType;
	private readonly INamedTypeSymbol? _sortExpressionType;

	internal CollectionTypeAnalysis(Compilation compilation)
	{
		_enumerableContracts = ResolveTypes(compilation, _enumerableContractMetadataNames);
		_mutableContracts = ResolveTypes(compilation, _mutableContractMetadataNames);
		_knownReadOnlyConcreteTypes = ResolveTypes(compilation, _knownReadOnlyConcreteTypeMetadataNames);
		_filterExpressionType = compilation.GetTypeByMetadataName(FilterExpressionMetadataName);
		_serviceCollectionType = compilation.GetTypeByMetadataName(ServiceCollectionMetadataName);
		_sortExpressionType = compilation.GetTypeByMetadataName(SortExpressionMetadataName);
	}

	internal bool IsCollectionType(ITypeSymbol type)
	{
		if (type.SpecialType == SpecialType.System_String)
			return false;

		if (type is IArrayTypeSymbol)
			return true;

		if (type is ITypeParameterSymbol typeParameter)
			return typeParameter.ConstraintTypes.Any(IsCollectionType);

		return MatchesTypeOrInterface(type, _enumerableContracts);
	}

	internal bool IsReadOnlyCollectionType(ITypeSymbol type)
	{
		if (!IsCollectionType(type) || type is IArrayTypeSymbol)
			return false;

		if (type is ITypeParameterSymbol typeParameter)
		{
			ITypeSymbol[] collectionConstraints = [.. typeParameter.ConstraintTypes.Where(IsCollectionType)];
			return collectionConstraints.Length > 0 && collectionConstraints.All(IsReadOnlyCollectionType);
		}

		if (type.TypeKind == TypeKind.Interface)
			return !MatchesTypeOrInterface(type, _mutableContracts);

		return IsKnownReadOnlyConcreteType(type);
	}

	internal bool IsAllowedExpressionArray(ITypeSymbol type)
	{
		if (type is not IArrayTypeSymbol { Rank: 1, IsSZArray: true } arrayType ||
			arrayType.ElementType is not INamedTypeSymbol elementType)
		{
			return false;
		}

		var elementDefinition = elementType.OriginalDefinition;

		return SymbolEqualityComparer.Default.Equals(elementDefinition, _filterExpressionType) ||
			SymbolEqualityComparer.Default.Equals(elementDefinition, _sortExpressionType);
	}

	internal static bool IsBinaryBuffer(ITypeSymbol type) =>
		type is IArrayTypeSymbol
		{
			Rank: 1,
			IsSZArray: true,
			ElementType.SpecialType: SpecialType.System_Byte
		};

	internal bool IsServiceCollectionContract(ITypeSymbol type) =>
		type is INamedTypeSymbol namedType &&
		SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, _serviceCollectionType);

	internal ImmutableArray<ITypeSymbol> GetCollectionPayloadTypes(ITypeSymbol type)
	{
		var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
		var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.IncludeNullability);
		CollectCollectionPayloadTypes(type, builder, visited);
		return builder.ToImmutable();
	}

	private void CollectCollectionPayloadTypes(
		ITypeSymbol type,
		ImmutableArray<ITypeSymbol>.Builder builder,
		HashSet<ITypeSymbol> visited)
	{
		if (!visited.Add(type))
			return;

		if (IsCollectionType(type))
		{
			builder.Add(type);
			return;
		}

		if (type.TypeKind == TypeKind.Delegate)
			return;

		if (type is ITypeParameterSymbol typeParameter)
		{
			foreach (var constraintType in typeParameter.ConstraintTypes)
			{
				CollectCollectionPayloadTypes(constraintType, builder, visited);
			}

			return;
		}

		if (type is not INamedTypeSymbol namedType)
			return;

		if (namedType.IsTupleType)
		{
			foreach (var tupleElement in namedType.TupleElements)
			{
				CollectCollectionPayloadTypes(tupleElement.Type, builder, visited);
			}

			return;
		}

		foreach (var typeArgument in namedType.TypeArguments)
		{
			CollectCollectionPayloadTypes(typeArgument, builder, visited);
		}
	}

	private bool IsKnownReadOnlyConcreteType(ITypeSymbol type)
	{
		if (type is not INamedTypeSymbol namedType)
			return false;

		for (var currentType = namedType; currentType is not null; currentType = currentType.BaseType)
		{
			if (MatchesOriginalDefinition(currentType, _knownReadOnlyConcreteTypes))
				return true;
		}

		return false;
	}

	private static bool MatchesTypeOrInterface(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> definitions)
	{
		if (MatchesOriginalDefinition(type, definitions))
			return true;

		return type.AllInterfaces.Any(interfaceType => MatchesOriginalDefinition(interfaceType, definitions));
	}

	private static bool MatchesOriginalDefinition(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> definitions)
	{
		if (type is not INamedTypeSymbol namedType)
			return false;

		return definitions.Any(definition =>
			SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, definition));
	}

	private static ImmutableArray<INamedTypeSymbol> ResolveTypes(
		Compilation compilation,
		ImmutableArray<string> metadataNames)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

		foreach (string metadataName in metadataNames)
		{
			if (compilation.GetTypeByMetadataName(metadataName) is { } type)
				builder.Add(type);
		}

		return builder.ToImmutable();
	}
}
