using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.DataAccess.Analyzers;

/// <summary>
/// Roslyn analyzer that forbids <c>IQueryable&lt;T&gt;</c> as a public return type on repository classes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepositoryIQueryableAnalyzer : DiagnosticAnalyzer
{
	/// <summary>Diagnostic emitted when a public repository method returns <c>IQueryable&lt;T&gt;</c>.</summary>
	public static readonly DiagnosticDescriptor IQueryableForbiddenRule = new(
		id: "UDA005",
		title: "Repository methods must not return IQueryable<T>",
		messageFormat: "Repository method '{0}' returns IQueryable<T> which leaks the ORM abstraction; return a concrete collection type such as IReadOnlyCollection<T> instead",
		category: "UmbrellaDataAccess",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Returning IQueryable<T> from a public repository method allows callers to compose queries against the database context outside the repository boundary, breaking encapsulation. Return IReadOnlyCollection<T> or another concrete type instead.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [IQueryableForbiddenRule];

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

		if (!methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
			return;

		if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol method
			|| method.MethodKind != MethodKind.Ordinary)
		{
			return;
		}

		if (!analysis.IsRepositoryType(method.ContainingType))
			return;

		if (analysis.ContainsQueryable(method.ReturnType))
			context.ReportDiagnostic(Diagnostic.Create(IQueryableForbiddenRule, methodDeclaration.Identifier.GetLocation(), method.Name));
	}
}
