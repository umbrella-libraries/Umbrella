using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Umbrella.Analyzers;

internal sealed class PublicMethodExceptionHandlingAnalysis
{
	private const string ExceptionMetadataName = "System.Exception";
	private const string IAsyncDisposableMetadataName = "System.IAsyncDisposable";
	private const string IDisposableMetadataName = "System.IDisposable";
	private const string ILoggerMetadataName = "Microsoft.Extensions.Logging.ILogger";
	private const string DoesNotReturnAttributeMetadataName = "System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute";
	private const string NonActionAttributeMetadataName = "Microsoft.AspNetCore.Mvc.NonActionAttribute";
	private const string RequestDelegateMetadataName = "Microsoft.AspNetCore.Http.RequestDelegate";
	private const string TaskMetadataName = "System.Threading.Tasks.Task";
	private const string TrimmableMetadataName = "Umbrella.Utilities.Text.IUmbrellaTrimmable";
	private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";

	private readonly Compilation _compilation;
	private readonly INamedTypeSymbol? _doesNotReturnAttributeType;
	private readonly INamedTypeSymbol? _exceptionType;
	private readonly INamedTypeSymbol? _iAsyncDisposableType;
	private readonly INamedTypeSymbol? _iDisposableType;
	private readonly INamedTypeSymbol? _loggerType;
	private readonly INamedTypeSymbol? _nonActionAttributeType;
	private readonly ParameterValidationAnalysis _parameterValidationAnalysis;
	private readonly INamedTypeSymbol? _requestDelegateType;
	private readonly INamedTypeSymbol? _taskType;
	private readonly INamedTypeSymbol? _trimmableType;
	private readonly INamedTypeSymbol? _valueTaskType;

	internal PublicMethodExceptionHandlingAnalysis(Compilation compilation)
	{
		_compilation = compilation;
		_doesNotReturnAttributeType = compilation.GetTypeByMetadataName(DoesNotReturnAttributeMetadataName);
		_exceptionType = compilation.GetTypeByMetadataName(ExceptionMetadataName);
		_iAsyncDisposableType = compilation.GetTypeByMetadataName(IAsyncDisposableMetadataName);
		_iDisposableType = compilation.GetTypeByMetadataName(IDisposableMetadataName);
		_loggerType = compilation.GetTypeByMetadataName(ILoggerMetadataName);
		_nonActionAttributeType = compilation.GetTypeByMetadataName(NonActionAttributeMetadataName);
		_parameterValidationAnalysis = new ParameterValidationAnalysis(compilation);
		_requestDelegateType = compilation.GetTypeByMetadataName(RequestDelegateMetadataName);
		_taskType = compilation.GetTypeByMetadataName(TaskMetadataName);
		_trimmableType = compilation.GetTypeByMetadataName(TrimmableMetadataName);
		_valueTaskType = compilation.GetTypeByMetadataName(ValueTaskMetadataName);
	}

	internal bool IsEligible(IMethodSymbol methodSymbol)
	{
		return IsCandidate(methodSymbol) &&
			HasAccessibleLogger(methodSymbol.ContainingType);
	}

	internal static bool IsCandidate(IMethodSymbol methodSymbol)
	{
		return methodSymbol.DeclaredAccessibility == Accessibility.Public &&
			methodSymbol.MethodKind == MethodKind.Ordinary &&
			!methodSymbol.IsStatic &&
			!methodSymbol.IsAbstract &&
			!methodSymbol.IsImplicitlyDeclared &&
			!methodSymbol.IsExtern &&
			methodSymbol.DeclaringSyntaxReferences.Length > 0 &&
			methodSymbol.Locations.Any(static location => location.IsInSource);
	}

	internal bool IsExempt(
		IMethodSymbol methodSymbol,
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		return methodDeclaration is { Body: null, ExpressionBody: null } ||
			HasAttribute(methodSymbol, _doesNotReturnAttributeType) ||
			HasNonActionAttribute(methodSymbol) ||
			IsMiddlewareEntryPoint(methodSymbol) ||
			IsDisposalImplementation(methodSymbol) ||
			IsInterfaceImplementation(methodSymbol, _trimmableType, "TrimAllStringProperties") ||
			IsDirectBaseForwarder(methodSymbol, methodDeclaration, semanticModel, cancellationToken) ||
			IsTrivialNoOp(methodDeclaration, semanticModel, cancellationToken);
	}

	private static bool HasAttribute(IMethodSymbol methodSymbol, INamedTypeSymbol? attributeType) =>
		attributeType is not null &&
		methodSymbol.GetAttributes().Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));

	internal TryStatementSyntax? FindOuterTryStatement(
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (methodDeclaration.Body is not { } body)
			return null;

		int index = 0;

		while (index < body.Statements.Count &&
			(_parameterValidationAnalysis.IsValidationPreambleStatement(body.Statements[index], semanticModel, cancellationToken) ||
				IsSafeLocalDeclaration(body.Statements[index], semanticModel, cancellationToken)))
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

		return tryStatement.Catches
			.Where(x => RequiresLogging(x, semanticModel, cancellationToken))
			.All(x => ContainsRequiredLogging(x, semanticModel, requiresState, cancellationToken));
	}

	private bool HasNonActionAttribute(IMethodSymbol methodSymbol)
	{
		if (_nonActionAttributeType is null)
			return false;

		return methodSymbol.GetAttributes().Any(
			x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, _nonActionAttributeType));
	}

	private bool IsMiddlewareEntryPoint(IMethodSymbol methodSymbol)
	{
		if (_requestDelegateType is null || methodSymbol.Name is not ("Invoke" or "InvokeAsync"))
			return false;

		return methodSymbol.ContainingType.InstanceConstructors
			.SelectMany(x => x.Parameters)
			.Any(x => SymbolEqualityComparer.Default.Equals(x.Type.OriginalDefinition, _requestDelegateType));
	}

	private bool IsDisposalImplementation(IMethodSymbol methodSymbol)
	{
		return IsInterfaceImplementation(methodSymbol, _iDisposableType, "Dispose") ||
			IsInterfaceImplementation(methodSymbol, _iAsyncDisposableType, "DisposeAsync");
	}

	private static bool IsInterfaceImplementation(
		IMethodSymbol methodSymbol,
		INamedTypeSymbol? interfaceType,
		string methodName)
	{
		if (interfaceType is null)
			return false;

		foreach (IMethodSymbol interfaceMethod in interfaceType.GetMembers(methodName).OfType<IMethodSymbol>())
		{
			ISymbol? implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod);

			if (SymbolEqualityComparer.Default.Equals(implementation, methodSymbol))
				return true;
		}

		return false;
	}

	private bool IsDirectBaseForwarder(
		IMethodSymbol methodSymbol,
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		InvocationExpressionSyntax? invocation = GetForwardedInvocation(
			methodDeclaration,
			semanticModel,
			cancellationToken);

		if (invocation is null ||
			semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol targetMethod)
		{
			return false;
		}

		targetMethod = targetMethod.ReducedFrom ?? targetMethod;

		if (!IsDeclaredOnBaseType(methodSymbol.ContainingType, targetMethod.ContainingType))
			return false;

		return methodSymbol.Parameters.All(
			x => ReferencesParameter(invocation, x, semanticModel, cancellationToken));
	}

	private InvocationExpressionSyntax? GetForwardedInvocation(
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		ExpressionSyntax? expression = methodDeclaration.ExpressionBody?.Expression;

		if (expression is null && methodDeclaration.Body is { } body)
		{
			int index = 0;

			while (index < body.Statements.Count &&
				_parameterValidationAnalysis.IsValidationPreambleStatement(body.Statements[index], semanticModel, cancellationToken))
			{
				index++;
			}

			if (index != body.Statements.Count - 1)
				return null;

			expression = body.Statements[index] switch
			{
				ReturnStatementSyntax returnStatement => returnStatement.Expression,
				ExpressionStatementSyntax expressionStatement => expressionStatement.Expression,
				_ => null
			};
		}

		while (expression is AwaitExpressionSyntax awaitExpression)
			expression = awaitExpression.Expression;

		return expression as InvocationExpressionSyntax;
	}

	private static bool IsDeclaredOnBaseType(INamedTypeSymbol containingType, INamedTypeSymbol targetContainingType)
	{
		for (INamedTypeSymbol? baseType = containingType.BaseType; baseType is not null; baseType = baseType.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(
				baseType.OriginalDefinition,
				targetContainingType.OriginalDefinition))
			{
				return true;
			}
		}

		return false;
	}

	private static bool ReferencesParameter(
		SyntaxNode node,
		IParameterSymbol parameter,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		foreach (IdentifierNameSyntax identifier in node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
		{
			if (SymbolEqualityComparer.Default.Equals(
				semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
				parameter))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsTrivialNoOp(
		MethodDeclarationSyntax methodDeclaration,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (methodDeclaration.ExpressionBody is { Expression: { } expression })
			return IsTrivialExpression(expression, semanticModel, cancellationToken);

		if (methodDeclaration.Body is not { } body)
			return false;

		int index = 0;

		while (index < body.Statements.Count &&
			_parameterValidationAnalysis.IsValidationPreambleStatement(body.Statements[index], semanticModel, cancellationToken))
		{
			index++;
		}

		if (index == body.Statements.Count)
			return true;

		return index == body.Statements.Count - 1 &&
			body.Statements[index] is ReturnStatementSyntax returnStatement &&
			(returnStatement.Expression is null ||
				IsTrivialExpression(returnStatement.Expression, semanticModel, cancellationToken));
	}

	private bool IsTrivialExpression(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (semanticModel.GetConstantValue(expression, cancellationToken).HasValue ||
			expression is DefaultExpressionSyntax)
		{
			return true;
		}

		if (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is not IPropertySymbol property ||
			property.Name != "CompletedTask")
		{
			return false;
		}

		return SymbolEqualityComparer.Default.Equals(property.ContainingType.OriginalDefinition, _taskType) ||
			SymbolEqualityComparer.Default.Equals(property.ContainingType.OriginalDefinition, _valueTaskType);
	}

	private static bool IsSafeLocalDeclaration(
		StatementSyntax statement,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (statement is not LocalDeclarationStatementSyntax localDeclaration ||
			!localDeclaration.UsingKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None) ||
			!localDeclaration.AwaitKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None))
		{
			return false;
		}

		return localDeclaration.Declaration.Variables.All(
			x => x.Initializer is null ||
				x.Initializer.Value is DefaultExpressionSyntax ||
				semanticModel.GetConstantValue(x.Initializer.Value, cancellationToken).HasValue);
	}

	private bool RequiresLogging(
		CatchClauseSyntax catchClause,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		if (catchClause.Declaration is null)
			return true;

		ITypeSymbol? caughtType = semanticModel.GetTypeInfo(catchClause.Declaration.Type, cancellationToken).Type;

		return _exceptionType is null ||
			caughtType is null ||
			SymbolEqualityComparer.Default.Equals(caughtType.OriginalDefinition, _exceptionType);
	}

	internal bool HasAccessibleLogger(INamedTypeSymbol containingType)
	{
		if (_loggerType is null)
			return false;

		if (containingType.InstanceConstructors
			.SelectMany(static constructor => constructor.Parameters)
			.Any(parameter => IsLoggerType(parameter.Type) && IsPrimaryConstructorParameter(parameter)))
		{
			return true;
		}

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

	private static bool IsPrimaryConstructorParameter(IParameterSymbol parameter)
	{
		return parameter.DeclaringSyntaxReferences.Any(
			static syntaxReference =>
				syntaxReference.GetSyntax() is ParameterSyntax
				{
					Parent.Parent: TypeDeclarationSyntax
				});
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
