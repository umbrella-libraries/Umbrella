using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Ensures argument and cancellation validation occurs before the first top-level try...catch and never inside a try
/// block.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParameterValidationPlacementAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Represents the unique identifier for the diagnostic associated with this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA009";

	/// <summary>
	/// Represents a diagnostic rule that enforces parameter validation to occur before the first try...catch block in a
	/// method.
	/// </summary>
	/// <remarks>This rule is used to ensure that parameter validation logic is placed at the beginning of a method,
	/// prior to any try...catch blocks, to improve code clarity and maintainability.</remarks>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Argument and cancellation validation must precede exception handling",
		"Argument and cancellation validation should occur before the first try...catch block in method '{0}'",
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
				var analysis = new ParameterValidationAnalysis(compilationContext.Compilation);
				compilationContext.RegisterSyntaxNodeAction(
					syntaxContext => AnalyzeMethod(syntaxContext, analysis),
					SyntaxKind.MethodDeclaration);
			});
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, ParameterValidationAnalysis analysis)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;

		if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol ||
			methodDeclaration.Body is not { } body)
		{
			return;
		}

		var allTryStatements = body.DescendantNodes(ShouldDescendInto)
			.OfType<TryStatementSyntax>()
			.ToList();

		TryStatementSyntax? firstTopLevelTry = body.Statements.OfType<TryStatementSyntax>().FirstOrDefault();
		int firstTopLevelTryStart = firstTopLevelTry?.SpanStart ?? int.MaxValue;
		foreach (SyntaxNode node in body.DescendantNodes(ShouldDescendInto))
		{
			if (!analysis.IsValidationNode(node, context.SemanticModel, context.CancellationToken))
				continue;

			if (IsInsideTryBlock(node, allTryStatements) || node.SpanStart > firstTopLevelTryStart)
			{
				Report(context, methodSymbol, node.GetLocation());
				return;
			}
		}
	}

	private static bool ShouldDescendInto(SyntaxNode node)
	{
		return node is not LocalFunctionStatementSyntax and not AnonymousFunctionExpressionSyntax;
	}

	private static bool IsInsideTryBlock(SyntaxNode node, IEnumerable<TryStatementSyntax> tryStatements)
	{
		foreach (TryStatementSyntax tryStatement in tryStatements)
		{
			if (node.Ancestors().Any(x => x == tryStatement.Block))
				return true;
		}

		return false;
	}

	private static void Report(SyntaxNodeAnalysisContext context, IMethodSymbol methodSymbol, Location location)
	{
		var diagnostic = Diagnostic.Create(Rule, location, methodSymbol.Name);
		context.ReportDiagnostic(diagnostic);
	}
}
