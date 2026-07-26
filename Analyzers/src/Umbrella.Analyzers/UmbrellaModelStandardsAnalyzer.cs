using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Analyzers;

/// <summary>
/// Roslyn analyzer that enforces coding standards for model classes and view models
/// following the Umbrella framework conventions for immutability and type safety.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer enforces the following rules:
/// </para>
/// <list type="bullet">
/// <item><description>UMS001: Model types must be records for better immutability guarantees</description></item>
/// <item><description>UMS002: Model properties must use the 'required' keyword for initialization safety</description></item>
/// <item><description>UMS003: Model properties must have getter and be init-only to prevent mutation</description></item>
/// <item><description>UA014: Collection properties must use a read-only collection type</description></item>
/// </list>
/// <para>
/// The analyzer targets types with names ending in: Model, ModelBase, ViewModel, ViewModelBase, or QueryResult.
/// </para>
/// <para>
/// Opt-out attributes are available to bypass specific rules when justified.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UmbrellaModelStandardsAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Diagnostic rule that requires model types to be defined as records instead of classes.
	/// </summary>
	/// <remarks>
	/// Records provide better immutability guarantees and value-based equality semantics
	/// which are essential for model types in the Umbrella framework.
	/// </remarks>
	public static readonly DiagnosticDescriptor ModelMustBeRecordRule = new(
		id: "UA011",
		title: "Model types must be records",
		messageFormat: "The model type '{0}' should be defined as a record",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Per Umbrella standards, model types should be defined as records for better immutability guarantees.");

	/// <summary>
	/// Diagnostic rule that requires model properties to use the 'required' keyword.
	/// </summary>
	/// <remarks>
	/// The 'required' keyword ensures that properties are initialized at object creation time,
	/// preventing null reference exceptions and improving type safety.
	/// </remarks>
	public static readonly DiagnosticDescriptor PropertiesMustBeRequiredRule = new(
		id: "UA012",
		title: "Model properties must use the required keyword",
		messageFormat: "Property '{0}' in model type '{1}' should use the 'required' keyword",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Per Umbrella standards, properties in model types should use the 'required' keyword.");

	/// <summary>
	/// Diagnostic rule that requires model properties to have a getter and be init-only.
	/// </summary>
	/// <remarks>
	/// Properties should be readable (getter) and only settable during initialization (init)
	/// to maintain immutability after object creation.
	/// </remarks>
	public static readonly DiagnosticDescriptor PropertiesMustBeGetterInitOnlyRule = new(
		id: "UA013",
		title: "Model properties must have getter and be init-only",
		messageFormat: "Property '{0}' in model type '{1}' should have a getter and be init-only",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Per Umbrella standards, properties in model types should have a getter and be init-only.");

	/// <summary>
	/// Diagnostic rule that requires collection properties to use a read-only collection type.
	/// </summary>
	/// <remarks>
	/// Using a read-only collection contract prevents external code from modifying the collection
	/// contents through the model, maintaining immutability and preventing unintended side effects.
	/// </remarks>
	public static readonly DiagnosticDescriptor CollectionsMustBeReadOnlyRule = new(
		id: "UA014",
		title: "Collection properties must use a read-only collection type",
		messageFormat: "Collection property '{0}' in model type '{1}' should use a read-only collection type",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Per Umbrella standards, collection properties in model types should expose a read-only collection contract or a recognized immutable collection type.");

	/// <summary>
	/// Diagnostic rule that requires model types with mutable string properties to implement <c>IUmbrellaTrimmable</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor MutableStringModelMustImplementTrimmableRule = new(
		id: "UA015",
		title: "Models with mutable string properties must implement IUmbrellaTrimmable",
		messageFormat: "Model type '{0}' has mutable string properties and must implement IUmbrellaTrimmable",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Model classes and records with public mutable string properties should implement IUmbrellaTrimmable so user-supplied strings can be trimmed.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[ModelMustBeRecordRule, PropertiesMustBeRequiredRule, PropertiesMustBeGetterInitOnlyRule, CollectionsMustBeReadOnlyRule, MutableStringModelMustImplementTrimmableRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? razorPageModelSymbol = startContext.Compilation.GetTypeByMetadataName(
				"Microsoft.AspNetCore.Mvc.RazorPages.PageModel");
			INamedTypeSymbol? trimmableSymbol = startContext.Compilation.GetTypeByMetadataName("Umbrella.Utilities.Text.IUmbrellaTrimmable");
			var collectionAnalysis = new CollectionTypeAnalysis(startContext.Compilation);

			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzeTypeDeclaration(ctx, razorPageModelSymbol),
				SyntaxKind.ClassDeclaration,
				SyntaxKind.RecordDeclaration);
			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzePropertyDeclaration(ctx, razorPageModelSymbol, collectionAnalysis),
				SyntaxKind.PropertyDeclaration);

			if (trimmableSymbol is null)
				return;

			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzeModelForTrimmingRequirements(ctx, trimmableSymbol, razorPageModelSymbol),
				SyntaxKind.ClassDeclaration,
				SyntaxKind.RecordDeclaration);
		});
	}

	private static void AnalyzeTypeDeclaration(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol? razorPageModelSymbol)
	{
		if (context.Node is not TypeDeclarationSyntax typeDecl)
			return;

		if (!IsApplicableModelType(typeDecl, context.SemanticModel, razorPageModelSymbol))
			return;

		if (HasOptOutAttribute(typeDecl, context.SemanticModel, "UmbrellaExcludeFromModelStandardsAttribute"))
			return;

		if (typeDecl is not RecordDeclarationSyntax)
		{
			var diagnostic = Diagnostic.Create(ModelMustBeRecordRule, typeDecl.Identifier.GetLocation(), typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}
	}

	private static void AnalyzePropertyDeclaration(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol? razorPageModelSymbol,
		CollectionTypeAnalysis collectionAnalysis)
	{
		var propertyDecl = (PropertyDeclarationSyntax)context.Node;

		var typeDecl = propertyDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>();
		if (typeDecl is null ||
			!IsApplicableModelType(typeDecl, context.SemanticModel, razorPageModelSymbol))
		{
			return;
		}

		if (HasOptOutAttribute(typeDecl, context.SemanticModel, "UmbrellaExcludeFromModelStandardsAttribute"))
			return;

		if (!HasRequiredModifier(propertyDecl) &&
			!HasOptOutAttribute(propertyDecl, context.SemanticModel, "UmbrellaAllowOptionalPropertyAttribute"))
		{
			var diagnostic = Diagnostic.Create(PropertiesMustBeRequiredRule, propertyDecl.Identifier.GetLocation(),
				propertyDecl.Identifier.Text, typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}

		// Check for getter (always required — [UmbrellaAllowMutableProperty] does not suppress this)
		bool hasMissingGetter = propertyDecl.AccessorList == null ||
			!propertyDecl.AccessorList.Accessors.Any(a => a.Kind() == SyntaxKind.GetAccessorDeclaration);

		// Check for setter instead of init (suppressed by [UmbrellaAllowMutableProperty])
		bool hasSetterWithoutInit = propertyDecl.AccessorList != null &&
			propertyDecl.AccessorList.Accessors.Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration && !HasInitModifier(a)) &&
			!HasOptOutAttribute(propertyDecl, context.SemanticModel, "UmbrellaAllowMutablePropertyAttribute");

		if (hasMissingGetter || hasSetterWithoutInit)
		{
			var diagnostic = Diagnostic.Create(PropertiesMustBeGetterInitOnlyRule, propertyDecl.Identifier.GetLocation(),
				propertyDecl.Identifier.Text, typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}

		var propertyType = context.SemanticModel.GetTypeInfo(propertyDecl.Type).Type;
		if (propertyType != null &&
			collectionAnalysis.IsCollectionType(propertyType) &&
			!collectionAnalysis.IsReadOnlyCollectionType(propertyType) &&
			!HasOptOutAttribute(propertyDecl, context.SemanticModel, "UmbrellaAllowMutableCollectionAttribute"))
		{
			var diagnostic = Diagnostic.Create(CollectionsMustBeReadOnlyRule, propertyDecl.Identifier.GetLocation(),
				propertyDecl.Identifier.Text, typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}
	}

	private static void AnalyzeModelForTrimmingRequirements(
		SyntaxNodeAnalysisContext context,
		INamedTypeSymbol trimmableSymbol,
		INamedTypeSymbol? razorPageModelSymbol)
	{
		var typeDecl = (TypeDeclarationSyntax)context.Node;

		if (!IsApplicableModelType(typeDecl, context.SemanticModel, razorPageModelSymbol))
			return;

		if (HasOptOutAttribute(typeDecl, context.SemanticModel, "UmbrellaExcludeFromModelStandardsAttribute"))
			return;

		if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol ||
			!HasMutableStringProperty(typeSymbol) ||
			ImplementsInterface(typeSymbol, trimmableSymbol))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			MutableStringModelMustImplementTrimmableRule,
			typeDecl.Identifier.GetLocation(),
			typeSymbol.Name));
	}

	private static bool HasMutableStringProperty(INamedTypeSymbol typeSymbol)
	{
		for (INamedTypeSymbol? currentType = typeSymbol;
			currentType is not null;
			currentType = currentType.BaseType)
		{
			if (currentType.GetMembers()
				.OfType<IPropertySymbol>()
				.Any(static property =>
					!property.IsStatic &&
					property.DeclaredAccessibility == Accessibility.Public &&
					property.Type.SpecialType == SpecialType.System_String &&
					property.SetMethod is { IsInitOnly: false }))
			{
				return true;
			}
		}

		return false;
	}

	private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol) =>
		typeSymbol.AllInterfaces.Any(
			interfaceType => SymbolEqualityComparer.Default.Equals(interfaceType, interfaceSymbol));

	private static bool IsModelType(string typeName)
	{
		return typeName.EndsWith("Model", StringComparison.Ordinal) ||
			   typeName.EndsWith("ModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModel", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("QueryResult", StringComparison.Ordinal);
	}

	private static bool IsApplicableModelType(
		TypeDeclarationSyntax typeDeclaration,
		SemanticModel semanticModel,
		INamedTypeSymbol? razorPageModelSymbol)
	{
		if (!IsModelType(typeDeclaration.Identifier.Text))
			return false;

		if (razorPageModelSymbol is null ||
			semanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
		{
			return true;
		}

		for (INamedTypeSymbol? currentType = typeSymbol;
			currentType is not null;
			currentType = currentType.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(currentType, razorPageModelSymbol))
				return false;
		}

		return true;
	}

	private static bool HasRequiredModifier(PropertyDeclarationSyntax property) =>
		property.Modifiers.Any(m => m.Text == "required");

	private static bool HasInitModifier(AccessorDeclarationSyntax accessor) =>
		accessor.Modifiers.Any(m => m.Text == "init");

	private static bool HasOptOutAttribute(SyntaxNode node, SemanticModel semanticModel, params string[] attributeNames)
	{
		if (node is MemberDeclarationSyntax memberDecl)
		{
			if (memberDecl.AttributeLists.Count == 0)
				return false;

			var symbol = semanticModel.GetDeclaredSymbol(memberDecl);
			if (symbol == null)
				return false;

			foreach (var attribute in symbol.GetAttributes())
			{
				var attributeClass = attribute.AttributeClass;
				if (attributeClass != null && attributeNames.Contains(attributeClass.Name))
					return true;
			}
		}

		return false;
	}
}
