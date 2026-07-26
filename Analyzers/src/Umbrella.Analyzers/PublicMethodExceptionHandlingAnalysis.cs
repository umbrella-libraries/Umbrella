using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Umbrella.Analyzers;

internal sealed class PublicMethodExceptionHandlingAnalysis
{
	private const string ILoggerMetadataName = "Microsoft.Extensions.Logging.ILogger";

	private readonly Compilation _compilation;
	private readonly INamedTypeSymbol? _loggerType;
	private readonly ParameterValidationAnalysis _parameterValidationAnalysis;

	internal PublicMethodExceptionHandlingAnalysis(Compilation compilation)
	{
		_compilation = compilation;
		_loggerType = compilation.GetTypeByMetadataName(ILoggerMetadataName);
		_parameterValidationAnalysis = new ParameterValidationAnalysis(compilation);
	}

	internal bool IsEligible(IMethodSymbol methodSymbol)
	{
		return methodSymbol.DeclaredAccessibility == Accessibility.Public &&
			methodSymbol.MethodKind == MethodKind.Ordinary &&
			!methodSymbol.IsStatic &&
			!methodSymbol.IsAbstract &&
			!methodSymbol.IsImplicitlyDeclared &&
			!methodSymbol.IsExtern &&
			methodSymbol.DeclaringSyntaxReferences.Length > 0 &&
			methodSymbol.Locations.Any(static location => location.IsInSource) &&
			HasAccessibleLogger(methodSymbol.ContainingType);
	}

	internal TryStatementSyntax? FindOuterTryStatement(
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (methodDeclaration.Body is not { } body)
			return null;

		int index = 0;

		while (index < body.Statements.Count &&
			_parameterValidationAnalysis.IsValidationPreambleStatement(body.Statements[index], semanticModel, cancellationToken))
		{
			index++;
		}

		if (index != body.Statements.Count - 1)
			return null;

		return body.Statements[index] as TryStatementSyntax;
	}

	internal bool HasRequiredLogging(
		IMethodSymbol methodSymbol,
		TryStatementSyntax tryStatement,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (tryStatement.Catches.Count == 0)
			return false;

		bool requiresState = methodSymbol.Parameters.Any(x => !_parameterValidationAnalysis.IsCancellationToken(x.Type));

		return tryStatement.Catches.All(
			x => ContainsRequiredLogging(x, semanticModel, requiresState, cancellationToken));
	}

	private bool HasAccessibleLogger(INamedTypeSymbol containingType)
	{
		if (_loggerType is null)
			return false;

		for (INamedTypeSymbol? type = containingType; type is not null; type = type.BaseType)
		{
			foreach (ISymbol member in type.GetMembers())
			{
				if (member is IFieldSymbol { IsStatic: false } field &&
					IsLoggerType(field.Type) &&
					(SymbolEqualityComparer.Default.Equals(type, containingType) ||
						_compilation.IsSymbolAccessibleWithin(field, containingType)))
				{
					return true;
				}

				if (member is IPropertySymbol { IsStatic: false } property &&
					IsLoggerType(property.Type) &&
					(SymbolEqualityComparer.Default.Equals(type, containingType) ||
						_compilation.IsSymbolAccessibleWithin(property, containingType)))
				{
					return true;
				}
			}
		}

		return false;
	}

	private bool ContainsRequiredLogging(
		CatchClauseSyntax catchClause,
		SemanticModel semanticModel,
		bool requiresState,
		CancellationToken cancellationToken)
	{
		if (catchClause.Declaration is null ||
			semanticModel.GetDeclaredSymbol(catchClause.Declaration, cancellationToken) is not ILocalSymbol exceptionLocal)
		{
			return false;
		}

		foreach (InvocationExpressionSyntax invocationSyntax in catchClause.DescendantNodes(ShouldDescendInto).OfType<InvocationExpressionSyntax>())
		{
			if (!IsLoggerReceiver(invocationSyntax, semanticModel, cancellationToken) ||
				invocationSyntax.Expression is not MemberAccessExpressionSyntax memberAccess ||
				!ReferencesLocal(invocationSyntax, exceptionLocal, semanticModel, cancellationToken))
			{
				continue;
			}

			string methodName = memberAccess.Name.Identifier.ValueText;

			if (methodName is "WriteError" or "WriteCritical" &&
				(!requiresState ||
					(semanticModel.GetOperation(invocationSyntax, cancellationToken) is IInvocationOperation writeInvocation &&
						HasExplicitStateArgument(writeInvocation))))
			{
				return true;
			}

			if (methodName is "LogError" or "LogCritical" &&
				(!requiresState || HasStructuredLoggingArgument(invocationSyntax, exceptionLocal, semanticModel, cancellationToken)))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsLoggerReceiver(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		ITypeSymbol? receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type;
		return IsLoggerType(receiverType);
	}

	private bool IsLoggerType(ITypeSymbol? type)
	{
		if (_loggerType is null || type is null)
			return false;

		if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _loggerType))
			return true;

		return type.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, _loggerType));
	}

	private static bool HasExplicitStateArgument(IInvocationOperation invocation)
	{
		IArgumentOperation? stateArgument = invocation.Arguments.FirstOrDefault(x => x.Parameter?.Name == "state");
		return stateArgument is not null && IsExplicitNonNullArgument(stateArgument);
	}

	private static bool HasStructuredLoggingArgument(
		InvocationExpressionSyntax invocation,
		ILocalSymbol exceptionLocal,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		for (int index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
		{
			ArgumentSyntax argument = invocation.ArgumentList.Arguments[index];

			if (ReferencesLocal(argument.Expression, exceptionLocal, semanticModel, cancellationToken))
				return invocation.ArgumentList.Arguments.Count >= index + 3;
		}

		return false;
	}

	private static bool IsExplicitNonNullArgument(IArgumentOperation argument)
	{
		if (argument.IsImplicit || argument.ArgumentKind == ArgumentKind.DefaultValue)
			return false;

		if (argument.Value.ConstantValue is { HasValue: true, Value: null })
			return false;

		return argument.Value is not IDefaultValueOperation;
	}

	private static bool ReferencesLocal(
		SyntaxNode node,
		ILocalSymbol local,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		foreach (IdentifierNameSyntax identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
		{
			if (SymbolEqualityComparer.Default.Equals(
				semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
				local))
			{
				return true;
			}
		}

		return false;
	}

	private static bool ShouldDescendInto(SyntaxNode node)
	{
		return node is not LocalFunctionStatementSyntax and not AnonymousFunctionExpressionSyntax;
	}
}
