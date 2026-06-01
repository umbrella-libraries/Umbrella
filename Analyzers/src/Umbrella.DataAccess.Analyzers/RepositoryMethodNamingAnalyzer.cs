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
		title: "Repository method returning a single item must start with 'FindBy'",
		messageFormat: "Repository method '{0}' returns a single item but does not start with 'FindBy'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public methods in repository classes that return a single entity or result must use the 'FindBy' prefix to make intent clear and consistent across the codebase.");

	/// <summary>Diagnostic emitted when a method returning a collection does not start with 'FindAllBy'.</summary>
	public static readonly DiagnosticDescriptor FindAllByRule = new(
		id: "UDA002",
		title: "Repository method returning a collection must start with 'FindAllBy'",
		messageFormat: "Repository method '{0}' returns a collection but does not start with 'FindAllBy'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public methods in repository classes that return a collection of entities or results must use the 'FindAllBy' prefix to make intent clear and consistent across the codebase.");

	/// <summary>Diagnostic emitted when a method returning a count does not start with 'FindCount'.</summary>
	public static readonly DiagnosticDescriptor FindCountRule = new(
		id: "UDA003",
		title: "Repository method returning a count must start with 'FindCount'",
		messageFormat: "Repository method '{0}' returns a count but does not start with 'FindCount'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Public methods in repository classes that return an integer count must use the 'FindCount' prefix to make intent clear and consistent across the codebase.");

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

		context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
	}

	private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
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

		if (!IsRepositoryClass(method.ContainingType))
			return;

		ReturnTypeCategory category = ClassifyReturnType(method.ReturnType);

		DiagnosticDescriptor? rule = category switch
		{
			ReturnTypeCategory.SingleItem when !method.Name.StartsWith("FindBy", StringComparison.Ordinal) => FindByRule,
			ReturnTypeCategory.Collection when !method.Name.StartsWith("FindAllBy", StringComparison.Ordinal) => FindAllByRule,
			ReturnTypeCategory.Count when !method.Name.StartsWith("FindCount", StringComparison.Ordinal) => FindCountRule,
			ReturnTypeCategory.Exists when !method.Name.StartsWith("Exists", StringComparison.Ordinal) => ExistsRule,
			_ => null
		};

		if (rule is not null)
			context.ReportDiagnostic(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), method.Name));
	}

	private static bool IsRepositoryClass(INamedTypeSymbol classSymbol)
	{
		INamedTypeSymbol? current = classSymbol.BaseType;

		while (current is not null)
		{
			string name = current.OriginalDefinition.Name;

			if (name is "GenericDbRepository" or "ReadOnlyGenericDbRepository")
				return true;

			current = current.BaseType;
		}

		return false;
	}

	private static ReturnTypeCategory ClassifyReturnType(ITypeSymbol returnType)
	{
		ITypeSymbol? inner = UnwrapTaskOrValueTask(returnType);

		if (inner is null)
			return ReturnTypeCategory.Ignored;

		// Unwrap Nullable<T> for value types (e.g. int?, bool?)
		if (inner is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
			inner = nullable.TypeArguments[0];

		if (inner.SpecialType == SpecialType.System_Boolean)
			return ReturnTypeCategory.Exists;

		if (inner.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64)
			return ReturnTypeCategory.Count;

		if (inner is INamedTypeSymbol named)
		{
			string name = named.OriginalDefinition.Name;

			// IQueryable is handled by UDA005; ignore it here to avoid double-reporting
			if (name == "IQueryable")
				return ReturnTypeCategory.Ignored;

			if (name is "IEnumerable" or "IReadOnlyCollection" or "IReadOnlyList"
					 or "IList" or "ICollection" or "List" or "PaginatedResultModel")
			{
				return ReturnTypeCategory.Collection;
			}
		}

		return ReturnTypeCategory.SingleItem;
	}

	private static ITypeSymbol? UnwrapTaskOrValueTask(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.SpecialType == SpecialType.System_Void)
			return null;

		if (typeSymbol is INamedTypeSymbol named)
		{
			if (named.Arity == 0 && named.Name == "Task")
				return null;

			if (named.Arity == 1 && named.Name is "Task" or "ValueTask")
				return named.TypeArguments[0];
		}

		return typeSymbol;
	}

	private enum ReturnTypeCategory
	{
		Ignored,
		SingleItem,
		Collection,
		Count,
		Exists
	}
}
