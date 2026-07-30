using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.DataAccess.Analyzers;

/// <summary>
/// Roslyn analyzer that enforces naming conventions for public methods on repository classes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepositoryMethodNamingAnalyzer : DiagnosticAnalyzer
{
	private const string Category = "UmbrellaDataAccess";

	/// <summary>Diagnostic emitted when a method returning a single item does not start with 'FindBy'.</summary>
	public static readonly DiagnosticDescriptor FindByRule = new(
		id: "UDA001",
		title: "Single-result repository query must start with 'Find'",
		messageFormat: "Repository query method '{0}' returns a single result but does not start with 'Find'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public query methods in repository classes that return a single entity, projection, or aggregate result must use the 'Find' prefix to make intent clear and consistent across the codebase.");

	/// <summary>Diagnostic emitted when a method returning a collection does not start with 'FindAllBy'.</summary>
	public static readonly DiagnosticDescriptor FindAllByRule = new(
		id: "UDA002",
		title: "Collection repository query must start with 'FindAll'",
		messageFormat: "Repository query method '{0}' returns a collection but does not start with 'FindAll'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public query methods in repository classes that return a collection of entities or results must use the 'FindAll' prefix to make intent clear and consistent across the codebase.");

	/// <summary>Diagnostic emitted when a method returning a count does not start with 'FindCount'.</summary>
	public static readonly DiagnosticDescriptor FindCountRule = new(
		id: "UDA003",
		title: "Count repository query must start with 'Find' and identify the count",
		messageFormat: "Repository query method '{0}' returns a count but its name does not start with 'Find' and contain 'Count'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public query methods in repository classes that return an integer count must start with 'Find' and identify the count in the method name.");

	/// <summary>Diagnostic emitted when a method returning a boolean does not start with 'Exists'.</summary>
	public static readonly DiagnosticDescriptor ExistsRule = new(
		id: "UDA004",
		title: "Repository method returning a boolean must start with 'Exists'",
		messageFormat: "Repository method '{0}' returns a boolean but does not start with 'Exists'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public methods in repository classes that return a boolean must use the 'Exists' prefix to make intent clear and consistent across the codebase.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[FindByRule, FindAllByRule, FindCountRule, ExistsRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(compilationContext =>
		{
			var analysis = RepositoryAnalysis.Create(compilationContext.Compilation);
			compilationContext.RegisterSyntaxNodeAction(x => AnalyzeMethod(x, analysis), SyntaxKind.MethodDeclaration);
		});
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, RepositoryAnalysis analysis)
	{
		var methodDeclaration = (MethodDeclarationSyntax)context.Node;

		if (!methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword)
			|| methodDeclaration.Modifiers.Any(SyntaxKind.OverrideKeyword)
			|| methodDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
		{
			return;
		}

		if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol method
			|| method.MethodKind != MethodKind.Ordinary)
		{
			return;
		}

		if (!analysis.IsRepositoryType(method.ContainingType)
			|| RepositoryAnalysis.IsCommandMethodName(method.Name))
		{
			return;
		}

		var category = analysis.ClassifyReturnType(method.ReturnType);

		var rule = category switch
		{
			RepositoryReturnTypeCategory.SingleItem when !RepositoryAnalysis.IsValidName(method.Name, category) => FindByRule,
			RepositoryReturnTypeCategory.Collection when !RepositoryAnalysis.IsValidName(method.Name, category) => FindAllByRule,
			RepositoryReturnTypeCategory.Count when !RepositoryAnalysis.IsValidName(method.Name, category) => FindCountRule,
			RepositoryReturnTypeCategory.Exists when !RepositoryAnalysis.IsValidName(method.Name, category) => ExistsRule,
			_ => null
		};

		if (rule is not null)
		{
			context.ReportDiagnostic(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), method.Name));
		}
	}
}
