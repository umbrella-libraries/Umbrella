using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Roslyn analyzer that enforces API standards for controllers that inherit from <c>UmbrellaApiController</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UmbrellaApiStandardsAnalyzer : DiagnosticAnalyzer
{
	private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";
	private const string NonControllerAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonControllerAttribute";
	private const string UmbrellaApiControllerMetadataName = "Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController";
	private const string ProducesResponseTypeAttributeMetadataName = "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute";
	private const string ProducesResponseTypeAttributeOfTMetadataName = "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute`1";
	private const string UmbrellaProducesResponseTypeAttributeMetadataName = "Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaProducesResponseTypeAttribute";
	private const string UmbrellaProducesResponseTypeAttributeOfTMetadataName = "Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaProducesResponseTypeAttribute`1";

	/// <summary>
	/// Diagnostic emitted when a controller or action on a <c>UmbrellaApiController</c> subclass uses a raw ASP.NET
	/// Core response type attribute instead of <c>[UmbrellaProducesResponseType]</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor UseUmbrellaProducesResponseTypeRule = new(
		id: "UA017",
		title: "Use [UmbrellaProducesResponseType] instead of [ProducesResponseType]",
		messageFormat: "{0} '{1}' uses a raw ASP.NET Core response type attribute; replace it with [UmbrellaProducesResponseType]",
		category: "UmbrellaApiStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "UmbrellaApiController subclasses must use the generic or non-generic UmbrellaProducesResponseType attribute rather than raw ASP.NET Core response type attributes. The Umbrella variant enforces consistent response type documentation and integrates with the Umbrella OpenAPI pipeline.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UseUmbrellaProducesResponseTypeRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? controllerBaseSymbol = startContext.Compilation.GetTypeByMetadataName(UmbrellaApiControllerMetadataName);
			INamedTypeSymbol? nonActionSymbol = startContext.Compilation.GetTypeByMetadataName(NonActionAttributeMetadataName);
			INamedTypeSymbol? nonControllerSymbol = startContext.Compilation.GetTypeByMetadataName(NonControllerAttributeMetadataName);
			INamedTypeSymbol? producesResponseTypeSymbol = startContext.Compilation.GetTypeByMetadataName(ProducesResponseTypeAttributeMetadataName);
			INamedTypeSymbol? producesResponseTypeOfTSymbol = startContext.Compilation.GetTypeByMetadataName(ProducesResponseTypeAttributeOfTMetadataName);
			INamedTypeSymbol? umbrellaProducesResponseTypeSymbol = startContext.Compilation.GetTypeByMetadataName(UmbrellaProducesResponseTypeAttributeMetadataName);
			INamedTypeSymbol? umbrellaProducesResponseTypeOfTSymbol = startContext.Compilation.GetTypeByMetadataName(UmbrellaProducesResponseTypeAttributeOfTMetadataName);

			if (controllerBaseSymbol is null ||
				(producesResponseTypeSymbol is null && producesResponseTypeOfTSymbol is null))
			{
				return;
			}

			var responseAttributeSymbols = new ResponseAttributeSymbols(
				producesResponseTypeSymbol,
				producesResponseTypeOfTSymbol,
				umbrellaProducesResponseTypeSymbol,
				umbrellaProducesResponseTypeOfTSymbol);

			startContext.RegisterSymbolAction(
				ctx => AnalyzeController(ctx, controllerBaseSymbol, nonControllerSymbol, responseAttributeSymbols),
				SymbolKind.NamedType);

			startContext.RegisterSymbolAction(
				ctx => AnalyzeMethod(ctx, controllerBaseSymbol, nonActionSymbol, nonControllerSymbol, responseAttributeSymbols),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeController(
		SymbolAnalysisContext context,
		INamedTypeSymbol controllerBaseSymbol,
		INamedTypeSymbol? nonControllerSymbol,
		ResponseAttributeSymbols responseAttributeSymbols)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		if (typeSymbol.TypeKind != TypeKind.Class ||
			typeSymbol.IsImplicitlyDeclared ||
			!typeSymbol.Locations.Any(static location => location.IsInSource) ||
			!InheritsFrom(typeSymbol, controllerBaseSymbol) ||
			HasNonControllerAttribute(typeSymbol, nonControllerSymbol))
		{
			return;
		}

		ReportRawAttributes(
			context,
			typeSymbol.GetAttributes(),
			responseAttributeSymbols,
			"Controller",
			typeSymbol.Name);
	}

	private static void AnalyzeMethod(
		SymbolAnalysisContext context,
		INamedTypeSymbol controllerBaseSymbol,
		INamedTypeSymbol? nonActionSymbol,
		INamedTypeSymbol? nonControllerSymbol,
		ResponseAttributeSymbols responseAttributeSymbols)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (methodSymbol.DeclaredAccessibility != Accessibility.Public ||
			methodSymbol.MethodKind != MethodKind.Ordinary ||
			methodSymbol.IsStatic ||
			methodSymbol.IsGenericMethod ||
			methodSymbol.IsImplicitlyDeclared ||
			methodSymbol.DeclaringSyntaxReferences.Length == 0 ||
			!methodSymbol.Locations.Any(static location => location.IsInSource) ||
			!InheritsFrom(methodSymbol.ContainingType, controllerBaseSymbol) ||
			HasNonControllerAttribute(methodSymbol.ContainingType, nonControllerSymbol) ||
			HasNonActionAttribute(methodSymbol, nonActionSymbol))
		{
			return;
		}

		ReportRawAttributes(
			context,
			methodSymbol.GetAttributes(),
			responseAttributeSymbols,
			"Method",
			methodSymbol.Name);
	}

	private static void ReportRawAttributes(
		SymbolAnalysisContext context,
		ImmutableArray<AttributeData> attributes,
		ResponseAttributeSymbols responseAttributeSymbols,
		string targetKind,
		string targetName)
	{
		foreach (AttributeData attribute in attributes)
		{
			if (!IsRawResponseAttribute(attribute.AttributeClass, responseAttributeSymbols))
				continue;

			Location? location = attribute.ApplicationSyntaxReference?
				.GetSyntax(context.CancellationToken)
				.GetLocation();

			if (location is not null)
			{
				context.ReportDiagnostic(Diagnostic.Create(
					UseUmbrellaProducesResponseTypeRule,
					location,
					targetKind,
					targetName));
			}
		}
	}

	private static bool HasNonActionAttribute(
		IMethodSymbol methodSymbol,
		INamedTypeSymbol? nonActionSymbol)
	{
		if (nonActionSymbol is null)
			return false;

		for (IMethodSymbol? current = methodSymbol; current is not null; current = current.OverriddenMethod)
		{
			if (current.GetAttributes().Any(
				attribute => IsOrInheritsFrom(attribute.AttributeClass, nonActionSymbol)))
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasNonControllerAttribute(
		INamedTypeSymbol typeSymbol,
		INamedTypeSymbol? nonControllerSymbol)
	{
		if (nonControllerSymbol is null)
			return false;

		for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
		{
			if (current.GetAttributes().Any(
				attribute => IsOrInheritsFrom(attribute.AttributeClass, nonControllerSymbol)))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsRawResponseAttribute(
		INamedTypeSymbol? attributeType,
		ResponseAttributeSymbols symbols)
	{
		if (attributeType is null ||
			IsOrInheritsFrom(attributeType, symbols.UmbrellaProducesResponseType) ||
			IsOrInheritsFrom(attributeType, symbols.UmbrellaProducesResponseTypeOfT))
		{
			return false;
		}

		return IsOrInheritsFrom(attributeType, symbols.ProducesResponseType) ||
			IsOrInheritsFrom(attributeType, symbols.ProducesResponseTypeOfT);
	}

	private static bool IsOrInheritsFrom(
		INamedTypeSymbol? type,
		INamedTypeSymbol? candidateBaseType)
	{
		if (candidateBaseType is null)
			return false;

		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(
				current.OriginalDefinition,
				candidateBaseType.OriginalDefinition))
			{
				return true;
			}
		}

		return false;
	}

	private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
	{
		INamedTypeSymbol? current = type.BaseType;
		while (current is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseType))
				return true;

			current = current.BaseType;
		}

		return false;
	}

	private sealed record ResponseAttributeSymbols(
		INamedTypeSymbol? ProducesResponseType,
		INamedTypeSymbol? ProducesResponseTypeOfT,
		INamedTypeSymbol? UmbrellaProducesResponseType,
		INamedTypeSymbol? UmbrellaProducesResponseTypeOfT);
}
