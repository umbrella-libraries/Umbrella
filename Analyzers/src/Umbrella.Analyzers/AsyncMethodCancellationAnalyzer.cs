using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that checks if eligible public async methods declare the canonical CancellationToken parameter.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncMethodCancellationAnalyzer : DiagnosticAnalyzer
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

	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA003";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Async methods should have a CancellationToken parameter",
		"Async method '{0}' should have a 'CancellationToken cancellationToken = default' parameter",
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
		context.RegisterCompilationStartAction(static compilationContext =>
		{
			var symbols = new KnownSymbols(compilationContext.Compilation);
			compilationContext.RegisterSymbolAction(
				context => AnalyzeMethod(context, symbols),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context, KnownSymbols symbols)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (!IsEligible(methodSymbol, symbols) ||
			HasCanonicalCancellationToken(methodSymbol, symbols.CancellationTokenType))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name));
	}

	private static bool IsEligible(IMethodSymbol methodSymbol, KnownSymbols symbols)
	{
		if (!methodSymbol.IsAsync ||
			methodSymbol.DeclaredAccessibility != Accessibility.Public ||
			methodSymbol.MethodKind != MethodKind.Ordinary ||
			methodSymbol.IsImplicitlyDeclared ||
			methodSymbol.IsOverride ||
			methodSymbol.PartialDefinitionPart is not null ||
			methodSymbol.PartialImplementationPart is not null ||
			methodSymbol.DeclaringSyntaxReferences.Length == 0 ||
			!methodSymbol.Locations.Any(static location => location.IsInSource) ||
			!IsSupportedReturnType(methodSymbol.ReturnType, symbols) ||
			IsDeclaredOnComponent(methodSymbol.ContainingType, symbols.ComponentBaseType) ||
			methodSymbol.ExplicitInterfaceImplementations.Length > 0 ||
			IsImplicitInterfaceImplementation(methodSymbol) ||
			HasExcludedAttribute(methodSymbol))
		{
			return false;
		}

		return true;
	}

	private static bool IsSupportedReturnType(ITypeSymbol returnType, KnownSymbols symbols)
	{
		var originalDefinition = returnType.OriginalDefinition;

		return SymbolEqualityComparer.Default.Equals(originalDefinition, symbols.TaskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, symbols.GenericTaskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, symbols.ValueTaskType) ||
			SymbolEqualityComparer.Default.Equals(originalDefinition, symbols.GenericValueTaskType);
	}

	private static bool IsDeclaredOnComponent(INamedTypeSymbol? containingType, INamedTypeSymbol? componentBaseType)
	{
		if (componentBaseType is null)
			return false;

		for (var type = containingType; type is not null; type = type.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(type, componentBaseType))
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

	private static bool HasCanonicalCancellationToken(IMethodSymbol methodSymbol, INamedTypeSymbol? cancellationTokenType)
	{
		if (cancellationTokenType is null)
			return false;

		foreach (var parameter in methodSymbol.Parameters)
		{
			if (!SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType) ||
				parameter.Name != "cancellationToken")
			{
				continue;
			}

			foreach (var syntaxReference in parameter.DeclaringSyntaxReferences)
			{
				if (syntaxReference.GetSyntax() is ParameterSyntax parameterSyntax &&
					parameterSyntax.Default?.Value.IsKind(SyntaxKind.DefaultLiteralExpression) == true)
				{
					return true;
				}
			}
		}

		return false;
	}

	private sealed class KnownSymbols
	{
		public KnownSymbols(Compilation compilation)
		{
			CancellationTokenType = compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
			ComponentBaseType = compilation.GetTypeByMetadataName(ComponentBaseMetadataName);
			TaskType = compilation.GetTypeByMetadataName(TaskMetadataName);
			GenericTaskType = compilation.GetTypeByMetadataName(GenericTaskMetadataName);
			ValueTaskType = compilation.GetTypeByMetadataName(ValueTaskMetadataName);
			GenericValueTaskType = compilation.GetTypeByMetadataName(GenericValueTaskMetadataName);
		}

		public INamedTypeSymbol? CancellationTokenType { get; }

		public INamedTypeSymbol? ComponentBaseType { get; }

		public INamedTypeSymbol? TaskType { get; }

		public INamedTypeSymbol? GenericTaskType { get; }

		public INamedTypeSymbol? ValueTaskType { get; }

		public INamedTypeSymbol? GenericValueTaskType { get; }
	}
}
