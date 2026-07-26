using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Analyzer that prevents entity types (types implementing <c>IEntity&lt;TEntityKey&gt;</c>) from being used as
/// parameters or immediate collection elements on changeable public query contracts. Passing an entity as a
/// query parameter uses the entity as a
/// specification bag, which leaks persistence concerns into the query contract. Query methods must accept
/// identifiers, scalar values, or dedicated parameter types instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityQueryParameterAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA020";

	private const string IEntityMetadataName = "Umbrella.DataAccess.Abstractions.IEntity`1";
	private const string IEnumerableMetadataName = "System.Collections.Generic.IEnumerable`1";
	private const string IAsyncEnumerableMetadataName = "System.Collections.Generic.IAsyncEnumerable`1";

	private static readonly ImmutableHashSet<string> _queryMethodPrefixes = ImmutableHashSet.Create(
		StringComparer.Ordinal,
		"Find",
		"Get",
		"Search",
		"Lookup",
		"Fetch",
		"Query");

	/// <summary>
	/// The diagnostic rule for this analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		id: DiagnosticId,
		title: "Entity values must not be used as query criteria",
		messageFormat: "Parameter '{0}' supplies entity type '{1}' to a query method. Accept entity identifiers, scalar or value types, or a dedicated query criteria type instead.",
		category: "DataAccess",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Passing an entity or a sequence of entities to a query method uses persisted state as a specification bag. This couples the query contract to the entity shape and leaks persistence concerns. Accept identifiers, scalar or value types, or a dedicated query criteria type instead.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			INamedTypeSymbol? iEntitySymbol = compilationContext.Compilation.GetTypeByMetadataName(IEntityMetadataName);

			if (iEntitySymbol is null)
				return;

			INamedTypeSymbol? iEnumerableSymbol = compilationContext.Compilation.GetTypeByMetadataName(IEnumerableMetadataName);
			INamedTypeSymbol? iAsyncEnumerableSymbol = compilationContext.Compilation.GetTypeByMetadataName(IAsyncEnumerableMetadataName);
			var methodAnalysis = new ChangeablePublicMethodAnalysis(compilationContext.Compilation);

			compilationContext.RegisterSymbolAction(
				ctx => AnalyzeMethod(
					ctx,
					iEntitySymbol,
					iEnumerableSymbol,
					iAsyncEnumerableSymbol,
					methodAnalysis),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(
		SymbolAnalysisContext context,
		INamedTypeSymbol iEntitySymbol,
		INamedTypeSymbol? iEnumerableSymbol,
		INamedTypeSymbol? iAsyncEnumerableSymbol,
		ChangeablePublicMethodAnalysis methodAnalysis)
	{
		var method = (IMethodSymbol)context.Symbol;

		if (!methodAnalysis.IsEligible(method) || !IsQueryMethod(method.Name))
			return;

		foreach (var parameter in method.Parameters)
		{
			if (!TryGetEntityPayload(
					parameter.Type,
					iEntitySymbol,
					iEnumerableSymbol,
					iAsyncEnumerableSymbol,
					out ITypeSymbol entityType))
			{
				continue;
			}

			var location = parameter.Locations.FirstOrDefault();

			if (location is null)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(
				Rule,
				location,
				parameter.Name,
				entityType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}

	private static bool IsQueryMethod(string methodName)
	{
		foreach (string prefix in _queryMethodPrefixes)
		{
			if (!methodName.StartsWith(prefix, StringComparison.Ordinal))
				continue;

			if (methodName.Length == prefix.Length)
				return true;

			char boundary = methodName[prefix.Length];
			if (char.IsUpper(boundary) || char.IsDigit(boundary) || boundary == '_')
				return true;
		}

		return false;
	}

	private static bool TryGetEntityPayload(
		ITypeSymbol parameterType,
		INamedTypeSymbol iEntitySymbol,
		INamedTypeSymbol? iEnumerableSymbol,
		INamedTypeSymbol? iAsyncEnumerableSymbol,
		out ITypeSymbol entityType)
	{
		if (IsEntityType(
				parameterType,
				iEntitySymbol,
				new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
		{
			entityType = parameterType;
			return true;
		}

		if (parameterType is IArrayTypeSymbol arrayType &&
			IsEntityType(
				arrayType.ElementType,
				iEntitySymbol,
				new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
		{
			entityType = arrayType.ElementType;
			return true;
		}

		if (TryGetSequenceEntityType(
				parameterType,
				iEntitySymbol,
				iEnumerableSymbol,
				iAsyncEnumerableSymbol,
				new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default),
				out entityType))
		{
			return true;
		}

		entityType = null!;
		return false;
	}

	private static bool TryGetSequenceEntityType(
		ITypeSymbol type,
		INamedTypeSymbol iEntitySymbol,
		INamedTypeSymbol? iEnumerableSymbol,
		INamedTypeSymbol? iAsyncEnumerableSymbol,
		HashSet<ITypeSymbol> visited,
		out ITypeSymbol entityType)
	{
		if (!visited.Add(type))
		{
			entityType = null!;
			return false;
		}

		if (type is ITypeParameterSymbol typeParameter)
		{
			foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
			{
				if (TryGetSequenceEntityType(
					constraintType,
					iEntitySymbol,
					iEnumerableSymbol,
					iAsyncEnumerableSymbol,
					visited,
					out entityType))
				{
					return true;
				}
			}
		}

		if (type is INamedTypeSymbol namedType)
		{
			if (IsEntitySequence(namedType, iEntitySymbol, iEnumerableSymbol, iAsyncEnumerableSymbol, out entityType))
				return true;

			foreach (INamedTypeSymbol interfaceType in namedType.AllInterfaces)
			{
				if (IsEntitySequence(interfaceType, iEntitySymbol, iEnumerableSymbol, iAsyncEnumerableSymbol, out entityType))
					return true;
			}
		}

		entityType = null!;
		return false;
	}

	private static bool IsEntitySequence(
		INamedTypeSymbol type,
		INamedTypeSymbol iEntitySymbol,
		INamedTypeSymbol? iEnumerableSymbol,
		INamedTypeSymbol? iAsyncEnumerableSymbol,
		out ITypeSymbol entityType)
	{
		if (type.TypeArguments.Length == 1 &&
			((iEnumerableSymbol is not null &&
					SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, iEnumerableSymbol)) ||
				(iAsyncEnumerableSymbol is not null &&
					SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, iAsyncEnumerableSymbol))) &&
			IsEntityType(
				type.TypeArguments[0],
				iEntitySymbol,
				new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
		{
			entityType = type.TypeArguments[0];
			return true;
		}

		entityType = null!;
		return false;
	}

	private static bool IsEntityType(
		ITypeSymbol type,
		INamedTypeSymbol iEntitySymbol,
		HashSet<ITypeSymbol> visited)
	{
		if (!visited.Add(type))
			return false;

		if (type is INamedTypeSymbol namedType)
		{
			if (SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, iEntitySymbol))
				return true;

			return namedType.AllInterfaces.Any(interfaceType =>
				SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, iEntitySymbol));
		}

		if (type is not ITypeParameterSymbol typeParameter)
			return false;

		return typeParameter.ConstraintTypes.Any(constraintType =>
			IsEntityType(constraintType, iEntitySymbol, visited));
	}
}
