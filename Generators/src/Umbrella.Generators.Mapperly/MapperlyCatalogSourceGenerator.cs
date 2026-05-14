using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Umbrella.Generators.Mapperly;

/// <summary>
/// Generates an explicit Mapperly catalog for all mapper types declared in the consuming assembly.
/// </summary>
[Generator]
public sealed class MapperlyCatalogSourceGenerator : IIncrementalGenerator
{
	private const string CatalogInterfaceName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyCatalog";
	private const string RegistryBuilderName = "Umbrella.Utilities.Mapping.Mapperly.UmbrellaMapperRegistryBuilder";
	private const string NewInstanceMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyNewInstanceMapper`2";
	private const string NewInstanceAsyncMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyNewInstanceAsyncMapper`2";
	private const string NewCollectionMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyNewCollectionMapper`2";
	private const string NewCollectionAsyncMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyNewCollectionAsyncMapper`2";
	private const string ExistingInstanceMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyExistingInstanceMapper`2";
	private const string ExistingInstanceAsyncMapperName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyExistingInstanceAsyncMapper`2";
	private const string CatalogReferenceAttributeName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogReferenceAttribute";
	private const string CatalogMappingAttributeName = "Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMappingAttribute";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Cheap syntax-only filter: only type declarations that declare a base list can implement mapper interfaces.
		IncrementalValuesProvider<TypeDeclarationSyntax> candidateTypes = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
				transform: static (ctx, _) => (TypeDeclarationSyntax)ctx.Node);

		IncrementalValueProvider<(Compilation Compilation, ImmutableArray<TypeDeclarationSyntax> CandidateTypes)> combined =
			context.CompilationProvider.Combine(candidateTypes.Collect());

		context.RegisterSourceOutput(combined, static (spc, state) => Execute(spc, state.Compilation, state.CandidateTypes));
	}

	private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<TypeDeclarationSyntax> candidateTypes)
	{
		if (compilation.GetTypeByMetadataName(CatalogInterfaceName) is null ||
			compilation.GetTypeByMetadataName(RegistryBuilderName) is null ||
			compilation.GetTypeByMetadataName(CatalogReferenceAttributeName) is null ||
			compilation.GetTypeByMetadataName(CatalogMappingAttributeName) is null ||
			compilation.GetTypeByMetadataName(NewInstanceMapperName) is not INamedTypeSymbol newInstanceMapperSymbol ||
			compilation.GetTypeByMetadataName(NewInstanceAsyncMapperName) is not INamedTypeSymbol newInstanceAsyncMapperSymbol ||
			compilation.GetTypeByMetadataName(NewCollectionMapperName) is not INamedTypeSymbol newCollectionMapperSymbol ||
			compilation.GetTypeByMetadataName(NewCollectionAsyncMapperName) is not INamedTypeSymbol newCollectionAsyncMapperSymbol ||
			compilation.GetTypeByMetadataName(ExistingInstanceMapperName) is not INamedTypeSymbol existingInstanceMapperSymbol ||
			compilation.GetTypeByMetadataName(ExistingInstanceAsyncMapperName) is not INamedTypeSymbol existingInstanceAsyncMapperSymbol)
		{
			return;
		}

		List<MapperDescriptor> descriptors = CollectMapperDescriptors(
			compilation,
			candidateTypes,
			newInstanceMapperSymbol,
			newInstanceAsyncMapperSymbol,
			newCollectionMapperSymbol,
			newCollectionAsyncMapperSymbol,
			existingInstanceMapperSymbol,
			existingInstanceAsyncMapperSymbol,
			context.CancellationToken);

		if (descriptors.Count is 0)
			return;

		string assemblyName = string.IsNullOrWhiteSpace(compilation.AssemblyName) ? "Assembly" : compilation.AssemblyName!;
		string sanitizedAssemblyName = SanitizeIdentifier(assemblyName);
		string catalogTypeName = $"{sanitizedAssemblyName}UmbrellaMapperlyCatalog";

		context.AddSource(
			$"{catalogTypeName}.g.cs",
			SourceText.From(GenerateSource(catalogTypeName, descriptors), Encoding.UTF8));
	}

	private static List<MapperDescriptor> CollectMapperDescriptors(
		Compilation compilation,
		ImmutableArray<TypeDeclarationSyntax> candidateTypes,
		INamedTypeSymbol newInstanceMapperSymbol,
		INamedTypeSymbol newInstanceAsyncMapperSymbol,
		INamedTypeSymbol newCollectionMapperSymbol,
		INamedTypeSymbol newCollectionAsyncMapperSymbol,
		INamedTypeSymbol existingInstanceMapperSymbol,
		INamedTypeSymbol existingInstanceAsyncMapperSymbol,
		CancellationToken cancellationToken)
	{
		HashSet<INamedTypeSymbol> mapperTypes = [];

		foreach (TypeDeclarationSyntax typeDeclaration in candidateTypes)
		{
			cancellationToken.ThrowIfCancellationRequested();

			SemanticModel semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);

			if (semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not INamedTypeSymbol typeSymbol ||
				typeSymbol.IsAbstract ||
				typeSymbol.TypeKind is TypeKind.Interface)
			{
				continue;
			}

			if (!typeSymbol.AllInterfaces.Any(x => x.IsGenericType && IsMapperInterface(x, newInstanceMapperSymbol, newInstanceAsyncMapperSymbol, newCollectionMapperSymbol, newCollectionAsyncMapperSymbol, existingInstanceMapperSymbol, existingInstanceAsyncMapperSymbol)))
				continue;

			_ = mapperTypes.Add(typeSymbol);
		}

		List<MapperDescriptor> descriptors = [];

		foreach (INamedTypeSymbol mapperType in mapperTypes.OrderBy(x => x.ToDisplayString()))
		{
			HashSet<(string Operation, string SourceType, string DestinationType)> emittedOperations = [];
			var interfaces = mapperType.AllInterfaces.Where(x => x.IsGenericType).ToList();

			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, newInstanceAsyncMapperSymbol, "AddAsyncNewInstance");
			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, newInstanceMapperSymbol, "AddNewInstance");
			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, newCollectionAsyncMapperSymbol, "AddAsyncNewCollection");
			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, newCollectionMapperSymbol, "AddNewCollection");
			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, existingInstanceAsyncMapperSymbol, "AddAsyncExistingInstance");
			AddDescriptors(descriptors, emittedOperations, mapperType, interfaces, existingInstanceMapperSymbol, "AddExistingInstance");
		}

		return descriptors;
	}

	private static void AddDescriptors(
		ICollection<MapperDescriptor> descriptors,
		HashSet<(string Operation, string SourceType, string DestinationType)> emittedOperations,
		INamedTypeSymbol mapperType,
		IEnumerable<INamedTypeSymbol> interfaces,
		INamedTypeSymbol targetInterface,
		string registrationMethodName)
	{
		foreach (INamedTypeSymbol implementedInterface in interfaces.Where(x => SymbolEqualityComparer.Default.Equals(x.OriginalDefinition, targetInterface)))
		{
			ITypeSymbol sourceType = implementedInterface.TypeArguments[0];
			ITypeSymbol destinationType = implementedInterface.TypeArguments[1];

			string sourceTypeName = sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			string destinationTypeName = destinationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

			var key = (NormalizeOperationKey(registrationMethodName), sourceTypeName, destinationTypeName);

			if (emittedOperations.Contains(key))
				continue;

			if (registrationMethodName.StartsWith("Add", StringComparison.Ordinal) &&
				!registrationMethodName.StartsWith("AddAsync", StringComparison.Ordinal) &&
				emittedOperations.Contains((NormalizeOperationKey($"AddAsync{registrationMethodName[3..]}"), sourceTypeName, destinationTypeName)))
			{
				continue;
			}

			_ = emittedOperations.Add(key);

			descriptors.Add(new MapperDescriptor(
				mapperType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
				sourceTypeName,
				destinationTypeName,
				registrationMethodName));
		}
	}

	private static bool IsMapperInterface(
		INamedTypeSymbol interfaceSymbol,
		params INamedTypeSymbol[] mapperInterfaces)
		=> mapperInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, x));

	private static string NormalizeOperationKey(string registrationMethodName)
		=> registrationMethodName switch
		{
			"AddAsyncNewInstance" or "AddNewInstance" => "NewInstance",
			"AddAsyncNewCollection" or "AddNewCollection" => "NewCollection",
			"AddAsyncExistingInstance" or "AddExistingInstance" => "ExistingInstance",
			_ => registrationMethodName
		};

	private static string GenerateSource(string catalogTypeName, IReadOnlyCollection<MapperDescriptor> descriptors)
	{
		string[] serviceRegistrations = descriptors
			.Select(x => x.MapperTypeName)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(x => x, StringComparer.Ordinal)
			.Select(x => $"		_ = global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<{x}>(services);")
			.ToArray();

		string[] mappingRegistrations = descriptors
			.OrderBy(x => x.MapperTypeName, StringComparer.Ordinal)
			.ThenBy(x => x.SourceTypeName, StringComparer.Ordinal)
			.ThenBy(x => x.DestinationTypeName, StringComparer.Ordinal)
			.ThenBy(x => x.RegistrationMethodName, StringComparer.Ordinal)
			.Select(x => $"		_ = builder.{x.RegistrationMethodName}<{x.MapperTypeName}, {x.SourceTypeName}, {x.DestinationTypeName}>();")
			.ToArray();

		string[] mappingAttributes = descriptors
			.OrderBy(x => x.OperationKind, StringComparer.Ordinal)
			.ThenBy(x => x.SourceTypeName, StringComparer.Ordinal)
			.ThenBy(x => x.DestinationTypeName, StringComparer.Ordinal)
			.Select(x =>
				$"[global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogMapping(typeof({x.SourceTypeName}), typeof({x.DestinationTypeName}), " +
				$"global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogOperationKind.{x.OperationKind})]")
			.ToArray();

		StringBuilder builder = new();
		_ = builder.AppendLine("// <auto-generated />");
		_ = builder.AppendLine("#nullable enable");
		_ = builder.AppendLine($"[assembly: global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.UmbrellaMapperlyCatalogReference(typeof(global::Umbrella.Generated.Mapping.Mapperly.{catalogTypeName}))]");
		_ = builder.AppendLine();
		_ = builder.AppendLine("namespace Umbrella.Generated.Mapping.Mapperly;");
		_ = builder.AppendLine();

		foreach (string mappingAttribute in mappingAttributes)
			_ = builder.AppendLine(mappingAttribute);

		_ = builder.AppendLine($"public sealed class {catalogTypeName} : global::Umbrella.Utilities.Mapping.Mapperly.Abstractions.IUmbrellaMapperlyCatalog");
		_ = builder.AppendLine("{");
		_ = builder.AppendLine($"	public static {catalogTypeName} Instance {{ get; }} = new();");
		_ = builder.AppendLine();
		_ = builder.AppendLine($"	private {catalogTypeName}()");
		_ = builder.AppendLine("	{");
		_ = builder.AppendLine("	}");
		_ = builder.AppendLine();
		_ = builder.AppendLine("	public void AddServices(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
		_ = builder.AppendLine("	{");

		foreach (string serviceRegistration in serviceRegistrations)
			_ = builder.AppendLine(serviceRegistration);

		_ = builder.AppendLine("	}");
		_ = builder.AppendLine();
		_ = builder.AppendLine("	public void AddMappings(global::Umbrella.Utilities.Mapping.Mapperly.UmbrellaMapperRegistryBuilder builder)");
		_ = builder.AppendLine("	{");

		foreach (string mappingRegistration in mappingRegistrations)
			_ = builder.AppendLine(mappingRegistration);

		_ = builder.AppendLine("	}");
		_ = builder.AppendLine("}");

		return builder.ToString();
	}

	private static string SanitizeIdentifier(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "Assembly";

		StringBuilder builder = new(value.Length);

		foreach (char character in value)
			_ = builder.Append(char.IsLetterOrDigit(character) ? character : '_');

		if (!char.IsLetter(builder[0]) && builder[0] != '_')
			_ = builder.Insert(0, '_');

		return builder.ToString();
	}

	private sealed record MapperDescriptor(
		string MapperTypeName,
		string SourceTypeName,
		string DestinationTypeName,
		string RegistrationMethodName)
	{
		public string OperationKind { get; } = NormalizeOperationKey(RegistrationMethodName);
	}
}
