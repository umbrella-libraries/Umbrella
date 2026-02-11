using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Analyzer that enforces all parameter validation (Guard.* calls, Argument* Throw* helpers, or direct throws
/// of ArgumentException / ArgumentNullException / ArgumentOutOfRangeException) appear before the first
/// top-level try...catch block in a method body and NEVER anywhere inside any try block (including nested tries,
/// lambdas within try blocks etc.). Parameter validation must be performed at the very start of the method
/// to ensure argument exceptions are thrown deterministically and not swallowed by later exception handling logic.
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
		"Parameter validation must appear before first try...catch block",
		"Parameter validation should occur before the first try...catch block in method '{0}'",
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
		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;
		var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
		if (syntaxRef is null)
			return;

		if (syntaxRef.GetSyntax(context.CancellationToken) is not Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodDecl)
			return;

		var body = methodDecl.Body;
		if (body is null)
			return; // expression-bodied members cannot contain try blocks nor multiple statements.

		// Gather all try statements (including nested ones) for inside-try detection.
		var allTryStatements = body.DescendantNodes(static _ => true)
			.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax>()
			.ToList();

		// Find the first TOP-LEVEL try (direct child of the method body statements collection).
		Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax? firstTopLevelTry = null;
		foreach (var statement in body.Statements)
		{
			if (statement is Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax ts)
			{
				firstTopLevelTry = ts;
				break;
			}
		}

		int firstTopLevelTryStart = firstTopLevelTry?.SpanStart ?? int.MaxValue;

		// Walk every descendant node (excluding local functions) to locate parameter validation occurrences.
		foreach (var node in body.DescendantNodes(ShouldDescendInto))
		{
			if (!IsParameterValidationNode(node))
				continue;

			// 1. Inside ANY try block? (even nested). If so, report.
			if (IsInsideTryBlock(node, allTryStatements))
			{
				Report(context, methodSymbol, node.GetLocation());
				return;
			}

			// 2. Appears AFTER the first top-level try? (i.e. even if not inside a try but placed later) -> report.
			if (node.SpanStart > firstTopLevelTryStart)
			{
				Report(context, methodSymbol, node.GetLocation());
				return;
			}
		}
	}

	private static bool ShouldDescendInto(SyntaxNode node)
	{
		// Do not analyze inside local functions; those have their own validation scope.
		if (node is Microsoft.CodeAnalysis.CSharp.Syntax.LocalFunctionStatementSyntax)
			return false;

		return true;
	}

	private static bool IsInsideTryBlock(SyntaxNode node, IEnumerable<Microsoft.CodeAnalysis.CSharp.Syntax.TryStatementSyntax> tryStatements)
	{
		foreach (var ts in tryStatements)
		{
			var block = ts.Block;
			if (block is null)
				continue;

			if (node.SpanStart >= block.SpanStart && node.SpanStart < block.Span.End && IsNodeWithinBlock(node, block))
				return true;
		}

		return false;
	}

	private static bool IsNodeWithinBlock(SyntaxNode node, Microsoft.CodeAnalysis.CSharp.Syntax.BlockSyntax block)
	{
		SyntaxNode? current = node;
		while (current is not null)
		{
			if (current == block)
				return true;

			// Stop if we reach a boundary that would indicate we are no longer strictly inside the try block itself.
			if (current is Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax or Microsoft.CodeAnalysis.CSharp.Syntax.FinallyClauseSyntax)
				return false;

			current = current.Parent;
		}

		return false;
	}

	private static bool IsParameterValidationNode(SyntaxNode node)
	{
		// Guard.*
		if (node is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax invocation && invocation.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess)
		{
			var leftId = memberAccess.Expression as Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax;
			var rightId = memberAccess.Name as Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax;
			if (leftId is not null && rightId is not null)
			{
				string leftText = leftId.Identifier.Text;
				string rightText = rightId.Identifier.Text;

				if (leftText == "Guard")
					return true;

				// ArgumentX.ThrowIfX style static helpers (must begin with Throw)
				if ((leftText is "ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException") && rightText.StartsWith("Throw", StringComparison.Ordinal))
					return true;
			}
		}

		// throw new ArgumentX(...)
		if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ThrowStatementSyntax throwStmt && throwStmt.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax objectCreation)
		{
			if (objectCreation.Type is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax typeId)
			{
				string name = typeId.Identifier.Text;
				if (name is "ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException")
					return true;
			}
			else if (objectCreation.Type is Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax qn)
			{
				string right = qn.Right.Identifier.Text;
				if (right is "ArgumentException" or "ArgumentNullException" or "ArgumentOutOfRangeException")
					return true;
			}
		}

		return false;
	}

	private static void Report(SymbolAnalysisContext context, IMethodSymbol methodSymbol, Location location)
	{
		var diagnostic = Diagnostic.Create(Rule, location, methodSymbol.Name);
		context.ReportDiagnostic(diagnostic);
	}
}