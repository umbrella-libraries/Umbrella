using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Umbrella.Utilities.Mapping.Mapperly.Analyzers;

/// <summary>
/// Roslyn analyzer that validates explicit Mapperly catalog registrations for <c>IUmbrellaMapper</c> call sites.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MapperlyRegistrationAnalyzer : DiagnosticAnalyzer
{
	private const string MapperInterfaceMetadataName = "Umbrella.Utilities.Mapping.Abstractions.IUmbrellaMapper";
	private const string CatalogInterfaceMetadataName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyCatalog";
	private const string CatalogReferenceAttributeMetadataName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogReferenceAttribute";
	private const string CatalogMappingAttributeMetadataName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMappingAttribute";
	private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
	private const string MapAsyncMethodName = "MapAsync";
	private const string MapAllAsyncMethodName = "MapAllAsync";

	private static readonly SymbolDisplayFormat _keyDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat;
	private static readonly SymbolDisplayFormat _messageDisplayFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

	/// <summary>
	/// Diagnostic emitted when a closed mapper call has no exact Mapperly registration.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingExactMappingRule = new(
		id: "UA019",
		title: "IUmbrellaMapper calls must target an exact Mapperly registration",
		messageFormat: "No exact Mapperly {0} mapping is registered for source type '{1}' and destination type '{2}'",
		category: "MapperlyRegistration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Strict Mapperly mapping requires an exact source/destination registration for each IUmbrellaMapper operation.");

	/// <summary>
	/// Diagnostic emitted when a mapper call uses open generic type arguments.
	/// </summary>
	public static readonly DiagnosticDescriptor OpenGenericMapperCallRule = new(
		id: "UA020",
		title: "Open generic IUmbrellaMapper calls cannot be fully validated",
		messageFormat: "Mapperly {0} call uses open generic type argument(s) for source '{1}' and destination '{2}', so exact registration cannot be proven at the generic definition site",
		category: "MapperlyRegistration",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Open generic IUmbrellaMapper calls are valid only for some closed constructions, so the analyzer reports them as warnings for manual review.");

	/// <summary>
	/// Diagnostic emitted when a Mapperly mapper class is not declared as <c>public partial class</c>.
	/// </summary>
	public static readonly DiagnosticDescriptor MapperClassMustBePublicPartialRule = new(
		id: "UA025",
		title: "Mapperly mapper classes must be public partial class",
		messageFormat: "Mapper class '{0}' must be declared as 'public partial class' so the Umbrella source generator can discover and register it",
		category: "UmbrellaMapperStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Mapper classes decorated with [Mapper] must be public and partial. The Umbrella.Generators.Mapperly source generator only scans public types. A non-public or non-partial mapper is silently skipped and never registered in the catalog.");

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingExactMappingRule, OpenGenericMapperCallRule, MapperClassMustBePublicPartialRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			var state = AnalyzerState.Create(startContext.Compilation);

			if (state is null || !state.HasConfiguredCatalogs)
				return;

			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeInvocation(syntaxContext, state), Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
		});

		context.RegisterCompilationStartAction(startContext =>
		{
			INamedTypeSymbol? mapperAttributeSymbol = startContext.Compilation.GetTypeByMetadataName("Riok.Mapperly.Abstractions.MapperAttribute");
			if (mapperAttributeSymbol is null)
				return;

			startContext.RegisterSyntaxNodeAction(
				ctx => AnalyzeMapperClassDeclaration(ctx, mapperAttributeSymbol),
				Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration);
		});
	}

	private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, AnalyzerState state)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!TryGetMapperCall(methodSymbol, state, out MapperOperation operation, out ITypeSymbol? sourceType, out ITypeSymbol? destinationType))
		{
			return;
		}

		string operationDisplay = GetOperationDisplayName(operation);
		string sourceDisplay = sourceType.ToDisplayString(_messageDisplayFormat);
		string destinationDisplay = destinationType.ToDisplayString(_messageDisplayFormat);

		if (ContainsOpenTypeParameter(sourceType) || ContainsOpenTypeParameter(destinationType))
		{
			context.ReportDiagnostic(Diagnostic.Create(
				OpenGenericMapperCallRule,
				invocation.GetLocation(),
				operationDisplay,
				sourceDisplay,
				destinationDisplay));

			return;
		}

		var key = new MappingKey(
			operation,
			sourceType.ToDisplayString(_keyDisplayFormat),
			destinationType.ToDisplayString(_keyDisplayFormat));

		if (state.RegisteredMappings.Contains(key))
			return;

		context.ReportDiagnostic(Diagnostic.Create(
			MissingExactMappingRule,
			invocation.GetLocation(),
			operationDisplay,
			sourceDisplay,
			destinationDisplay));
	}

	private static bool TryGetMapperCall(
		IMethodSymbol methodSymbol,
		AnalyzerState state,
		out MapperOperation operation,
		out ITypeSymbol sourceType,
		out ITypeSymbol destinationType)
	{
		operation = default;
		sourceType = null!;
		destinationType = null!;

		if (methodSymbol.TypeArguments.Length is not 2 ||
			!IsMapperMethodContainer(methodSymbol.ContainingType, state.MapperInterfaceSymbol))
		{
			return false;
		}

		if (methodSymbol.Name is MapAllAsyncMethodName &&
			methodSymbol.Parameters.Length is 2 &&
			SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[1].Type, state.CancellationTokenSymbol))
		{
			operation = MapperOperation.NewCollection;
		}
		else if (methodSymbol.Name is MapAsyncMethodName)
		{
			if (methodSymbol.Parameters.Length is 2 &&
				SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[1].Type, state.CancellationTokenSymbol))
			{
				operation = MapperOperation.NewInstance;
			}
			else if (methodSymbol.Parameters.Length is 3 &&
				SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[2].Type, state.CancellationTokenSymbol))
			{
				operation = MapperOperation.ExistingInstance;
			}
			else
			{
				return false;
			}
		}
		else
		{
			return false;
		}

		sourceType = methodSymbol.TypeArguments[0];
		destinationType = methodSymbol.TypeArguments[1];

		return true;
	}

	private static bool IsMapperMethodContainer(INamedTypeSymbol containingType, INamedTypeSymbol mapperInterfaceSymbol)
	{
		if (SymbolEqualityComparer.Default.Equals(containingType, mapperInterfaceSymbol))
			return true;

		return containingType.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, mapperInterfaceSymbol));
	}

	private static ImmutableHashSet<MappingKey> CollectRegisteredMappings(
		IReadOnlyCollection<INamedTypeSymbol> configuredCatalogs,
		INamedTypeSymbol catalogMappingAttributeSymbol)
	{
		ImmutableHashSet<MappingKey>.Builder builder = ImmutableHashSet.CreateBuilder<MappingKey>();

		foreach (INamedTypeSymbol catalogType in configuredCatalogs)
		{
			foreach (AttributeData attributeData in catalogType.GetAttributes())
			{
				if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, catalogMappingAttributeSymbol) ||
					!TryCreateMappingKey(attributeData, out MappingKey key))
				{
					continue;
				}

				_ = builder.Add(key);
			}
		}

		return builder.ToImmutable();
	}

	private static ImmutableArray<INamedTypeSymbol> GetConfiguredCatalogs(
		IAssemblySymbol assemblySymbol,
		INamedTypeSymbol catalogInterfaceSymbol,
		INamedTypeSymbol catalogReferenceAttributeSymbol)
	{
		ImmutableArray<INamedTypeSymbol>.Builder builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		HashSet<INamedTypeSymbol> seenCatalogs = new(SymbolEqualityComparer.Default);

		foreach (AttributeData attributeData in assemblySymbol.GetAttributes())
		{
			if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, catalogReferenceAttributeSymbol) ||
				attributeData.ConstructorArguments.Length is not 1 ||
				attributeData.ConstructorArguments[0].Value is not INamedTypeSymbol catalogType ||
				catalogType.TypeKind is TypeKind.Error ||
				!ImplementsCatalogInterface(catalogType, catalogInterfaceSymbol) ||
				!seenCatalogs.Add(catalogType))
			{
				continue;
			}

			builder.Add(catalogType);
		}

		return builder.ToImmutable();
	}

	private static bool ImplementsCatalogInterface(INamedTypeSymbol catalogType, INamedTypeSymbol catalogInterfaceSymbol)
	{
		if (SymbolEqualityComparer.Default.Equals(catalogType, catalogInterfaceSymbol))
			return true;

		return catalogType.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, catalogInterfaceSymbol));
	}

	private static bool TryCreateMappingKey(AttributeData attributeData, out MappingKey mappingKey)
	{
		mappingKey = default;

		if (attributeData.ConstructorArguments.Length is not 3 ||
			attributeData.ConstructorArguments[0].Value is not ITypeSymbol sourceType ||
			attributeData.ConstructorArguments[1].Value is not ITypeSymbol destinationType ||
			attributeData.ConstructorArguments[2].Value is not int operationValue ||
			!TryGetOperation(operationValue, out MapperOperation operation))
		{
			return false;
		}

		mappingKey = new MappingKey(
			operation,
			sourceType.ToDisplayString(_keyDisplayFormat),
			destinationType.ToDisplayString(_keyDisplayFormat));

		return true;
	}

	private static bool TryGetOperation(int operationValue, out MapperOperation operation)
	{
		switch (operationValue)
		{
			case (int)MapperOperation.NewInstance:
				operation = MapperOperation.NewInstance;
				return true;
			case (int)MapperOperation.NewCollection:
				operation = MapperOperation.NewCollection;
				return true;
			case (int)MapperOperation.ExistingInstance:
				operation = MapperOperation.ExistingInstance;
				return true;
			default:
				operation = default;
				return false;
		}
	}

	private static bool ContainsOpenTypeParameter(ITypeSymbol typeSymbol)
	{
		if (typeSymbol is ITypeParameterSymbol)
			return true;

		return typeSymbol switch
		{
			IArrayTypeSymbol arrayTypeSymbol => ContainsOpenTypeParameter(arrayTypeSymbol.ElementType),
			INamedTypeSymbol namedTypeSymbol => namedTypeSymbol.TypeArguments.Any(ContainsOpenTypeParameter),
			IPointerTypeSymbol pointerTypeSymbol => ContainsOpenTypeParameter(pointerTypeSymbol.PointedAtType),
			_ => false
		};
	}

	private static void AnalyzeMapperClassDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol mapperAttributeSymbol)
	{
		var classDecl = (ClassDeclarationSyntax)context.Node;

		if (classDecl.AttributeLists.Count == 0)
			return;

		if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
			return;

		bool hasMapperAttribute = false;
		foreach (AttributeData attr in classSymbol.GetAttributes())
		{
			if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, mapperAttributeSymbol))
			{
				hasMapperAttribute = true;
				break;
			}
		}

		if (!hasMapperAttribute)
			return;

		bool isPublic = classSymbol.DeclaredAccessibility == Accessibility.Public;
		bool isPartial = classDecl.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword);

		if (!isPublic || !isPartial)
		{
			context.ReportDiagnostic(Diagnostic.Create(
				MapperClassMustBePublicPartialRule,
				classDecl.Identifier.GetLocation(),
				classSymbol.Name));
		}
	}

	private static string GetOperationDisplayName(MapperOperation operation)
		=> operation switch
		{
			MapperOperation.NewInstance => "new-instance",
			MapperOperation.NewCollection => "new-collection",
			MapperOperation.ExistingInstance => "existing-instance",
			_ => throw new ArgumentOutOfRangeException(nameof(operation))
		};

	private readonly record struct MappingKey(MapperOperation Operation, string SourceTypeName, string DestinationTypeName);

	private enum MapperOperation
	{
		NewInstance,
		NewCollection,
		ExistingInstance
	}

	private sealed record AnalyzerState(
		INamedTypeSymbol MapperInterfaceSymbol,
		INamedTypeSymbol CancellationTokenSymbol,
		ImmutableHashSet<MappingKey> RegisteredMappings,
		bool HasConfiguredCatalogs)
	{
		public static AnalyzerState? Create(Compilation compilation)
		{
			INamedTypeSymbol? mapperInterfaceSymbol = compilation.GetTypeByMetadataName(MapperInterfaceMetadataName);
			INamedTypeSymbol? cancellationTokenSymbol = compilation.GetTypeByMetadataName(CancellationTokenMetadataName);
			INamedTypeSymbol? catalogInterfaceSymbol = compilation.GetTypeByMetadataName(CatalogInterfaceMetadataName);
			INamedTypeSymbol? catalogReferenceAttributeSymbol = compilation.GetTypeByMetadataName(CatalogReferenceAttributeMetadataName);
			INamedTypeSymbol? catalogMappingAttributeSymbol = compilation.GetTypeByMetadataName(CatalogMappingAttributeMetadataName);

			if (mapperInterfaceSymbol is null ||
				cancellationTokenSymbol is null ||
				catalogInterfaceSymbol is null ||
				catalogReferenceAttributeSymbol is null ||
				catalogMappingAttributeSymbol is null)
			{
				return null;
			}

			ImmutableArray<INamedTypeSymbol> configuredCatalogs = GetConfiguredCatalogs(compilation.Assembly, catalogInterfaceSymbol, catalogReferenceAttributeSymbol);

			if (configuredCatalogs.Length is 0)
				return new AnalyzerState(mapperInterfaceSymbol, cancellationTokenSymbol, ImmutableHashSet<MappingKey>.Empty, false);

			ImmutableHashSet<MappingKey> mappings = CollectRegisteredMappings(configuredCatalogs, catalogMappingAttributeSymbol);

			return new AnalyzerState(mapperInterfaceSymbol, cancellationTokenSymbol, mappings, true);
		}
	}
}
