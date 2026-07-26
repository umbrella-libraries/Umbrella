using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Umbrella.Analyzers;

internal sealed class ParameterValidationAnalysis
{
	private const string ArgumentExceptionMetadataName = "System.ArgumentException";
	private const string ArgumentNullExceptionMetadataName = "System.ArgumentNullException";
	private const string ArgumentOutOfRangeExceptionMetadataName = "System.ArgumentOutOfRangeException";
	private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
	private const string GuardMetadataName = "CommunityToolkit.Diagnostics.Guard";

	private readonly INamedTypeSymbol? _argumentExceptionType;
	private readonly INamedTypeSymbol? _argumentNullExceptionType;
	private readonly INamedTypeSymbol? _argumentOutOfRangeExceptionType;
	private readonly INamedTypeSymbol? _cancellationTokenType;
	private readonly INamedTypeSymbol? _guardType;

	internal ParameterValidationAnalysis(Compilation compilation)
	{
		_argumentExceptionType = compilation.GetTypeByMetadataName(ArgumentExceptionMetadataName);
		_argumentNullExceptionType = compilation.GetTypeByMetadataName(ArgumentNullExceptionMetadataName);
		_argumentOutOfRangeExceptionType = compilation.GetTypeByMetadataName(ArgumentOutOfRangeExceptionMetadataName);
		_cancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
		_guardType = compilation.GetTypeByMetadataName(GuardMetadataName);
	}

	internal bool IsValidationNode(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		return node switch
		{
			InvocationExpressionSyntax invocation => IsValidationInvocation(invocation, semanticModel, cancellationToken),
			ThrowStatementSyntax throwStatement => IsArgumentValidationThrow(throwStatement, semanticModel, cancellationToken),
			_ => false
		};
	}

	internal bool IsValidationPreambleStatement(StatementSyntax statement, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		switch (statement)
		{
			case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
				return IsValidationInvocation(invocation, semanticModel, cancellationToken);

			case ThrowStatementSyntax throwStatement:
				return IsArgumentValidationThrow(throwStatement, semanticModel, cancellationToken);

			case BlockSyntax block:
				return block.Statements.Count > 0 &&
					block.Statements.All(x => IsValidationPreambleStatement(x, semanticModel, cancellationToken));

			case IfStatementSyntax ifStatement:
				return IsValidationPreambleStatement(ifStatement.Statement, semanticModel, cancellationToken) &&
					(ifStatement.Else is null || IsValidationPreambleStatement(ifStatement.Else.Statement, semanticModel, cancellationToken));

			default:
				return false;
		}
	}

	internal bool IsCancellationToken(ITypeSymbol type)
	{
		return IsType(type, _cancellationTokenType);
	}

	private bool IsValidationInvocation(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
			return false;

		method = method.ReducedFrom ?? method;

		if (IsType(method.ContainingType, _guardType))
			return true;

		if (method.Name.StartsWith("Throw", StringComparison.Ordinal) &&
			(IsType(method.ContainingType, _argumentExceptionType) ||
				IsType(method.ContainingType, _argumentNullExceptionType) ||
				IsType(method.ContainingType, _argumentOutOfRangeExceptionType)))
		{
			return true;
		}

		return method.Name == nameof(CancellationToken.ThrowIfCancellationRequested) &&
			IsType(method.ContainingType, _cancellationTokenType);
	}

	private bool IsArgumentValidationThrow(
		ThrowStatementSyntax throwStatement,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (throwStatement.Expression is not ObjectCreationExpressionSyntax objectCreation)
			return false;

		ITypeSymbol? createdType = semanticModel.GetTypeInfo(objectCreation, cancellationToken).Type;

		return IsType(createdType, _argumentExceptionType) ||
			IsType(createdType, _argumentNullExceptionType) ||
			IsType(createdType, _argumentOutOfRangeExceptionType);
	}

	private static bool IsType(ITypeSymbol? candidate, INamedTypeSymbol? expected)
	{
		return expected is not null &&
			candidate is not null &&
			SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, expected);
	}
}
