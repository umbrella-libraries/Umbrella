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
	private const string UmbrellaApiControllerMetadataName = "Umbrella.AspNetCore.WebUtilities.Mvc.UmbrellaApiController";
	private const string ProducesResponseTypeAttributeMetadataName = "Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute";

	/// <summary>
	/// Diagnostic emitted when a method on a <c>UmbrellaApiController</c> subclass uses <c>[ProducesResponseType]</c> instead of <c>[UmbrellaProducesResponseType]</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor UseUmbrellaProducesResponseTypeRule = new(
		id: "UA017",
		title: "Use [UmbrellaProducesResponseType] instead of [ProducesResponseType]",
		messageFormat: "Method '{0}' uses [ProducesResponseType]; replace with [UmbrellaProducesResponseType] in UmbrellaApiController subclasses",
		category: "UmbrellaApiStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "UmbrellaApiController subclasses must use [UmbrellaProducesResponseType] rather than the raw ASP.NET Core [ProducesResponseType] attribute. The Umbrella variant enforces consistent response type documentation and integrates with the Umbrella OpenAPI pipeline.");

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
			INamedTypeSymbol? producesResponseTypeSymbol = startContext.Compilation.GetTypeByMetadataName(ProducesResponseTypeAttributeMetadataName);

			if (controllerBaseSymbol is null || producesResponseTypeSymbol is null)
				return;

			startContext.RegisterSymbolAction(
				ctx => AnalyzeMethod(ctx, controllerBaseSymbol, producesResponseTypeSymbol),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(
		SymbolAnalysisContext context,
		INamedTypeSymbol controllerBaseSymbol,
		INamedTypeSymbol producesResponseTypeSymbol)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (methodSymbol.ContainingType is not INamedTypeSymbol containingType)
			return;

		if (!InheritsFrom(containingType, controllerBaseSymbol))
			return;

		foreach (AttributeData attr in methodSymbol.GetAttributes())
		{
			if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, producesResponseTypeSymbol))
				continue;

			Location location = attr.ApplicationSyntaxReference is null
				? methodSymbol.Locations[0]
				: attr.ApplicationSyntaxReference.GetSyntax(context.CancellationToken).GetLocation();

			context.ReportDiagnostic(Diagnostic.Create(
				UseUmbrellaProducesResponseTypeRule,
				location,
				methodSymbol.Name));

			return;
		}
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
}
