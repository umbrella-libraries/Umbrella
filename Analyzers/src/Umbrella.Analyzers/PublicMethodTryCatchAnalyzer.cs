using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Ensures public instance methods on logger-owning types wrap their operational code in an outer try...catch block
/// and log caught exceptions with relevant method state.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicMethodTryCatchAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA008";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Public instance methods with an ILogger should use state-aware exception handling",
		"Public method '{0}' should wrap operational code in try...catch and log the caught exception with relevant method state",
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
		context.RegisterCompilationStartAction(
			compilationContext =>
			{
				var analysis = new PublicMethodExceptionHandlingAnalysis(compilationContext.Compilation);
				compilationContext.RegisterSyntaxNodeAction(
					syntaxContext => AnalyzeMethod(syntaxContext, analysis),
					SyntaxKind.MethodDeclaration);
			});
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, PublicMethodExceptionHandlingAnalysis analysis)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;

		if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol ||
			!analysis.IsEligible(methodSymbol))
		{
			return;
		}

		if (methodDeclaration.ExpressionBody is not null)
		{
			ReportDiagnostic(context, methodSymbol);
			return;
		}

		TryStatementSyntax? tryStatement = analysis.FindOuterTryStatement(
			methodDeclaration,
			context.SemanticModel,
			context.CancellationToken);

		if (tryStatement is null ||
			!analysis.HasRequiredLogging(methodSymbol, tryStatement, context.SemanticModel, context.CancellationToken))
		{
			ReportDiagnostic(context, methodSymbol);
		}
	}

	private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, IMethodSymbol methodSymbol)
	{
		var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
		context.ReportDiagnostic(diagnostic);
	}
}
