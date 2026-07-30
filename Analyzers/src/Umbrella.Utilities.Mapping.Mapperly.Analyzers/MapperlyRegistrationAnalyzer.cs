using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
	private const string MapperAttributeMetadataName = "Riok.Mapperly.Abstractions.MapperAttribute";
	private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";
	private const string MapAsyncMethodName = "MapAsync";
	private const string MapAllAsyncMethodName = "MapAllAsync";

	private static readonly SymbolDisplayFormat _messageDisplayFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

	/// <summary>
	/// Diagnostic emitted when a closed mapper call has no exact Mapperly registration.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingExactMappingRule = new(
		id: "UMA001",
		title: "IUmbrellaMapper calls must target an exact Mapperly registration",
		messageFormat: "No exact Mapperly {0} mapping is registered for source type '{1}' and destination type '{2}'",
		category: "MapperlyRegistration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "Strict Mapperly mapping requires an exact source/destination registration for each IUmbrellaMapper operation.");

	/// <summary>
	/// Diagnostic emitted when a mapper call cannot be resolved to known closed constructions.
	/// </summary>
	public static readonly DiagnosticDescriptor OpenGenericMapperCallRule = new(
		id: "UMA002",
		title: "Open generic IUmbrellaMapper calls cannot be fully validated",
		messageFormat: "Mapperly {0} call uses open generic type argument(s) for source '{1}' and destination '{2}', and no complete set of closed source constructions could be validated",
		category: "MapperlyRegistration",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Open generic IUmbrellaMapper calls are validated against known closed source constructions. Calls that remain open require manual review.");

	/// <summary>
	/// Diagnostic emitted when a Mapperly mapper class is not partial or is inaccessible to the generated catalog.
	/// </summary>
	public static readonly DiagnosticDescriptor MapperClassMustBePublicPartialRule = new(
		id: "UMA003",
		title: "Mapperly mapper classes must be partial and accessible",
		messageFormat: "Mapper class '{0}' must be partial and accessible to the generated Umbrella mapper catalog",
		category: "UmbrellaMapperStandards",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Classes decorated with [Mapper] must be partial. Internal and public mapper classes are supported, but the generated catalog must be able to access the mapper type.");

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
			if (state is not null)
			{
				var openCalls = new ConcurrentBag<OpenMapperCall>();

				startContext.RegisterSyntaxNodeAction(
					syntaxContext => AnalyzeInvocation(syntaxContext, state, openCalls),
					SyntaxKind.InvocationExpression);

				startContext.RegisterCompilationEndAction(
					endContext => AnalyzeOpenMapperCalls(endContext, state, openCalls));
			}

			INamedTypeSymbol? mapperAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(MapperAttributeMetadataName);
			if (mapperAttributeSymbol is not null)
			{
				startContext.RegisterSymbolAction(
					symbolContext => AnalyzeMapperType(symbolContext, mapperAttributeSymbol),
					SymbolKind.NamedType);
			}
		});
	}

	private static void AnalyzeInvocation(
		SyntaxNodeAnalysisContext context,
		AnalyzerState state,
		ConcurrentBag<OpenMapperCall> openCalls)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!TryGetMapperCall(methodSymbol, state, out MapperOperation operation, out ITypeSymbol? sourceType, out ITypeSymbol? destinationType))
		{
			return;
		}

		if (ContainsOpenTypeParameter(sourceType) || ContainsOpenTypeParameter(destinationType))
		{
			if (context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, context.CancellationToken) is IMethodSymbol containingMethod &&
				containingMethod.ContainingType is not null)
			{
				openCalls.Add(new OpenMapperCall(
					invocation.GetLocation(),
					containingMethod,
					operation,
					sourceType,
					destinationType));
			}
			else
			{
				ReportOpenGenericDiagnostic(context.ReportDiagnostic, invocation.GetLocation(), operation, sourceType, destinationType);
			}

			return;
		}

		ReportMissingMappingIfRequired(
			context.ReportDiagnostic,
			invocation.GetLocation(),
			state,
			new MappingKey(operation, sourceType, destinationType));
	}

	private static void AnalyzeOpenMapperCalls(
		CompilationAnalysisContext context,
		AnalyzerState state,
		ConcurrentBag<OpenMapperCall> openCalls)
	{
		ImmutableArray<INamedTypeSymbol> sourceTypes = GetAllSourceTypes(context.Compilation.Assembly.GlobalNamespace);

		foreach (OpenMapperCall openCall in openCalls)
		{
			bool foundClosedConstruction = false;
			bool hasUnresolvedConstruction = false;
			var checkedMappings = new HashSet<MappingKey>(MappingKeyComparer.Instance);

			foreach (INamedTypeSymbol candidateType in sourceTypes)
			{
				if (candidateType.IsAbstract ||
					ContainsOpenTypeParameter(candidateType) ||
					!TryFindConstructedContainingType(candidateType, openCall.ContainingMethod.ContainingType, out INamedTypeSymbol? constructedContainingType))
				{
					continue;
				}

				foundClosedConstruction = true;
				if (IsContainingMethodOverridden(candidateType, constructedContainingType, openCall.ContainingMethod))
					continue;

				ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions = CreateTypeSubstitutions(constructedContainingType);
				ITypeSymbol sourceType = SubstituteType(openCall.SourceType, substitutions, context.Compilation);
				ITypeSymbol destinationType = SubstituteType(openCall.DestinationType, substitutions, context.Compilation);

				if (ContainsOpenTypeParameter(sourceType) || ContainsOpenTypeParameter(destinationType))
				{
					hasUnresolvedConstruction = true;
					continue;
				}

				var mappingKey = new MappingKey(openCall.Operation, sourceType, destinationType);
				if (checkedMappings.Add(mappingKey))
					ReportMissingMappingIfRequired(context.ReportDiagnostic, openCall.Location, state, mappingKey);
			}

			if (!foundClosedConstruction || hasUnresolvedConstruction)
			{
				ReportOpenGenericDiagnostic(
					context.ReportDiagnostic,
					openCall.Location,
					openCall.Operation,
					openCall.SourceType,
					openCall.DestinationType);
			}
		}
	}

	private static void ReportMissingMappingIfRequired(
		Action<Diagnostic> reportDiagnostic,
		Location location,
		AnalyzerState state,
		MappingKey mappingKey)
	{
		if (state.RegisteredMappings.Contains(mappingKey))
			return;

		reportDiagnostic(Diagnostic.Create(
			MissingExactMappingRule,
			location,
			GetOperationDisplayName(mappingKey.Operation),
			mappingKey.SourceType.ToDisplayString(_messageDisplayFormat),
			mappingKey.DestinationType.ToDisplayString(_messageDisplayFormat)));
	}

	private static void ReportOpenGenericDiagnostic(
		Action<Diagnostic> reportDiagnostic,
		Location location,
		MapperOperation operation,
		ITypeSymbol sourceType,
		ITypeSymbol destinationType)
	{
		reportDiagnostic(Diagnostic.Create(
			OpenGenericMapperCallRule,
			location,
			GetOperationDisplayName(operation),
			sourceType.ToDisplayString(_messageDisplayFormat),
			destinationType.ToDisplayString(_messageDisplayFormat)));
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

		if (methodSymbol.TypeArguments.Length is not 2)
			return false;

		foreach (MapperMethod mapperMethod in state.MapperMethods)
		{
			if (!IsInterfaceMethodOrImplementation(methodSymbol, mapperMethod.Method))
				continue;

			operation = mapperMethod.Operation;
			sourceType = methodSymbol.TypeArguments[0];
			destinationType = methodSymbol.TypeArguments[1];
			return true;
		}

		return false;
	}

	private static bool IsInterfaceMethodOrImplementation(IMethodSymbol methodSymbol, IMethodSymbol interfaceMethod)
	{
		if (SymbolEqualityComparer.Default.Equals(methodSymbol.OriginalDefinition, interfaceMethod.OriginalDefinition))
			return true;

		ISymbol? implementation = methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod);
		return implementation is IMethodSymbol implementationMethod &&
			SymbolEqualityComparer.Default.Equals(methodSymbol.OriginalDefinition, implementationMethod.OriginalDefinition);
	}

	private static ImmutableArray<MapperMethod> GetMapperMethods(
		INamedTypeSymbol mapperInterfaceSymbol,
		INamedTypeSymbol cancellationTokenSymbol)
	{
		ImmutableArray<MapperMethod>.Builder builder = ImmutableArray.CreateBuilder<MapperMethod>();

		foreach (IMethodSymbol method in mapperInterfaceSymbol.GetMembers().OfType<IMethodSymbol>())
		{
			if (method.TypeParameters.Length is not 2)
				continue;

			if (method.Name is MapAllAsyncMethodName &&
				method.Parameters.Length is 2 &&
				SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, cancellationTokenSymbol))
			{
				builder.Add(new MapperMethod(method, MapperOperation.NewCollection));
			}
			else if (method.Name is MapAsyncMethodName &&
				method.Parameters.Length is 2 &&
				SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, cancellationTokenSymbol))
			{
				builder.Add(new MapperMethod(method, MapperOperation.NewInstance));
			}
			else if (method.Name is MapAsyncMethodName &&
				method.Parameters.Length is 3 &&
				SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, cancellationTokenSymbol))
			{
				builder.Add(new MapperMethod(method, MapperOperation.ExistingInstance));
			}
		}

		return builder.ToImmutable();
	}

	private static ImmutableHashSet<MappingKey> CollectRegisteredMappings(
		IReadOnlyCollection<INamedTypeSymbol> configuredCatalogs,
		INamedTypeSymbol catalogMappingAttributeSymbol)
	{
		ImmutableHashSet<MappingKey>.Builder builder = ImmutableHashSet.CreateBuilder<MappingKey>(MappingKeyComparer.Instance);

		foreach (INamedTypeSymbol catalogType in configuredCatalogs)
		{
			foreach (AttributeData attributeData in catalogType.GetAttributes())
			{
				if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, catalogMappingAttributeSymbol) &&
					TryCreateMappingKey(attributeData, out MappingKey key))
				{
					_ = builder.Add(key);
				}
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
			if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, catalogReferenceAttributeSymbol) &&
				attributeData.ConstructorArguments.Length is 1 &&
				attributeData.ConstructorArguments[0].Value is INamedTypeSymbol catalogType &&
				catalogType.TypeKind is not TypeKind.Error &&
				ImplementsCatalogInterface(catalogType, catalogInterfaceSymbol) &&
				seenCatalogs.Add(catalogType))
			{
				builder.Add(catalogType);
			}
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

		mappingKey = new MappingKey(operation, sourceType, destinationType);
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
			INamedTypeSymbol namedTypeSymbol =>
				(namedTypeSymbol.ContainingType is not null && ContainsOpenTypeParameter(namedTypeSymbol.ContainingType)) ||
				namedTypeSymbol.TypeArguments.Any(ContainsOpenTypeParameter),
			IPointerTypeSymbol pointerTypeSymbol => ContainsOpenTypeParameter(pointerTypeSymbol.PointedAtType),
			_ => false
		};
	}

	private static void AnalyzeMapperType(SymbolAnalysisContext context, INamedTypeSymbol mapperAttributeSymbol)
	{
		var mapperType = (INamedTypeSymbol)context.Symbol;
		if (mapperType.TypeKind is not TypeKind.Class ||
			!mapperType.GetAttributes().Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, mapperAttributeSymbol)))
		{
			return;
		}

		ImmutableArray<TypeDeclarationSyntax> declarations =
		[
			.. mapperType.DeclaringSyntaxReferences
				.Select(x => x.GetSyntax(context.CancellationToken))
				.OfType<TypeDeclarationSyntax>()
		];

		if (declarations.Length is 0)
			return;

		bool isPartial = declarations.All(x => x.Modifiers.Any(SyntaxKind.PartialKeyword));
		if (isPartial && IsAccessibleToGeneratedCatalog(mapperType))
			return;

		context.ReportDiagnostic(Diagnostic.Create(
			MapperClassMustBePublicPartialRule,
			declarations[0].Identifier.GetLocation(),
			mapperType.Name));
	}

	private static bool IsAccessibleToGeneratedCatalog(INamedTypeSymbol typeSymbol)
	{
		for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.ContainingType)
		{
			if (current.DeclaredAccessibility is not (
				Accessibility.Public or
				Accessibility.Internal or
				Accessibility.ProtectedOrInternal))
			{
				return false;
			}
		}

		return true;
	}

	private static ImmutableArray<INamedTypeSymbol> GetAllSourceTypes(INamespaceSymbol globalNamespace)
	{
		ImmutableArray<INamedTypeSymbol>.Builder builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		AddNamespaceTypes(globalNamespace, builder);
		return builder.ToImmutable();
	}

	private static void AddNamespaceTypes(
		INamespaceSymbol namespaceSymbol,
		ImmutableArray<INamedTypeSymbol>.Builder builder)
	{
		foreach (INamedTypeSymbol typeSymbol in namespaceSymbol.GetTypeMembers())
			AddTypeAndNestedTypes(typeSymbol, builder);

		foreach (INamespaceSymbol childNamespace in namespaceSymbol.GetNamespaceMembers())
			AddNamespaceTypes(childNamespace, builder);
	}

	private static void AddTypeAndNestedTypes(
		INamedTypeSymbol typeSymbol,
		ImmutableArray<INamedTypeSymbol>.Builder builder)
	{
		builder.Add(typeSymbol);

		foreach (INamedTypeSymbol nestedType in typeSymbol.GetTypeMembers())
			AddTypeAndNestedTypes(nestedType, builder);
	}

	private static bool TryFindConstructedContainingType(
		INamedTypeSymbol candidateType,
		INamedTypeSymbol openContainingType,
		out INamedTypeSymbol constructedContainingType)
	{
		for (INamedTypeSymbol? current = candidateType; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, openContainingType.OriginalDefinition))
			{
				constructedContainingType = current;
				return true;
			}
		}

		constructedContainingType = null!;
		return false;
	}

	private static bool IsContainingMethodOverridden(
		INamedTypeSymbol candidateType,
		INamedTypeSymbol constructedContainingType,
		IMethodSymbol containingMethod)
	{
		for (INamedTypeSymbol? current = candidateType;
			current is not null && !SymbolEqualityComparer.Default.Equals(current, constructedContainingType);
			current = current.BaseType)
		{
			foreach (IMethodSymbol method in current.GetMembers(containingMethod.Name).OfType<IMethodSymbol>())
			{
				for (IMethodSymbol? overriddenMethod = method.OverriddenMethod;
					overriddenMethod is not null;
					overriddenMethod = overriddenMethod.OverriddenMethod)
				{
					if (SymbolEqualityComparer.Default.Equals(
						overriddenMethod.OriginalDefinition,
						containingMethod.OriginalDefinition))
					{
						return true;
					}
				}
			}
		}

		return false;
	}

	private static ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> CreateTypeSubstitutions(
		INamedTypeSymbol constructedType)
	{
		ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol>.Builder builder =
			ImmutableDictionary.CreateBuilder<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);

		AddTypeSubstitutions(constructedType, builder);
		return builder.ToImmutable();
	}

	private static void AddTypeSubstitutions(
		INamedTypeSymbol constructedType,
		ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol>.Builder builder)
	{
		if (constructedType.ContainingType is not null)
			AddTypeSubstitutions(constructedType.ContainingType, builder);

		ImmutableArray<ITypeParameterSymbol> parameters = constructedType.OriginalDefinition.TypeParameters;
		ImmutableArray<ITypeSymbol> arguments = constructedType.TypeArguments;

		for (int i = 0; i < parameters.Length && i < arguments.Length; i++)
			builder[parameters[i]] = arguments[i];
	}

	private static ITypeSymbol SubstituteType(
		ITypeSymbol typeSymbol,
		ImmutableDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions,
		Compilation compilation)
	{
		switch (typeSymbol)
		{
			case ITypeParameterSymbol typeParameter when substitutions.TryGetValue(typeParameter, out ITypeSymbol? replacement):
				return replacement.WithNullableAnnotation(typeSymbol.NullableAnnotation);

			case IArrayTypeSymbol arrayType:
				return compilation.CreateArrayTypeSymbol(
					SubstituteType(arrayType.ElementType, substitutions, compilation),
					arrayType.Rank,
					arrayType.NullableAnnotation);

			case IPointerTypeSymbol pointerType:
				return compilation.CreatePointerTypeSymbol(
					SubstituteType(pointerType.PointedAtType, substitutions, compilation));

			case INamedTypeSymbol namedType when namedType.TypeArguments.Length > 0:
				ITypeSymbol[] typeArguments =
				[
					.. namedType.TypeArguments.Select(x => SubstituteType(x, substitutions, compilation))
				];

				return namedType.OriginalDefinition
					.Construct(typeArguments)
					.WithNullableAnnotation(namedType.NullableAnnotation);

			default:
				return typeSymbol;
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

	private readonly record struct MappingKey(MapperOperation Operation, ITypeSymbol SourceType, ITypeSymbol DestinationType);

	private sealed class MappingKeyComparer : IEqualityComparer<MappingKey>
	{
		public static MappingKeyComparer Instance { get; } = new();

		public bool Equals(MappingKey x, MappingKey y)
			=> x.Operation == y.Operation &&
				SymbolEqualityComparer.Default.Equals(x.SourceType, y.SourceType) &&
				SymbolEqualityComparer.Default.Equals(x.DestinationType, y.DestinationType);

		public int GetHashCode(MappingKey obj)
		{
			unchecked
			{
				int hashCode = (int)obj.Operation;
				hashCode = (hashCode * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.SourceType);
				hashCode = (hashCode * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.DestinationType);
				return hashCode;
			}
		}
	}

	private readonly record struct MapperMethod(IMethodSymbol Method, MapperOperation Operation);

	private readonly record struct OpenMapperCall(
		Location Location,
		IMethodSymbol ContainingMethod,
		MapperOperation Operation,
		ITypeSymbol SourceType,
		ITypeSymbol DestinationType);

	private enum MapperOperation
	{
		NewInstance,
		NewCollection,
		ExistingInstance
	}

	private sealed record AnalyzerState(
		ImmutableArray<MapperMethod> MapperMethods,
		ImmutableHashSet<MappingKey> RegisteredMappings)
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

			ImmutableArray<MapperMethod> mapperMethods = GetMapperMethods(mapperInterfaceSymbol, cancellationTokenSymbol);
			if (mapperMethods.Length is 0)
				return null;

			ImmutableArray<INamedTypeSymbol> configuredCatalogs = GetConfiguredCatalogs(
				compilation.Assembly,
				catalogInterfaceSymbol,
				catalogReferenceAttributeSymbol);

			ImmutableHashSet<MappingKey> mappings = CollectRegisteredMappings(
				configuredCatalogs,
				catalogMappingAttributeSymbol);

			return new AnalyzerState(mapperMethods, mappings);
		}
	}
}
