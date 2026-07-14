using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks for primitive and enum comparisons using '==' or '!=' and enforces using pattern matching with 'is' or 'is not'.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PrimitiveComparisonAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA002";

	/// <summary>
	/// The diagnostic rule for this analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Use pattern matching for primitive and enum comparisons",
		"Use 'is' or 'is not' instead of '==' or '!=' for primitive and enum comparisons",
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
		context.RegisterSyntaxNodeAction(AnalyzeBinaryExpression, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
	}

	private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context)
	{
		var binaryExpression = (BinaryExpressionSyntax)context.Node;
		var leftConstantValue = context.SemanticModel.GetConstantValue(binaryExpression.Left, context.CancellationToken);
		var rightConstantValue = context.SemanticModel.GetConstantValue(binaryExpression.Right, context.CancellationToken);
		var leftType = context.SemanticModel.GetTypeInfo(binaryExpression.Left).Type;
		var rightType = context.SemanticModel.GetTypeInfo(binaryExpression.Right).Type;

		if ((leftConstantValue.HasValue || rightConstantValue.HasValue) &&
			leftType != null && rightType != null &&
			(leftType.IsValueType || leftType.TypeKind == TypeKind.Enum) &&
			SymbolEqualityComparer.Default.Equals(leftType, rightType))
		{
			if (ExpressionTreeAnalysis.IsWithinExpressionTree(binaryExpression, context))
				return;

			var diagnostic = Diagnostic.Create(Rule, binaryExpression.GetLocation());
			context.ReportDiagnostic(diagnostic);
		}
	}
}