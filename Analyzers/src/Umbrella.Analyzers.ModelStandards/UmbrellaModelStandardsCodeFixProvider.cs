//#if NET8_0_OR_GREATER
//using System.Collections.Immutable;
//using System.Composition;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CodeActions;
//using Microsoft.CodeAnalysis.CodeFixes;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;

//namespace Umbrella.Analyzers.ModelStandards;

///// <summary>
///// Code fix provider for Umbrella model standards analyzer.
///// Provides automatic fixes for model standards violations.
///// </summary>
//[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UmbrellaModelStandardsCodeFixProvider)), Shared]
//public class UmbrellaModelStandardsCodeFixProvider : CodeFixProvider
//{
//	/// <inheritdoc/>
//	public sealed override ImmutableArray<string> FixableDiagnosticIds =>
//		ImmutableArray.Create(
//			UmbrellaModelStandardsAnalyzer.ModelMustBeRecordRule.Id,
//			UmbrellaModelStandardsAnalyzer.PropertiesMustBeRequiredRule.Id,
//			UmbrellaModelStandardsAnalyzer.PropertiesMustBeGetterInitOnlyRule.Id,
//			UmbrellaModelStandardsAnalyzer.CollectionsMustBeReadOnlyRule.Id);

//	/// <inheritdoc/>
//	public sealed override FixAllProvider GetFixAllProvider()
//	{
//		return WellKnownFixAllProviders.BatchFixer;
//	}

//	/// <inheritdoc/>
//	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
//	{
//		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
//		if (root == null)
//			return;

//		foreach (var diagnostic in context.Diagnostics)
//		{
//			var diagnosticSpan = diagnostic.Location.SourceSpan;
//			var node = root.FindNode(diagnosticSpan);

//			switch (diagnostic.Id)
//			{
//				case "UMS001": // Model must be record
//					if (node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is ClassDeclarationSyntax classDecl)
//					{
//						context.RegisterCodeFix(
//							CodeAction.Create(
//								title: "Convert to record",
//								createChangedDocument: c => ConvertToRecordAsync(context.Document, classDecl, c),
//								equivalenceKey: "ConvertToRecord"),
//							diagnostic);
//					}

//					break;

//				case "UMS002": // Properties must be required
//					if (node.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is PropertyDeclarationSyntax propertyDecl1)
//					{
//						context.RegisterCodeFix(
//							CodeAction.Create(
//								title: "Add 'required' modifier",
//								createChangedDocument: c => AddRequiredModifierAsync(context.Document, propertyDecl1, c),
//								equivalenceKey: "AddRequiredModifier"),
//							diagnostic);
//					}

//					break;

//				case "UMS003": // Properties must have getter and be init-only
//					if (node.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is PropertyDeclarationSyntax propertyDecl2)
//					{
//						context.RegisterCodeFix(
//							CodeAction.Create(
//								title: "Fix property accessors (get; init;)",
//								createChangedDocument: c => FixPropertyAccessorsAsync(context.Document, propertyDecl2, c),
//								equivalenceKey: "FixPropertyAccessors"),
//							diagnostic);
//					}

//					break;

//				case "UMS004": // Collections must be IReadOnlyCollection<T>
//					if (node.FirstAncestorOrSelf<PropertyDeclarationSyntax>() is PropertyDeclarationSyntax propertyDecl3)
//					{
//						context.RegisterCodeFix(
//							CodeAction.Create(
//								title: "Change to IReadOnlyCollection<T>",
//								createChangedDocument: c => ChangeToReadOnlyCollectionAsync(context.Document, propertyDecl3, c),
//								equivalenceKey: "ChangeToReadOnlyCollection"),
//							diagnostic);
//					}

//					break;
//			}
//		}
//	}

//	private static async Task<Document> ConvertToRecordAsync(Document document, ClassDeclarationSyntax classDecl, CancellationToken cancellationToken)
//	{
//		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
//		if (root == null)
//			return document;

//		// Create a new record declaration with the same members and modifiers
//		var recordDecl = SyntaxFactory.RecordDeclaration(
//				attributeLists: classDecl.AttributeLists,
//				modifiers: classDecl.Modifiers,
//				keyword: SyntaxFactory.Token(SyntaxKind.RecordKeyword),
//				identifier: classDecl.Identifier,
//				typeParameterList: classDecl.TypeParameterList,
//				parameterList: null,
//				baseList: classDecl.BaseList,
//				constraintClauses: classDecl.ConstraintClauses,
//				openBraceToken: classDecl.OpenBraceToken,
//				members: classDecl.Members,
//				closeBraceToken: classDecl.CloseBraceToken,
//				semicolonToken: SyntaxFactory.Token(SyntaxKind.None))
//			.WithLeadingTrivia(classDecl.GetLeadingTrivia())
//			.WithTrailingTrivia(classDecl.GetTrailingTrivia());

//		// Replace the class with the record
//		var newRoot = root.ReplaceNode(classDecl, recordDecl);
//		return document.WithSyntaxRoot(newRoot);
//	}

//	private static async Task<Document> AddRequiredModifierAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
//	{
//		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
//		if (root == null)
//			return document;

//		// Create required token using text-based approach for compatibility
//		var requiredToken = SyntaxFactory.Token(
//			SyntaxFactory.TriviaList(),
//			SyntaxKind.IdentifierToken,
//			"required",
//			"required",
//			SyntaxFactory.TriviaList(SyntaxFactory.Space));

//		var newModifiers = property.Modifiers.Insert(0, requiredToken);
//		var newProperty = property.WithModifiers(newModifiers);

//		var newRoot = root.ReplaceNode(property, newProperty);
//		return document.WithSyntaxRoot(newRoot);
//	}

//	private static async Task<Document> FixPropertyAccessorsAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
//	{
//		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
//		if (root == null)
//			return document;

//		// Create new accessors - get and init
//		var getAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
//			.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

//		// Create init accessor using text-based approach for compatibility
//		var initToken = SyntaxFactory.Token(
//			SyntaxFactory.TriviaList(),
//			SyntaxKind.IdentifierToken,
//			"init",
//			"init",
//			SyntaxFactory.TriviaList());

//		var initAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
//			.WithModifiers(SyntaxFactory.TokenList(initToken))
//			.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

//		var accessorList = SyntaxFactory.AccessorList(
//			SyntaxFactory.List(new[] { getAccessor, initAccessor }));

//		// Create the new property with proper accessors
//		PropertyDeclarationSyntax newProperty;
//		if (property.AccessorList != null)
//		{
//			newProperty = property.WithAccessorList(accessorList);
//		}
//		else
//		{
//			// Handle expression-bodied property
//			newProperty = property
//				.WithExpressionBody(null)
//				.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
//				.WithAccessorList(accessorList);
//		}

//		var newRoot = root.ReplaceNode(property, newProperty);
//		return document.WithSyntaxRoot(newRoot);
//	}

//	private static async Task<Document> ChangeToReadOnlyCollectionAsync(Document document, PropertyDeclarationSyntax property, CancellationToken cancellationToken)
//	{
//		var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
//		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

//		if (semanticModel == null || root == null)
//			return document;

//		var typeInfo = semanticModel.GetTypeInfo(property.Type, cancellationToken);
//		if (typeInfo.Type == null) 
//			return document;

//		// Get the element type from the collection
//		ITypeSymbol? elementType = null;

//		// Try to find the element type from implemented interfaces
//		foreach (var interfaceType in typeInfo.Type.AllInterfaces)
//		{
//			if (interfaceType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
//			{
//				elementType = ((INamedTypeSymbol)interfaceType).TypeArguments[0];
//				break;
//			}
//		}

//		// If we couldn't find it, check if it's a direct generic type
//		if (elementType == null && typeInfo.Type is INamedTypeSymbol namedType && namedType.IsGenericType)
//		{
//			elementType = namedType.TypeArguments[0];
//		}

//		if (elementType == null) 
//			return document;

//		// Create the new type syntax: IReadOnlyCollection<elementType>
//		var elementTypeSyntax = SyntaxFactory.ParseTypeName(elementType.ToDisplayString());
//		var readOnlyCollectionType = SyntaxFactory.GenericName(
//				SyntaxFactory.Identifier("IReadOnlyCollection"))
//			.WithTypeArgumentList(
//				SyntaxFactory.TypeArgumentList(
//					SyntaxFactory.SingletonSeparatedList(elementTypeSyntax)));

//		var newProperty = property.WithType(readOnlyCollectionType);
//		var newRoot = root.ReplaceNode(property, newProperty);

//		return document.WithSyntaxRoot(newRoot);
//	}
//}

//#else

//namespace Umbrella.Analyzers.ModelStandards;

///// <summary>
///// Code fix provider for Umbrella model standards analyzer.
///// </summary>
///// <remarks>
///// <para>
///// Code fixes are only available when targeting .NET 8.0 or higher.
///// This analyzer package targets .NET Standard 2.0 for maximum compatibility,
///// but code fixes require the Microsoft.CodeAnalysis.Workspaces packages
///// which are only available for .NET 8.0+.
///// </para>
///// <para>
///// The analyzer functionality (diagnostics/warnings) works on all target frameworks.
///// Only the automatic code fixing functionality requires .NET 8.0+.
///// </para>
///// </remarks>
//internal sealed class UmbrellaModelStandardsCodeFixProvider
//{
//	// Code fixes require Microsoft.CodeAnalysis.Workspaces which is only available for .NET 8.0+
//}

//#endif