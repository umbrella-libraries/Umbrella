using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if collection payloads returned by changeable public methods are non-nullable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NonNullableCollectionReturnTypeAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA007";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Method return types should be non-nullable collection types",
		"Method '{0}' returns a collection type '{1}' which is nullable",
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

		foreach (var returnType in collectionAnalysis.GetCollectionPayloadTypes(methodSymbol.ReturnType))
		{
			if (returnType.NullableAnnotation != NullableAnnotation.Annotated)
				continue;

			context.ReportDiagnostic(Diagnostic.Create(
				Rule,
				methodSymbol.Locations[0],
				methodSymbol.Name,
				returnType.ToDisplayString()));
		}
	}
}
