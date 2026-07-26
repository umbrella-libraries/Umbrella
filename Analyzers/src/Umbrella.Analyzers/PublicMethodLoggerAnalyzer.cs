using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Ensures classes and records containing public operational instance methods expose an accessible
/// <c>Microsoft.Extensions.Logging.ILogger</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicMethodLoggerAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA021";

	private static readonly ImmutableHashSet<string> _testEntryPointAttributeMetadataNames = ImmutableHashSet.Create(
		StringComparer.Ordinal,
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
	/// The diagnostic rule for this analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Types with public operational methods should provide an ILogger",
		"Type '{0}' should provide an accessible ILogger because public method '{1}' contains operational code",
		"CodeStyle",
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "UA008 can enforce state-aware exception logging only when a type exposes an ILogger. Classes and records with public operational instance methods should receive or inherit an ILogger so failures can be logged with method state.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterCompilationStartAction(
			compilationContext =>
			{
				var analysis = new PublicMethodExceptionHandlingAnalysis(compilationContext.Compilation);
				compilationContext.RegisterSymbolAction(
					symbolContext => AnalyzeType(symbolContext, analysis),
					SymbolKind.NamedType);
			});
	}

	private static void AnalyzeType(
		SymbolAnalysisContext context,
		PublicMethodExceptionHandlingAnalysis analysis)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		if (typeSymbol.TypeKind != TypeKind.Class ||
			typeSymbol.IsStatic ||
			typeSymbol.IsImplicitlyDeclared ||
			analysis.HasAccessibleLogger(typeSymbol))
		{
			return;
		}

		foreach (IMethodSymbol methodSymbol in typeSymbol.GetMembers().OfType<IMethodSymbol>())
		{
			if (!PublicMethodExceptionHandlingAnalysis.IsCandidate(methodSymbol) ||
				IsTestEntryPoint(methodSymbol))
			{
				continue;
			}

			foreach (SyntaxReference syntaxReference in methodSymbol.DeclaringSyntaxReferences)
			{
				if (syntaxReference.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodDeclaration)
					continue;

#pragma warning disable RS1030 // A type-level diagnostic requires checking every method with its semantic model.
				SemanticModel semanticModel = context.Compilation.GetSemanticModel(methodDeclaration.SyntaxTree);
#pragma warning restore RS1030

				if (analysis.IsExempt(
					methodSymbol,
					methodDeclaration,
					semanticModel,
					context.CancellationToken))
				{
					continue;
				}

				Location? location = typeSymbol.Locations.FirstOrDefault(static x => x.IsInSource);

				if (location is not null)
				context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeSymbol.Name, methodSymbol.Name));

				return;
			}
		}
	}

	private static bool IsTestEntryPoint(IMethodSymbol methodSymbol)
	{
		foreach (AttributeData attribute in methodSymbol.GetAttributes())
		{
			for (INamedTypeSymbol? attributeType = attribute.AttributeClass;
				attributeType is not null;
				attributeType = attributeType.BaseType)
			{
				if (_testEntryPointAttributeMetadataNames.Contains(attributeType.ToDisplayString()))
					return true;
			}
		}

		return false;
	}
}
