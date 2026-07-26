using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
	private const string EntityMetadataName = "Umbrella.DataAccess.Abstractions.IEntity`1";

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
				INamedTypeSymbol? entityType = compilationContext.Compilation.GetTypeByMetadataName(EntityMetadataName);
				var reportedTypes = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);

				compilationContext.RegisterSyntaxNodeAction(
					syntaxContext => AnalyzeMethod(syntaxContext, analysis, entityType, reportedTypes),
					SyntaxKind.MethodDeclaration);
			});
	}

	private static void AnalyzeMethod(
		SyntaxNodeAnalysisContext context,
		PublicMethodExceptionHandlingAnalysis analysis,
		INamedTypeSymbol? entityType,
		ConcurrentDictionary<ISymbol, byte> reportedTypes)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;

		if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
			return;

		INamedTypeSymbol typeSymbol = methodSymbol.ContainingType;

		if (typeSymbol.TypeKind != TypeKind.Class ||
			typeSymbol.IsStatic ||
			typeSymbol.IsImplicitlyDeclared ||
			analysis.HasAccessibleLogger(typeSymbol) ||
			IsEntity(typeSymbol, entityType) ||
			!PublicMethodExceptionHandlingAnalysis.IsCandidate(methodSymbol) ||
			IsTestEntryPoint(methodSymbol) ||
			analysis.IsExempt(
				methodSymbol,
				methodDeclaration,
				context.SemanticModel,
				context.CancellationToken) ||
			!reportedTypes.TryAdd(typeSymbol, 0))
		{
			return;
		}

		Location? location = methodDeclaration
			.FirstAncestorOrSelf<TypeDeclarationSyntax>()?
			.Identifier
			.GetLocation();

		if (location is not null)
			context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeSymbol.Name, methodSymbol.Name));
	}

	private static bool IsEntity(INamedTypeSymbol typeSymbol, INamedTypeSymbol? entityType)
	{
		return entityType is not null &&
			typeSymbol.AllInterfaces.Any(
				x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, entityType));
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
