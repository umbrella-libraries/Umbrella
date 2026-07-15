using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Umbrella.Analyzers;

internal sealed class AsyncMethodCancellationAnalysis
{
	private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
	private const string TaskMetadataName = "System.Threading.Tasks.Task";
	private const string GenericTaskMetadataName = "System.Threading.Tasks.Task`1";
	private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";
	private const string GenericValueTaskMetadataName = "System.Threading.Tasks.ValueTask`1";

	private readonly ChangeablePublicMethodAnalysis _changeablePublicMethodAnalysis;
	private readonly INamedTypeSymbol? _taskType;
	private readonly INamedTypeSymbol? _genericTaskType;
	private readonly INamedTypeSymbol? _valueTaskType;
	private readonly INamedTypeSymbol? _genericValueTaskType;
	private readonly IMethodSymbol? _throwIfCancellationRequestedMethod;

	internal AsyncMethodCancellationAnalysis(Compilation compilation)
	{
		_changeablePublicMethodAnalysis = new ChangeablePublicMethodAnalysis(compilation);
		CancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
		_taskType = compilation.GetTypeByMetadataName(TaskMetadataName);
		_genericTaskType = compilation.GetTypeByMetadataName(GenericTaskMetadataName);
		_valueTaskType = compilation.GetTypeByMetadataName(ValueTaskMetadataName);
		_genericValueTaskType = compilation.GetTypeByMetadataName(GenericValueTaskMetadataName);
		_throwIfCancellationRequestedMethod = CancellationTokenType?
			.GetMembers("ThrowIfCancellationRequested")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(static method => !method.IsStatic && method.Parameters.Length == 0);
	}

	internal INamedTypeSymbol? CancellationTokenType { get; }

	internal bool IsEligible(IMethodSymbol methodSymbol)
	{
		return _changeablePublicMethodAnalysis.IsEligible(methodSymbol) &&
			methodSymbol.IsAsync &&
			IsSupportedReturnType(methodSymbol.ReturnType);
	}

	internal IParameterSymbol? GetCanonicalCancellationTokenParameter(IMethodSymbol methodSymbol)
	{
		if (CancellationTokenType is null)
			return null;

		foreach (var parameter in methodSymbol.Parameters)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameter.Type, CancellationTokenType) ||
				parameter.Name != "cancellationToken")
			{
				continue;
			}

			foreach (var syntaxReference in parameter.DeclaringSyntaxReferences)
			{
				if (syntaxReference.GetSyntax() is ParameterSyntax parameterSyntax &&
					parameterSyntax.Default?.Value.IsKind(SyntaxKind.DefaultLiteralExpression) == true)
				{
					return parameter;
				}
			}
		}

		return null;
	}

	internal bool IsThrowIfCancellationRequestedCall(
		StatementSyntax statement,
		SemanticModel semanticModel,
		IParameterSymbol cancellationTokenParameter,
		CancellationToken cancellationToken)
	{
		if (_throwIfCancellationRequestedMethod is null ||
			statement is not ExpressionStatementSyntax expressionStatement ||
			expressionStatement.Expression is not InvocationExpressionSyntax invocation ||
			invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			return false;
		}

		var receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;
		var invokedMethod = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;

		return SymbolEqualityComparer.Default.Equals(receiverSymbol, cancellationTokenParameter) &&
			SymbolEqualityComparer.Default.Equals(invokedMethod?.OriginalDefinition, _throwIfCancellationRequestedMethod);
	}

	private bool IsSupportedReturnType(ITypeSymbol returnType)
	{
		var originalDefinition = returnType.OriginalDefinition;

		return SymbolEqualityComparer.Default.Equals(originalDefinition, _taskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, _genericTaskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, _valueTaskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, _genericValueTaskType);
	}
}
