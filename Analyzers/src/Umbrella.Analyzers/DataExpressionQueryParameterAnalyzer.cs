using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Analyzer that prevents a single <c>SortExpression&lt;TItem&gt;</c> or <c>FilterExpression&lt;TItem&gt;</c> from being
/// declared as an action parameter. Only the collection form, or the matching descriptor type, may be used.
/// </summary>
/// <remarks>
/// <para>
/// These types expose a <see cref="System.Linq.Expressions.Expression"/> property. ASP.NET Core's ApiExplorer flattens
/// a non-collection complex action parameter into one description per property, recursing through the property graph as
/// it goes. Reaching the expression property takes the walk into <see cref="System.Type"/>, whose reflection graph is
/// vast and densely cyclic, and because the walk enumerates paths rather than nodes it does not complete in practical
/// time. Every consumer of ApiExplorer stalls with it, so OpenAPI document generation never returns and the document
/// endpoint hangs rather than failing.
/// </para>
/// <para>
/// The collection form is not flattened, so it is unaffected: ApiExplorer emits a single parameter and never walks the
/// element type's properties. That is the form the Umbrella generic controller families already use. The descriptor
/// types carry no expression tree and are safe in either form.
/// </para>
/// <para>
/// The binding source is deliberately not considered. A single data expression is wrong wherever it is bound from: the
/// Data Expression model binders read a JSON document from a single query string value, so a body-bound parameter
/// bypasses them entirely and cannot be deserialized.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataExpressionQueryParameterAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA024";

	private const string SortExpressionMetadataName = "Umbrella.Utilities.Data.Sorting.SortExpression`1";
	private const string FilterExpressionMetadataName = "Umbrella.Utilities.Data.Filtering.FilterExpression`1";
	private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";
	private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";

	/// <summary>
	/// The diagnostic rule for this analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		id: DiagnosticId,
		title: "Data expression action parameters must use the collection form",
		messageFormat: "Parameter '{0}' on action '{1}' is a single '{2}'. ApiExplorer flattens it and walks the expression tree, so OpenAPI document generation never completes. Declare an array or IEnumerable<> of it, or use the matching descriptor type.",
		category: "UmbrellaApiStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "A single SortExpression<TItem> or FilterExpression<TItem> action parameter is flattened by ApiExplorer into one description per property. The walk reaches the expression tree and then System.Type, whose reflection graph does not terminate in practical time, so OpenAPI document generation hangs. The collection form is not flattened and is the form the Umbrella generic controller families use; the descriptor types carry no expression tree and are safe in either form.");

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
			INamedTypeSymbol? sortExpressionSymbol = compilationContext.Compilation.GetTypeByMetadataName(
				SortExpressionMetadataName);

			INamedTypeSymbol? filterExpressionSymbol = compilationContext.Compilation.GetTypeByMetadataName(
				FilterExpressionMetadataName);

			if (sortExpressionSymbol is null && filterExpressionSymbol is null)
				return;

			INamedTypeSymbol? controllerBaseSymbol = compilationContext.Compilation.GetTypeByMetadataName(
				ControllerBaseMetadataName);

			if (controllerBaseSymbol is null)
				return;

			INamedTypeSymbol? nonActionAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(
				NonActionAttributeMetadataName);

			var symbols = new DataExpressionSymbols(
				sortExpressionSymbol,
				filterExpressionSymbol,
				controllerBaseSymbol,
				nonActionAttributeSymbol);

			compilationContext.RegisterSymbolAction(ctx => AnalyzeMethod(ctx, symbols), SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context, DataExpressionSymbols symbols)
	{
		var method = (IMethodSymbol)context.Symbol;

		if (!IsAction(method, symbols))
			return;

		foreach (IParameterSymbol parameter in method.Parameters)
		{
			INamedTypeSymbol? expressionType = TryGetSingleDataExpressionType(parameter.Type, symbols);

			if (expressionType is null)
				continue;

			Location? location = parameter.Locations.FirstOrDefault(static x => x.IsInSource);

			if (location is null)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(
				Rule,
				location,
				parameter.Name,
				method.Name,
				expressionType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}

	/// <summary>
	/// Determines whether the method is an MVC action. Every public instance method on a controller is an action
	/// unless it opts out with <c>[NonAction]</c>.
	/// </summary>
	private static bool IsAction(IMethodSymbol method, DataExpressionSymbols symbols)
	{
		if (method.MethodKind is not MethodKind.Ordinary
			|| method.DeclaredAccessibility is not Accessibility.Public
			|| method.IsStatic
			|| method.IsImplicitlyDeclared
			|| !method.Locations.Any(static x => x.IsInSource))
		{
			return false;
		}

		return InheritsFromControllerBase(method.ContainingType, symbols.ControllerBase)
			&& !HasNonActionAttribute(method, symbols.NonActionAttribute);
	}

	private static bool InheritsFromControllerBase(INamedTypeSymbol? type, INamedTypeSymbol controllerBase)
	{
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, controllerBase))
				return true;
		}

		return false;
	}

	private static bool HasNonActionAttribute(IMethodSymbol method, INamedTypeSymbol? nonActionAttribute)
	{
		if (nonActionAttribute is null)
			return false;

		for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
		{
			foreach (AttributeData attribute in current.GetAttributes())
			{
				if (IsOrInheritsFrom(attribute.AttributeClass, nonActionAttribute))
					return true;
			}
		}

		return false;
	}

	private static bool IsOrInheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol target)
	{
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Resolves the data expression type when the parameter supplies exactly one of them. A collection of data
	/// expressions is the supported form, so it deliberately does not match.
	/// </summary>
	private static INamedTypeSymbol? TryGetSingleDataExpressionType(
		ITypeSymbol parameterType,
		DataExpressionSymbols symbols)
	{
		if (parameterType is not INamedTypeSymbol namedType)
			return null;

		// The data expressions are structs, so an optional parameter arrives as Nullable<T>.
		if (namedType.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T
			&& namedType.TypeArguments.Length is 1)
		{
			return TryGetSingleDataExpressionType(namedType.TypeArguments[0], symbols);
		}

		INamedTypeSymbol definition = namedType.OriginalDefinition;

		if (symbols.SortExpression is not null
			&& SymbolEqualityComparer.Default.Equals(definition, symbols.SortExpression))
		{
			return namedType;
		}

		return symbols.FilterExpression is not null
			&& SymbolEqualityComparer.Default.Equals(definition, symbols.FilterExpression)
			? namedType
			: null;
	}

	private sealed record DataExpressionSymbols(
		INamedTypeSymbol? SortExpression,
		INamedTypeSymbol? FilterExpression,
		INamedTypeSymbol ControllerBase,
		INamedTypeSymbol? NonActionAttribute);
}
