using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// An analyzer that ensures all public methods are wrapped in an outer try...catch block.
/// If an ILogger instance exists, it ensures the logging pattern matches the specified conventions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PublicMethodTryCatchAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The diagnostic ID for this analyzer.
	/// </summary>
	public const string DiagnosticId = "UA008";

	/// <summary>
	/// Gets the diagnostic rule for the analyzer.
	/// </summary>
	public static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Public methods should be wrapped in a try...catch block",
		"Public method '{0}' should be wrapped in a try...catch block, and logging should follow the specified pattern if ILogger is available",
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
		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context)
	{
		var methodSymbol = (IMethodSymbol)context.Symbol;

		// Only analyze public methods
		if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
			return;

		if (methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax methodDeclaration)
			return;

		// Expression-bodied methods (=> expr) cannot contain a try-catch — flag them immediately
		if (methodDeclaration.ExpressionBody is not null)
		{
			ReportDiagnostic(context, methodSymbol);
			return;
		}

		// Abstract, extern, or partial declaration with no body — skip
		if (methodDeclaration.Body is null)
			return;

		// Check if the method body is wrapped in a try...catch block
		var firstStatement = methodDeclaration.Body.Statements.FirstOrDefault();
		if (firstStatement is not TryStatementSyntax tryStatement || tryStatement.Catches.Count == 0)
		{
			ReportDiagnostic(context, methodSymbol);
			return;
		}

		// Check for ILogger instance in the class or base class
		var containingType = methodSymbol.ContainingType;
		var loggerFieldOrProperty = FindLoggerFieldOrProperty(containingType);

		if (loggerFieldOrProperty != null)
		{
			// Ensure the catch block contains a logging statement
			if (!ContainsLoggingStatement(tryStatement, loggerFieldOrProperty.Name))
			{
				ReportDiagnostic(context, methodSymbol);
			}
		}
	}

	private static ISymbol? FindLoggerFieldOrProperty(INamedTypeSymbol containingType)
	{
		// Check fields and properties in the current type
		foreach (var member in containingType.GetMembers())
		{
			if (member is IFieldSymbol field && field.Type.Name == "ILogger")
				return field;

			if (member is IPropertySymbol property && property.Type.Name == "ILogger")
				return property;
		}

		// Check base types for protected members
		var baseType = containingType.BaseType;
		while (baseType != null)
		{
			foreach (var member in baseType.GetMembers())
			{
				if (member.DeclaredAccessibility == Accessibility.Public || member.DeclaredAccessibility == Accessibility.Protected)
				{
					if (member is IFieldSymbol field && field.Type.Name == "ILogger")
						return field;

					if (member is IPropertySymbol property && property.Type.Name == "ILogger")
						return property;
				}
			}

			baseType = baseType.BaseType;
		}

		return null;
	}

	private static bool ContainsLoggingStatement(TryStatementSyntax tryStatement, string loggerName)
	{
		foreach (var catchClause in tryStatement.Catches)
		{
			// Check logger calls in the catch block body
			if (catchClause.Block.Statements.Any(statement =>
				statement is ExpressionStatementSyntax expressionStatement &&
				expressionStatement.Expression is InvocationExpressionSyntax invocation &&
				invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
				memberAccess.Expression is IdentifierNameSyntax identifier &&
				identifier.Identifier.Text == loggerName))
			{
				return true;
			}

			// Check logger calls in the when(...) exception filter expression
			// e.g. catch (Exception exc) when (exc is not X && Logger.WriteError(exc, ...))
			if (catchClause.Filter is { } filter &&
				ContainsLoggerInvocationInExpression(filter.FilterExpression, loggerName))
			{
				return true;
			}
		}

		return false;
	}

	private static bool ContainsLoggerInvocationInExpression(ExpressionSyntax expression, string loggerName)
	{
		// Direct invocation: _logger.WriteError(...) / Logger.WriteError(...)
		if (expression is InvocationExpressionSyntax invocation &&
			invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
			memberAccess.Expression is IdentifierNameSyntax identifier &&
			identifier.Identifier.Text == loggerName)
		{
			return true;
		}

		// Binary expression: left && right / left || right
		if (expression is BinaryExpressionSyntax binary)
		{
			return ContainsLoggerInvocationInExpression(binary.Left, loggerName) ||
				   ContainsLoggerInvocationInExpression(binary.Right, loggerName);
		}

		// Parenthesised expression
		if (expression is ParenthesizedExpressionSyntax parenthesized)
			return ContainsLoggerInvocationInExpression(parenthesized.Expression, loggerName);

		return false;
	}

	private static void ReportDiagnostic(SymbolAnalysisContext context, IMethodSymbol methodSymbol)
	{
		var diagnostic = Diagnostic.Create(Rule, methodSymbol.Locations[0], methodSymbol.Name);
		context.ReportDiagnostic(diagnostic);
	}
}