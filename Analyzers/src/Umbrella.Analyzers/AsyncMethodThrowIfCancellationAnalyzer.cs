using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if eligible async methods with the canonical CancellationToken parameter call
/// <c>ThrowIfCancellationRequested</c> as the first statement of the method body.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncMethodThrowIfCancellationAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA004";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Async methods with CancellationToken should call ThrowIfCancellationRequested",
		"Async method '{0}' should call 'cancellationToken.ThrowIfCancellationRequested()' as the first line of the method body",
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
			compilationContext.RegisterSyntaxNodeAction(
				context => AnalyzeMethod(context, analysis),
				SyntaxKind.MethodDeclaration);
		});
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, AsyncMethodCancellationAnalysis analysis)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;
		var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);

		if (methodSymbol is null || !analysis.IsEligible(methodSymbol))
			return;

		var cancellationTokenParameter = analysis.GetCanonicalCancellationTokenParameter(methodSymbol);

		if (cancellationTokenParameter is null)
			return;

		var firstStatement = methodDeclaration.Body?.Statements.FirstOrDefault();

		if (firstStatement is not null &&
			analysis.IsThrowIfCancellationRequestedCall(
				firstStatement,
				context.SemanticModel,
				cancellationTokenParameter,
				context.CancellationToken))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(Rule, methodDeclaration.Identifier.GetLocation(), methodDeclaration.Identifier.Text));
	}
}
