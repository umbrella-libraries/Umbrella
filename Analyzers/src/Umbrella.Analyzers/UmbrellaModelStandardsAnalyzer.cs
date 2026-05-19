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
/// <item><description>UMS004: Collection properties must use <see cref="IReadOnlyCollection{T}" /> for immutability</description></item>
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
	/// Diagnostic rule that requires collection properties to use <see cref="IReadOnlyCollection{T}" />
	/// </summary>
	/// <remarks>
	/// Using IReadOnlyCollection&lt;T&gt; prevents external code from modifying the collection
	/// contents, maintaining immutability and preventing unintended side effects.
	/// </remarks>
	public static readonly DiagnosticDescriptor CollectionsMustBeReadOnlyRule = new(
		id: "UA014",
		title: "Collection properties must use IReadOnlyCollection<T>",
		messageFormat: "Collection property '{0}' in model type '{1}' should be of type IReadOnlyCollection<T>",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Per Umbrella standards, collection properties in model types should use IReadOnlyCollection<T> for better immutability.");

	/// <summary>
	/// Diagnostic rule that requires model records to be <c>partial</c> when <c>IUmbrellaTrimmable</c> is present in the compilation.
	/// </summary>
	public static readonly DiagnosticDescriptor ModelRecordMustBePartialRule = new(
		id: "UA021",
		title: "Model records must be partial when IUmbrellaTrimmable is used",
		messageFormat: "Model record '{0}' must be declared as 'partial' when the project uses IUmbrellaTrimmable source generation",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "When a project uses IUmbrellaTrimmable for source-generated string trimming, model records must be partial so the source generator can emit the trimming implementation.");

	/// <summary>
	/// Diagnostic rule that requires Create*/Update* model records with string properties to implement <c>IUmbrellaTrimmable</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor InputModelMustImplementTrimmableRule = new(
		id: "UA022",
		title: "Input model records with string properties must implement IUmbrellaTrimmable",
		messageFormat: "Input model '{0}' has string properties but does not implement IUmbrellaTrimmable; user-supplied strings will not be trimmed",
		category: "UmbrellaModelStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Create/Update model records that contain string properties should implement IUmbrellaTrimmable so the source generator emits string-trimming code for user input before it reaches validation or persistence.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
		[ModelMustBeRecordRule, PropertiesMustBeRequiredRule, PropertiesMustBeGetterInitOnlyRule, CollectionsMustBeReadOnlyRule, ModelRecordMustBePartialRule, InputModelMustImplementTrimmableRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
		context.RegisterSyntaxNodeAction(AnalyzePropertyDeclaration, SyntaxKind.PropertyDeclaration);

		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? trimmableSymbol = startContext.Compilation.GetTypeByMetadataName("Umbrella.Utilities.Text.IUmbrellaTrimmable");
			if (trimmableSymbol is null)
				return;

			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzeModelRecordForBlazorRequirements(ctx, trimmableSymbol),
				SyntaxKind.RecordDeclaration);
		});
	}

	private void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not TypeDeclarationSyntax typeDecl)
			return;

		if (!IsModelType(typeDecl.Identifier.Text))
			return;

		if (HasOptOutAttribute(typeDecl, context.SemanticModel, "UmbrellaExcludeFromModelStandardsAttribute"))
			return;

		if (typeDecl is not RecordDeclarationSyntax)
		{
			var diagnostic = Diagnostic.Create(ModelMustBeRecordRule, typeDecl.Identifier.GetLocation(), typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}
	}

	private void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
	{
		var propertyDecl = (PropertyDeclarationSyntax)context.Node;

		var typeDecl = propertyDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>();
		if (typeDecl == null || !IsModelType(typeDecl.Identifier.Text))
			return;

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
		if (propertyType != null && IsCollectionType(propertyType) && !IsReadOnlyCollectionType(propertyType) &&
			!HasOptOutAttribute(propertyDecl, context.SemanticModel, "UmbrellaAllowMutableCollectionAttribute"))
		{
			var diagnostic = Diagnostic.Create(CollectionsMustBeReadOnlyRule, propertyDecl.Identifier.GetLocation(),
				propertyDecl.Identifier.Text, typeDecl.Identifier.Text);
			context.ReportDiagnostic(diagnostic);
		}
	}

	private static void AnalyzeModelRecordForBlazorRequirements(SyntaxNodeAnalysisContext context, INamedTypeSymbol trimmableSymbol)
	{
		var recordDecl = (RecordDeclarationSyntax)context.Node;

		if (!IsModelType(recordDecl.Identifier.Text))
			return;

		if (HasOptOutAttribute(recordDecl, context.SemanticModel, "UmbrellaExcludeFromModelStandardsAttribute"))
			return;

		if (context.SemanticModel.GetDeclaredSymbol(recordDecl) is not INamedTypeSymbol typeSymbol)
			return;

		bool isPartial = recordDecl.Modifiers.Any(SyntaxKind.PartialKeyword);

		if (!isPartial)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				ModelRecordMustBePartialRule,
				recordDecl.Identifier.GetLocation(),
				typeSymbol.Name));
		}

		if (!IsInputModelType(recordDecl.Identifier.Text))
			return;

		bool implementsTrimmable = false;
		foreach (INamedTypeSymbol iface in typeSymbol.AllInterfaces)
		{
			if (SymbolEqualityComparer.Default.Equals(iface, trimmableSymbol))
			{
				implementsTrimmable = true;
				break;
			}
		}

		if (implementsTrimmable)
			return;

		bool hasStringProperty = false;
		foreach (ISymbol member in typeSymbol.GetMembers())
		{
			if (member is IPropertySymbol prop && prop.Type.SpecialType == SpecialType.System_String)
			{
				hasStringProperty = true;
				break;
			}
		}

		if (hasStringProperty)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				InputModelMustImplementTrimmableRule,
				recordDecl.Identifier.GetLocation(),
				typeSymbol.Name));
		}
	}

	private static bool IsInputModelType(string typeName) =>
		(typeName.StartsWith("Create", StringComparison.Ordinal) || typeName.StartsWith("Update", StringComparison.Ordinal)) &&
		IsModelType(typeName);

	private static bool IsModelType(string typeName)
	{
		return typeName.EndsWith("Model", StringComparison.Ordinal) ||
			   typeName.EndsWith("ModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModel", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("QueryResult", StringComparison.Ordinal);
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

	private static bool IsCollectionType(ITypeSymbol type)
	{
		if (type.SpecialType == SpecialType.System_String)
			return false;

		foreach (var interfaceType in type.AllInterfaces)
		{
			if (interfaceType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
				return true;
		}

		return false;
	}

	private static bool IsReadOnlyCollectionType(ITypeSymbol type) =>
		type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyCollection<T>";
}
