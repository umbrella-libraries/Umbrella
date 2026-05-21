using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that warns when a controller overrides a standard CRUD endpoint method without calling the base
/// implementation. Bypassing the base call skips any cross-cutting concerns registered in the base lifecycle
/// hooks (BeforeCreateEntityAsync, AfterDeleteEntityAsync, etc.).
/// Suppress with <c>[NonAction]</c> when the intent is to intentionally disable the endpoint.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerEndpointOverrideAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA026";

	private static readonly ImmutableHashSet<string> _crudMethodNames = ImmutableHashSet.Create(
		StringComparer.Ordinal,
		"GetAsync",
		"PostAsync",
		"PutAsync",
		"DeleteAsync",
		"PatchAsync",
		"SearchSlimAsync",
		"SearchAsync");

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Controller endpoint override must call base method",
		"Override of '{0}' in '{1}' does not call base.{0}(). This skips base lifecycle hooks. Use Before/After lifecycle hook overrides for custom logic, or apply [NonAction] to intentionally disable the endpoint.",
		"Architecture",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true);

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
		{
			throw new ArgumentNullException(nameof(context));
		}

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		if (!methodSymbol.IsOverride)
		{
			return;
		}

		if (!_crudMethodNames.Contains(methodSymbol.Name))
		{
			return;
		}

		if (!methodSymbol.ContainingType.Name.EndsWith("Controller", StringComparison.Ordinal))
		{
			return;
		}

		if (methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
			is not MethodDeclarationSyntax methodDecl)
		{
			return;
		}

		if (HasNonActionAttribute(methodDecl))
		{
			return;
		}

		if (methodDecl.Body is null && methodDecl.ExpressionBody is null)
		{
			return;
		}

		if (!ContainsBaseCall(methodDecl, methodSymbol.Name))
		{
			var diagnostic = Diagnostic.Create(
				Rule,
				methodSymbol.Locations[0],
				methodSymbol.Name,
				methodSymbol.ContainingType.Name);

			context.ReportDiagnostic(diagnostic);
		}
	}

	private static bool HasNonActionAttribute(MethodDeclarationSyntax methodDecl)
	{
		foreach (var attrList in methodDecl.AttributeLists)
		{
			foreach (var attr in attrList.Attributes)
			{
				string? name = attr.Name switch
				{
					IdentifierNameSyntax id => id.Identifier.Text,
					QualifiedNameSyntax qn => qn.Right.Identifier.Text,
					_ => null
				};

				if (name is "NonAction" or "NonActionAttribute")
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool ContainsBaseCall(MethodDeclarationSyntax methodDecl, string methodName)
	{
		SyntaxNode? body = methodDecl.Body ?? (SyntaxNode?)methodDecl.ExpressionBody;

		if (body is null)
		{
			return false;
		}

		foreach (var node in body.DescendantNodes())
		{
			if (node is InvocationExpressionSyntax invocation &&
				invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
				memberAccess.Expression is BaseExpressionSyntax &&
				memberAccess.Name.Identifier.Text == methodName)
			{
				return true;
			}
		}

		return false;
	}
}
