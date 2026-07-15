using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if eligible public async methods declare the canonical CancellationToken parameter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncMethodCancellationAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA003";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Async methods should have a CancellationToken parameter",
		"Async method '{0}' should have a 'CancellationToken cancellationToken = default' parameter",
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
			var analysis = new AsyncMethodCancellationAnalysis(compilationContext.Compilation);
			compilationContext.RegisterSymbolAction(
				context => AnalyzeMethod(context, analysis),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context, AsyncMethodCancellationAnalysis analysis)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (!analysis.IsEligible(methodSymbol) ||
			analysis.GetCanonicalCancellationTokenParameter(methodSymbol) is not null)
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name));
	}
}
