using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

internal static class ExpressionTreeAnalysis
{
	public static bool IsWithinExpressionTree(SyntaxNode node, SyntaxNodeAnalysisContext context)
	{
		INamedTypeSymbol? expressionType = context.Compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");

		if (expressionType is null)
			return false;

		foreach (AnonymousFunctionExpressionSyntax anonymousFunction in node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>())
		{
			ITypeSymbol? convertedType = context.SemanticModel.GetTypeInfo(anonymousFunction, context.CancellationToken).ConvertedType;

			if (convertedType is INamedTypeSymbol namedType &&
				SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, expressionType))
			{
				return true;
			}
		}

		return false;
	}
}
