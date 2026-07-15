using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Umbrella.Analyzers;

internal sealed class AsyncMethodCancellationAnalysis
{
	private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
	private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";
	private const string TaskMetadataName = "System.Threading.Tasks.Task";
	private const string GenericTaskMetadataName = "System.Threading.Tasks.Task`1";
	private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";
	private const string GenericValueTaskMetadataName = "System.Threading.Tasks.ValueTask`1";

	private static readonly ImmutableHashSet<string> _excludedAttributeMetadataNames = ImmutableHashSet.Create(
		StringComparer.Ordinal,
		"Microsoft.JSInterop.JSInvokableAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.AssemblyCleanupAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.AssemblyInitializeAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.ClassCleanupAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.ClassInitializeAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute",
		"Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",
		"NUnit.Framework.OneTimeSetUpAttribute",
		"NUnit.Framework.OneTimeTearDownAttribute",
		"NUnit.Framework.SetUpAttribute",
		"NUnit.Framework.TearDownAttribute",
		"NUnit.Framework.TestAttribute",
		"NUnit.Framework.TestCaseAttribute",
		"NUnit.Framework.TestCaseSourceAttribute",
		"NUnit.Framework.TheoryAttribute",
		"Xunit.FactAttribute",
		"Xunit.TheoryAttribute");

	private readonly INamedTypeSymbol? _componentBaseType;
	private readonly INamedTypeSymbol? _taskType;
	private readonly INamedTypeSymbol? _genericTaskType;
	private readonly INamedTypeSymbol? _valueTaskType;
	private readonly INamedTypeSymbol? _genericValueTaskType;
	private readonly IMethodSymbol? _throwIfCancellationRequestedMethod;

	internal AsyncMethodCancellationAnalysis(Compilation compilation)
	{
		CancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
		_componentBaseType = compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
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
		return methodSymbol.IsAsync &&
			methodSymbol.DeclaredAccessibility == Accessibility.Public &&
			methodSymbol.MethodKind == MethodKind.Ordinary &&
			!methodSymbol.IsImplicitlyDeclared &&
			!methodSymbol.IsOverride &&
			methodSymbol.PartialDefinitionPart is null &&
			methodSymbol.PartialImplementationPart is null &&
			methodSymbol.DeclaringSyntaxReferences.Length > 0 &&
			methodSymbol.Locations.Any(static location => location.IsInSource) &&
			IsSupportedReturnType(methodSymbol.ReturnType) &&
			!IsDeclaredOnComponent(methodSymbol.ContainingType) &&
			methodSymbol.ExplicitInterfaceImplementations.Length == 0 &&
			!IsImplicitInterfaceImplementation(methodSymbol) &&
			!HasExcludedAttribute(methodSymbol);
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

	private bool IsDeclaredOnComponent(INamedTypeSymbol? containingType)
	{
		if (_componentBaseType is null)
			return false;

		for (var type = containingType; type is not null; type = type.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(type, _componentBaseType))
				return true;
		}

		return false;
	}

	private static bool IsImplicitInterfaceImplementation(IMethodSymbol methodSymbol)
	{
		foreach (var interfaceType in methodSymbol.ContainingType.AllInterfaces)
		{
			foreach (var interfaceMember in interfaceType.GetMembers().OfType<IMethodSymbol>())
			{
				var implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMember);

				if (SymbolEqualityComparer.Default.Equals(implementation, methodSymbol))
					return true;
			}
		}

		return false;
	}

	private static bool HasExcludedAttribute(IMethodSymbol methodSymbol)
	{
		foreach (var attribute in methodSymbol.GetAttributes())
		{
			for (var attributeType = attribute.AttributeClass; attributeType is not null; attributeType = attributeType.BaseType)
			{
				if (_excludedAttributeMetadataNames.Contains(attributeType.ToDisplayString()))
					return true;
			}
		}

		return false;
	}
}
