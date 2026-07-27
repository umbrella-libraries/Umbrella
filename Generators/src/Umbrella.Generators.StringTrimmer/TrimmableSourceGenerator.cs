using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Umbrella.Generators.StringTrimmer;

/// <summary>
/// A source generator that automatically implements the IUmbrellaTrimmable interface
/// </summary>
[Generator]
public class TrimmableSourceGenerator : IIncrementalGenerator
{
	private const string InterfaceName = "Umbrella.Utilities.Text.IUmbrellaTrimmable";
	private const string DoNotTrimAttributeName = "Umbrella.Utilities.Text.UmbrellaDoNotTrimAttribute";
	private const string AllowMutablePropertyAttributeName = "Umbrella.Analyzers.UmbrellaAllowMutablePropertyAttribute";
	private const string ConcurrencyStampInterfaceName = "Umbrella.Utilities.Data.Concurrency.IConcurrencyStamp";
	private const string AllowedMicrosoftNamespace = "Microsoft.AspNetCore.Identity";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Handle classes
		var classDeclarations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (syntaxNode, _) => IsClassCandidate(syntaxNode),
				transform: static (ctx, _) => new { Node = (TypeDeclarationSyntax)ctx.Node, IsRecord = false })
			.Where(static result => result.Node is not null);

		// Handle records
		var recordDeclarations = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (syntaxNode, _) => IsRecordCandidate(syntaxNode),
				transform: static (ctx, _) => new { Node = (TypeDeclarationSyntax)ctx.Node, IsRecord = true })
			.Where(static result => result.Node is not null);

		// Combine both declaration types
		var typeDeclarations = classDeclarations.Collect().Combine(recordDeclarations.Collect())
			.Select((pair, _) => pair.Left.Concat(pair.Right).ToArray());

		var compilationAndTypes = context.CompilationProvider.Combine(typeDeclarations);

		context.RegisterSourceOutput(compilationAndTypes, static (spc, source) =>
		{
			var (compilation, typeList) = source;
			var trimmableSymbol = compilation.GetTypeByMetadataName(InterfaceName);

			if (trimmableSymbol is null)
				return;

			var trimmingSymbols = new TrimmingSymbolContext(
				trimmableSymbol,
				compilation.GetTypeByMetadataName(DoNotTrimAttributeName),
				compilation.GetTypeByMetadataName(AllowMutablePropertyAttributeName),
				compilation.GetTypeByMetadataName(ConcurrencyStampInterfaceName));

			foreach (var typeInfo in typeList.Distinct())
			{
				var model = compilation.GetSemanticModel(typeInfo.Node.SyntaxTree);

				if (model.GetDeclaredSymbol(typeInfo.Node) is not INamedTypeSymbol typeSymbol)
					continue;

				if (ImplementsInterface(typeSymbol, trimmableSymbol) &&
					!HasConcreteImplementation(typeSymbol, trimmableSymbol))
				{
					string generated = GenerateTrimmableImplementation(typeSymbol, trimmingSymbols, typeInfo.IsRecord);
					string namespaceSafeName = GetSafeNamespaceName(typeSymbol.ContainingNamespace.ToDisplayString());
					spc.AddSource($"{namespaceSafeName}.{typeSymbol.Name}_UmbrellaTrimmable.g.cs", SourceText.From(generated, Encoding.UTF8));
				}
			}
		});
	}

	private static string GetSafeNamespaceName(string namespaceName)
	{
		// Replace illegal filename characters with underscore
		return namespaceName.Replace('.', '_').Replace('<', '_').Replace('>', '_')
			.Replace('(', '_').Replace(')', '_').Replace(',', '_')
			.Replace(' ', '_').Replace('-', '_');
	}

	private static bool IsClassCandidate(SyntaxNode node)
		=> node is ClassDeclarationSyntax cds &&
			cds.BaseList?.Types.Any() == true &&
			cds.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

	private static bool IsRecordCandidate(SyntaxNode node)
		=> node is RecordDeclarationSyntax rds &&
			rds.BaseList?.Types.Any() == true &&
			rds.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

	private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
		=> typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));

	private static bool HasConcreteImplementation(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
	{
		foreach (IMethodSymbol interfaceMethod in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
		{
			if (typeSymbol.FindImplementationForInterfaceMember(interfaceMethod) is IMethodSymbol implementation &&
				SymbolEqualityComparer.Default.Equals(implementation.ContainingType, typeSymbol) &&
				!implementation.IsAbstract)
			{
				return true;
			}
		}

		return false;
	}

	private static string GenerateTrimmableImplementation(
		INamedTypeSymbol typeSymbol,
		TrimmingSymbolContext trimmingSymbols,
		bool isRecord)
	{
		string namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();
		string typeName = typeSymbol.Name;
		string typeKeyword = isRecord ? "record" : "class";
		string trimMethodModifiers = HasTrimmableBaseType(typeSymbol, trimmingSymbols.TrimmableInterface)
			? "public new"
			: "public";

		var trimStatements = GenerateTrimStatements(
			typeSymbol,
			"this",
			new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
			trimmingSymbols,
			0,
			3);

		string statementsText = trimStatements.Any()
			? string.Join("\n", trimStatements)
			: $"{Indent(3)}// No string properties found to trim";

		return $$"""
		// <auto-generated />
		#nullable enable
		using System;
		
		namespace {{namespaceName}}
		{
		{{Indent(1)}}partial {{typeKeyword}} {{typeName}}
		{{Indent(1)}}{
		{{Indent(2)}}private bool _isTrimmingInProgress;

		{{Indent(2)}}/// <summary>
		{{Indent(2)}}/// Trims all string properties in this object and its nested properties.
		{{Indent(2)}}/// </summary>
		{{Indent(2)}}{{trimMethodModifiers}} void TrimAllStringProperties()
		{{Indent(2)}}{
		{{Indent(3)}}if (_isTrimmingInProgress)
		{{Indent(3)}}	return;
		
		{{Indent(3)}}_isTrimmingInProgress = true;

		{{Indent(3)}}try
		{{Indent(3)}}{
		{{statementsText}}
		{{Indent(3)}}}
		{{Indent(3)}}finally
		{{Indent(3)}}{
		{{Indent(4)}}_isTrimmingInProgress = false;
		{{Indent(3)}}}
		{{Indent(2)}}}
		{{Indent(1)}}}
		}
		""";
	}

	private static List<string> GenerateTrimStatements(
		INamedTypeSymbol typeSymbol,
		string instanceName,
		HashSet<INamedTypeSymbol> visitedTypes,
		TrimmingSymbolContext trimmingSymbols,
		int depth,
		int indentLevel)
	{
		var statements = new List<string>();

		if (visitedTypes.Contains(typeSymbol))
			return statements;

		_ = visitedTypes.Add(typeSymbol);

		// Process base type first unless it's System.Object, and allow Identity types
		if (typeSymbol.BaseType != null &&
			!SymbolEqualityComparer.Default.Equals(typeSymbol.BaseType, typeSymbol.ContainingAssembly.GetTypeByMetadataName("System.Object")) &&
			!IsSystemType(typeSymbol.BaseType, allowNamespace: AllowedMicrosoftNamespace))
		{
			var baseTypeStatements = GenerateTrimStatements(
				typeSymbol.BaseType,
				instanceName,
				visitedTypes,
				trimmingSymbols,
				depth,
				indentLevel);

			statements.AddRange(baseTypeStatements);
		}
		else if (typeSymbol.BaseType != null &&
			typeSymbol.BaseType.ContainingNamespace?.ToDisplayString().StartsWith(AllowedMicrosoftNamespace, StringComparison.Ordinal) == true)
		{
			var baseTypeStatements = GenerateTrimStatements(
				typeSymbol.BaseType,
				instanceName,
				visitedTypes,
				trimmingSymbols,
				depth,
				indentLevel);

			statements.AddRange(baseTypeStatements);
		}

		foreach (var member in typeSymbol.GetMembers())
		{
			if (member is IPropertySymbol property &&
				property.SetMethod is not null &&
				!property.SetMethod.IsInitOnly &&
				property.GetMethod is not null &&
				property.DeclaredAccessibility is Accessibility.Public &&
				!ShouldSkipProperty(property, typeSymbol, trimmingSymbols))
			{
				string propertyName = property.Name;
				var propertyType = property.Type;

				if (propertyType.SpecialType == SpecialType.System_String)
				{
					statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
					statements.Add($"{Indent(indentLevel)}{{");
					statements.Add($"{Indent(indentLevel + 1)}{instanceName}.{propertyName} = {instanceName}.{propertyName}.Trim();");
					statements.Add($"{Indent(indentLevel)}}}");
				}
				else if (propertyType is INamedTypeSymbol namedPropertyType &&
						 !IsSystemType(namedPropertyType) &&
						 !namedPropertyType.IsValueType)
				{
					if (ImplementsInterface(namedPropertyType, trimmingSymbols.TrimmableInterface))
					{
						statements.Add($"{Indent(indentLevel)}{instanceName}.{propertyName}?.TrimAllStringProperties();");
					}
					else
					{
						var nestedStatements = GenerateTrimStatements(
							namedPropertyType,
							$"{instanceName}.{propertyName}",
							visitedTypes,
							trimmingSymbols,
							depth + 1,
							indentLevel + 1);

						if (nestedStatements.Any())
						{
							statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
							statements.Add($"{Indent(indentLevel)}{{");
							statements.AddRange(nestedStatements);
							statements.Add($"{Indent(indentLevel)}}}");
						}
					}
				}
				else if (IsDictionaryType(propertyType))
				{
					var (keyType, valueType) = GetDictionaryKeyValueTypes(propertyType);
					if (keyType is not null && valueType is not null)
					{
						// Handle dictionary with string keys
						if (keyType.SpecialType == SpecialType.System_String)
						{
							statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
							statements.Add($"{Indent(indentLevel)}{{");
							statements.Add($"{Indent(indentLevel + 1)}var trimmedKeys{depth} = new System.Collections.Generic.Dictionary<string, dynamic>();");
							statements.Add($"{Indent(indentLevel + 1)}foreach (var kvp{depth} in {instanceName}.{propertyName})");
							statements.Add($"{Indent(indentLevel + 1)}{{");
							statements.Add($"{Indent(indentLevel + 2)}string trimmedKey{depth} = kvp{depth}.Key?.Trim() ?? kvp{depth}.Key;");
							statements.Add($"{Indent(indentLevel + 2)}trimmedKeys{depth}[trimmedKey{depth}] = kvp{depth}.Value;");
							statements.Add($"{Indent(indentLevel + 1)}}}");
							statements.Add("");
							statements.Add($"{Indent(indentLevel + 1)}// Clear and repopulate with trimmed keys");
							statements.Add($"{Indent(indentLevel + 1)}{instanceName}.{propertyName}.Clear();");
							statements.Add($"{Indent(indentLevel + 1)}foreach (var kvp{depth} in trimmedKeys{depth})");
							statements.Add($"{Indent(indentLevel + 1)}{{");
							statements.Add($"{Indent(indentLevel + 2)}{instanceName}.{propertyName}[kvp{depth}.Key] = kvp{depth}.Value;");
							statements.Add($"{Indent(indentLevel + 1)}}}");
							statements.Add($"{Indent(indentLevel)}}}");
						}

						// Handle dictionary with string values
						if (valueType.SpecialType == SpecialType.System_String)
						{
							statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
							statements.Add($"{Indent(indentLevel)}{{");
							statements.Add($"{Indent(indentLevel + 1)}var keys{depth} = {instanceName}.{propertyName}.Keys.ToArray();");
							statements.Add($"{Indent(indentLevel + 1)}foreach (var key{depth} in keys{depth})");
							statements.Add($"{Indent(indentLevel + 1)}{{");
							statements.Add($"{Indent(indentLevel + 2)}if ({instanceName}.{propertyName}[key{depth}] is not null)");
							statements.Add($"{Indent(indentLevel + 2)}{{");
							statements.Add($"{Indent(indentLevel + 3)}{instanceName}.{propertyName}[key{depth}] = {instanceName}.{propertyName}[key{depth}].Trim();");
							statements.Add($"{Indent(indentLevel + 2)}}}");
							statements.Add($"{Indent(indentLevel + 1)}}}");
							statements.Add($"{Indent(indentLevel)}}}");
						}
						// Handle dictionary with object values implementing IUmbrellaTrimmable
						else if (valueType is INamedTypeSymbol namedValueType &&
								!IsSystemType(namedValueType) &&
								!namedValueType.IsValueType)
						{
							if (ImplementsInterface(namedValueType, trimmingSymbols.TrimmableInterface))
							{
								statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
								statements.Add($"{Indent(indentLevel)}{{");
								statements.Add($"{Indent(indentLevel + 1)}foreach (var value{depth} in {instanceName}.{propertyName}.Values)");
								statements.Add($"{Indent(indentLevel + 1)}{{");
								statements.Add($"{Indent(indentLevel + 2)}value{depth}?.TrimAllStringProperties();");
								statements.Add($"{Indent(indentLevel + 1)}}}");
								statements.Add($"{Indent(indentLevel)}}}");
							}
							else
							{
								// Handle dictionary values that may contain string properties
								var valueStatements = GenerateTrimStatements(
									namedValueType,
									$"value{depth}",
									visitedTypes,
									trimmingSymbols,
									depth + 1,
									indentLevel + 2);

								if (valueStatements.Any())
								{
									statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
									statements.Add($"{Indent(indentLevel)}{{");
									statements.Add($"{Indent(indentLevel + 1)}foreach (var value{depth} in {instanceName}.{propertyName}.Values)");
									statements.Add($"{Indent(indentLevel + 1)}{{");
									statements.Add($"{Indent(indentLevel + 2)}if (value{depth} is not null)");
									statements.Add($"{Indent(indentLevel + 2)}{{");
									statements.AddRange(valueStatements);
									statements.Add($"{Indent(indentLevel + 2)}}}");
									statements.Add($"{Indent(indentLevel + 1)}}}");
									statements.Add($"{Indent(indentLevel)}}}");
								}
							}
						}
					}
				}
				else if (IsCollectionType(propertyType))
				{
					var elementType = GetCollectionElementType(propertyType);
					if (elementType is not null)
					{
						if (elementType.SpecialType == SpecialType.System_String)
						{
							statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
							statements.Add($"{Indent(indentLevel)}{{");
							statements.Add($"{Indent(indentLevel + 1)}for (int i{depth} = 0; i{depth} < {instanceName}.{propertyName}.Count; i{depth}++)");
							statements.Add($"{Indent(indentLevel + 1)}{{");
							statements.Add($"{Indent(indentLevel + 2)}if ({instanceName}.{propertyName}[i{depth}] is not null)");
							statements.Add($"{Indent(indentLevel + 2)}{{");
							statements.Add($"{Indent(indentLevel + 3)}{instanceName}.{propertyName}[i{depth}] = {instanceName}.{propertyName}[i{depth}].Trim();");
							statements.Add($"{Indent(indentLevel + 2)}}}");
							statements.Add($"{Indent(indentLevel + 1)}}}");
							statements.Add($"{Indent(indentLevel)}}}");
						}
						else if (elementType is INamedTypeSymbol namedElementType &&
								 !IsSystemType(namedElementType) &&
								 !namedElementType.IsValueType)
						{
							if (ImplementsInterface(namedElementType, trimmingSymbols.TrimmableInterface))
							{
								statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
								statements.Add($"{Indent(indentLevel)}{{");
								statements.Add($"{Indent(indentLevel + 1)}foreach (var item{depth} in {instanceName}.{propertyName})");
								statements.Add($"{Indent(indentLevel + 1)}{{");
								statements.Add($"{Indent(indentLevel + 2)}item{depth}?.TrimAllStringProperties();");
								statements.Add($"{Indent(indentLevel + 1)}}}");
								statements.Add($"{Indent(indentLevel)}}}");
							}
							else
							{
								// Handle collections of non-IUmbrellaTrimmable types that may have string properties
								var collectionStatements = GenerateTrimStatements(
									namedElementType,
									$"item{depth}",
									visitedTypes,
									trimmingSymbols,
									depth + 1,
									indentLevel + 2);

								if (collectionStatements.Any())
								{
									statements.Add($"{Indent(indentLevel)}if ({instanceName}.{propertyName} is not null)");
									statements.Add($"{Indent(indentLevel)}{{");
									statements.Add($"{Indent(indentLevel + 1)}foreach (var item{depth} in {instanceName}.{propertyName})");
									statements.Add($"{Indent(indentLevel + 1)}{{");
									statements.Add($"{Indent(indentLevel + 2)}if (item{depth} is not null)");
									statements.Add($"{Indent(indentLevel + 2)}{{");
									statements.AddRange(collectionStatements);
									statements.Add($"{Indent(indentLevel + 2)}}}");
									statements.Add($"{Indent(indentLevel + 1)}}}");
									statements.Add($"{Indent(indentLevel)}}}");
								}
							}
						}
					}
				}
			}
		}

		_ = visitedTypes.Remove(typeSymbol);

		return statements;
	}

	private static bool ShouldSkipProperty(
		IPropertySymbol propertySymbol,
		INamedTypeSymbol typeSymbol,
		TrimmingSymbolContext trimmingSymbols) =>
		HasAttribute(propertySymbol, trimmingSymbols.DoNotTrimAttribute) ||
		HasAttribute(propertySymbol, trimmingSymbols.AllowMutablePropertyAttribute) ||
		ImplementsInterfaceProperty(propertySymbol, typeSymbol, trimmingSymbols.ConcurrencyStampInterface);

	private static bool HasTrimmableBaseType(INamedTypeSymbol typeSymbol, INamedTypeSymbol trimmableInterface)
	{
		for (INamedTypeSymbol? baseType = typeSymbol.BaseType;
			baseType is not null;
			baseType = baseType.BaseType)
		{
			if (ImplementsInterface(baseType, trimmableInterface))
				return true;
		}

		return false;
	}

	private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol) =>
		attributeSymbol is not null &&
		symbol.GetAttributes().Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));

	private static bool ImplementsInterfaceProperty(
		IPropertySymbol propertySymbol,
		INamedTypeSymbol typeSymbol,
		INamedTypeSymbol? interfaceSymbol)
	{
		if (interfaceSymbol is null)
			return false;

		return interfaceSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Any(interfaceProperty =>
				SymbolEqualityComparer.Default.Equals(
					typeSymbol.FindImplementationForInterfaceMember(interfaceProperty),
					propertySymbol));
	}

	private static bool IsSystemType(INamedTypeSymbol type, string? allowNamespace = null)
	{
		string? namespaceName = type.ContainingNamespace?.ToDisplayString();

		if (!string.IsNullOrEmpty(allowNamespace) && namespaceName?.StartsWith(allowNamespace, StringComparison.Ordinal) == true)
			return false;

		return namespaceName?.StartsWith("System", StringComparison.Ordinal) is true ||
			   namespaceName?.StartsWith("Microsoft", StringComparison.Ordinal) is true;
	}

	private static bool IsDictionaryType(ITypeSymbol type)
	{
		if (type is INamedTypeSymbol namedType)
		{
			string[] dictionaryInterfaces =
			[
				"System.Collections.Generic.IDictionary",
				"System.Collections.Generic.IReadOnlyDictionary"
			];

			return namedType.AllInterfaces.Any(i =>
				dictionaryInterfaces.Any(di => i.ToDisplayString().StartsWith(di, StringComparison.Ordinal)));
		}

		return false;
	}

	private static (ITypeSymbol? KeyType, ITypeSymbol? ValueType) GetDictionaryKeyValueTypes(ITypeSymbol type)
	{
		if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length >= 2)
		{
			return (namedType.TypeArguments[0], namedType.TypeArguments[1]);
		}

		return (null, null);
	}

	private static bool IsCollectionType(ITypeSymbol type)
	{
		if (type is IArrayTypeSymbol)
			return true;

		if (type is INamedTypeSymbol namedType)
		{
			string[] collectionInterfaces =
			[
				"System.Collections.Generic.IList",
				"System.Collections.Generic.ICollection",
				"System.Collections.Generic.IEnumerable"
			];

			return namedType.AllInterfaces.Any(i =>
				collectionInterfaces.Any(ci => i.ToDisplayString().StartsWith(ci, StringComparison.Ordinal)));
		}

		return false;
	}

	private static ITypeSymbol? GetCollectionElementType(ITypeSymbol type)
	{
		if (type is IArrayTypeSymbol arrayType)
			return arrayType.ElementType;

		if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
			return namedType.TypeArguments[0];

		return null;
	}

	private static string Indent(int level) => new('\t', level);

	private sealed class TrimmingSymbolContext
	{
		public INamedTypeSymbol TrimmableInterface { get; }

		public INamedTypeSymbol? DoNotTrimAttribute { get; }

		public INamedTypeSymbol? AllowMutablePropertyAttribute { get; }

		public INamedTypeSymbol? ConcurrencyStampInterface { get; }

		public TrimmingSymbolContext(
			INamedTypeSymbol trimmableInterface,
			INamedTypeSymbol? doNotTrimAttribute,
			INamedTypeSymbol? allowMutablePropertyAttribute,
			INamedTypeSymbol? concurrencyStampInterface)
		{
			TrimmableInterface = trimmableInterface;
			DoNotTrimAttribute = doNotTrimAttribute;
			AllowMutablePropertyAttribute = allowMutablePropertyAttribute;
			ConcurrencyStampInterface = concurrencyStampInterface;
		}
	}
}
