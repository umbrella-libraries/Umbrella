using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Umbrella.Analyzers;

internal sealed class ChangeablePublicMethodAnalysis
{
	private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

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

	internal ChangeablePublicMethodAnalysis(Compilation compilation)
	{
		_componentBaseType = compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
	}

	internal bool IsEligible(IMethodSymbol methodSymbol)
	{
		return methodSymbol.DeclaredAccessibility == Accessibility.Public &&
			methodSymbol.MethodKind == MethodKind.Ordinary &&
			!methodSymbol.IsImplicitlyDeclared &&
			!methodSymbol.IsOverride &&
			!methodSymbol.IsExtern &&
			methodSymbol.PartialDefinitionPart is null &&
			methodSymbol.PartialImplementationPart is null &&
			methodSymbol.DeclaringSyntaxReferences.Length > 0 &&
			methodSymbol.Locations.Any(static location => location.IsInSource) &&
			!IsDeclaredOnComponent(methodSymbol.ContainingType) &&
			methodSymbol.ExplicitInterfaceImplementations.Length == 0 &&
			!IsImplicitInterfaceImplementation(methodSymbol) &&
			!HasExcludedAttribute(methodSymbol);
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
