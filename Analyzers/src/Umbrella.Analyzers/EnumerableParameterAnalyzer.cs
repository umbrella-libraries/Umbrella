using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if collection-like parameters on changeable public methods use read-only collection types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EnumerableParameterAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA005";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Method parameters should use read-only collection types",
		"Parameter '{0}' should use a read-only collection type instead of {1}",
		"CodeStyle",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(static compilationContext =>
		{
			var methodAnalysis = new ChangeablePublicMethodAnalysis(compilationContext.Compilation);
			var collectionAnalysis = new CollectionTypeAnalysis(compilationContext.Compilation);
			compilationContext.RegisterSymbolAction(
				context => AnalyzeMethod(context, methodAnalysis, collectionAnalysis),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(
		SymbolAnalysisContext context,
		ChangeablePublicMethodAnalysis methodAnalysis,
		CollectionTypeAnalysis collectionAnalysis)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (!methodAnalysis.IsEligible(methodSymbol))
			return;

		foreach (var parameter in methodSymbol.Parameters)
		{
			if (parameter.IsParams ||
				collectionAnalysis.IsAllowedExpressionArray(parameter.Type) ||
				CollectionTypeAnalysis.IsBinaryBuffer(parameter.Type) ||
				collectionAnalysis.IsServiceCollectionContract(parameter.Type))
			{
				continue;
			}

			if (!collectionAnalysis.IsCollectionType(parameter.Type) ||
				collectionAnalysis.IsReadOnlyCollectionType(parameter.Type))
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				Rule,
				parameter.Locations[0],
				parameter.Name,
				parameter.Type.ToDisplayString()));
		}
	}
}
