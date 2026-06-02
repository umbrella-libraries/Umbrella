using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Analyzer that prevents entity types (types implementing <c>IEntity&lt;TEntityKey&gt;</c>) from being used as
/// parameters to query or lookup methods. Passing an entity as a query parameter uses the entity as a
/// specification bag, which leaks persistence concerns into the query contract. Query methods must accept
/// primitive values or dedicated parameter types instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityQueryParameterAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA020";

	private const string IEntityMetadataName = "Umbrella.DataAccess.Abstractions.IEntity`1";

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
		title: "Entity types must not be used as query method parameters",
		messageFormat: "Parameter '{0}' is of entity type '{1}'. Query methods must accept primitive values or dedicated parameter types, not entity instances.",
		category: "DataAccess",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Passing an entity instance to a query method uses the entity as a specification bag. This couples the query contract to the entity shape and leaks persistence concerns. Accept individual primitive values or a dedicated query/filter type instead.");

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
			var iEntitySymbol = compilationContext.Compilation.GetTypeByMetadataName(IEntityMetadataName);

			if (iEntitySymbol is null)
				return;

			compilationContext.RegisterSymbolAction(
				ctx => AnalyzeMethod(ctx, iEntitySymbol),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol iEntitySymbol)
	{
		var method = (IMethodSymbol)context.Symbol;

		if (!IsQueryMethod(method.Name))
			return;

		foreach (var parameter in method.Parameters)
		{
			if (parameter.Type is not INamedTypeSymbol paramType)
				continue;

			if (!IsOrImplementsIEntity(paramType, iEntitySymbol))
				continue;

			var location = parameter.Locations.FirstOrDefault();

			if (location is null)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(Rule, location, parameter.Name, paramType.Name));
		}
	}

	private static bool IsQueryMethod(string methodName)
	{
		foreach (string prefix in _queryMethodPrefixes)
		{
			if (methodName.StartsWith(prefix, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static bool IsOrImplementsIEntity(INamedTypeSymbol type, INamedTypeSymbol iEntitySymbol)
	{
		if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, iEntitySymbol))
			return true;

		return type.AllInterfaces.Any(i =>
			SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iEntitySymbol));
	}
}
