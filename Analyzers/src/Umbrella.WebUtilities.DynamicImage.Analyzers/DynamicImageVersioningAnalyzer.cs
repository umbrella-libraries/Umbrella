using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Umbrella.DynamicImage.RazorAnalysis;

namespace Umbrella.WebUtilities.DynamicImage.Analyzers;

/// <summary>
/// Roslyn analyzer that enforces the DynamicImage URL/version-token pairing convention for Umbrella model types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DynamicImageVersioningAnalyzer : DiagnosticAnalyzer
{
	private const string DynamicImageMiddlewareOptionsMetadataName = "Umbrella.WebUtilities.DynamicImage.Middleware.Options.DynamicImageMiddlewareOptions";
	private const string EnableUrlFingerprintingPropertyName = "EnableUrlFingerprinting";
	private const string EnableUrlFingerprintingBuildPropertyName = "build_property.UmbrellaDynamicImageEnableUrlFingerprinting";

	private static readonly string[] _dynamicImagePropertyIndicators =
	[
		"image",
		"thumbnail",
		"photo",
		"picture",
		"logo",
		"icon",
		"avatar",
		"banner"
	];

	/// <summary>
	/// Diagnostic rule that requires DynamicImage URL properties to declare matching version token properties.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenPropertyRule = new(
		id: "UWDI001",
		title: "DynamicImage URL properties must declare matching version token properties",
		messageFormat: "Property '{0}' in model type '{1}' must declare a matching '{2}' property when DynamicImage URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "DynamicImage URL properties in Umbrella model types must declare matching VersionToken properties when middleware URL fingerprinting is explicitly enabled.");

	/// <summary>
	/// Diagnostic rule that requires DynamicImage URL assignments to assign the matching version token property.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenAssignmentRule = new(
		id: "UWDI002",
		title: "DynamicImage URL assignments must also assign matching version tokens",
		messageFormat: "Assignment to '{0}' must also assign '{1}' in the same model construction or update flow when DynamicImage URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "DynamicImage URL assignments in Umbrella model construction and mapping flows must also assign the matching VersionToken property when middleware URL fingerprinting is explicitly enabled.");

	/// <summary>
	/// Diagnostic rule that requires UmbrellaDynamicImage and DynamicImage tag helper usages to assign the VersionToken
	/// input.
	/// </summary>
	public static readonly DiagnosticDescriptor MissingVersionTokenUsageRule = new(
		id: "UWDI003",
		title: "DynamicImage UI usages must assign VersionToken",
		messageFormat: "DynamicImage usage bound to '{0}' must also assign '{1}' when URL fingerprinting is explicitly enabled",
		category: "DynamicImageVersioning",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "UmbrellaDynamicImage and DynamicImage tag helper usages bound to DynamicImage URL model properties must also assign the matching VersionToken input when middleware URL fingerprinting is explicitly enabled.");

	/// <summary>
	/// Diagnostic rule that warns when DynamicImage variant-shaping inputs are too dynamic for reliable source-generated
	/// catalog discovery.
	/// </summary>
	public static readonly DiagnosticDescriptor NonStaticVariantShapingInputRule = new(
		id: "UWDI004",
		title: "DynamicImage variant discovery coverage is reduced by non-static inputs",
		messageFormat: "DynamicImage usage assigns non-static variant-shaping input(s) '{0}', so source-generated variant discovery and validation coverage may be incomplete; this does not affect runtime rendering",
		category: "DynamicImageGeneration",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "DynamicImage usages should keep variant-shaping inputs static when possible so source-generated catalogs can discover and validate the expected variants.",
		customTags: [WellKnownDiagnosticTags.CompilationEnd]);

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MissingVersionTokenPropertyRule, MissingVersionTokenAssignmentRule, MissingVersionTokenUsageRule, NonStaticVariantShapingInputRule];

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(startContext =>
		{
			bool buildPropertyEnabled = startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
				EnableUrlFingerprintingBuildPropertyName,
				out string? buildPropertyValue) &&
				bool.TryParse(buildPropertyValue, out bool parsedBuildPropertyValue) &&
				parsedBuildPropertyValue;
			var state = new DynamicImageVersioningState(buildPropertyEnabled);
			var activationSymbols = DynamicImageActivationSymbols.Create(startContext.Compilation);

			if (activationSymbols is not null)
			{
				startContext.RegisterSyntaxNodeAction(
					syntaxContext => AnalyzeDynamicImageRegistrationInvocation(syntaxContext, state, activationSymbols),
					SyntaxKind.InvocationExpression);
			}

			startContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, state), SymbolKind.NamedType);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeObjectInitializer(syntaxContext, state), SyntaxKind.ObjectInitializerExpression);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeAssignmentExpression(syntaxContext, state), SyntaxKind.SimpleAssignmentExpression);
			startContext.RegisterSyntaxNodeAction(syntaxContext => AnalyzeBlock(syntaxContext, state), SyntaxKind.Block);
			startContext.RegisterCompilationEndAction(compilationContext =>
			{
				AnalyzeRazorVariantDiscoveryCoverage(compilationContext);
				ReportDiagnostics(compilationContext, state);
			});
		});
	}

	private static void AnalyzeDynamicImageRegistrationInvocation(
		SyntaxNodeAnalysisContext context,
		DynamicImageVersioningState state,
		DynamicImageActivationSymbols activationSymbols)
	{
		var invocation = (InvocationExpressionSyntax)context.Node;

		if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!IsDynamicImageRegistrationMethod(methodSymbol, activationSymbols.OptionsType))
		{
			return;
		}

		ArgumentSyntax? optionsBuilderArgument = GetOptionsBuilderArgument(invocation, methodSymbol);

		bool explicitlyEnabled = optionsBuilderArgument is not null &&
			HasExplicitTrueFingerprintingAssignment(
				optionsBuilderArgument.Expression,
				context.SemanticModel,
				activationSymbols.EnableUrlFingerprintingProperty,
				context.CancellationToken);

		state.MarkRegistration(explicitlyEnabled);
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context, DynamicImageVersioningState state)
	{
		var typeSymbol = (INamedTypeSymbol)context.Symbol;

		if (!IsRelevantModelType(typeSymbol))
			return;

		var properties = typeSymbol.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(x => !x.IsStatic && x.Parameters.Length is 0)
			.ToImmutableArray();

		foreach (IPropertySymbol property in properties)
		{
			if (!TryGetDynamicImageVersionTokenPropertyName(property, out string versionTokenPropertyName))
				continue;

			if (TryGetMatchingProperty(properties, versionTokenPropertyName) is { Type.SpecialType: SpecialType.System_String })
				continue;

			Location location = property.Locations.FirstOrDefault() ?? Location.None;
			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenPropertyRule,
				location,
				property.Name,
				typeSymbol.Name,
				versionTokenPropertyName));
		}
	}

	private static void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var initializer = (InitializerExpressionSyntax)context.Node;

		if (GetCreatedTypeSymbol(context, initializer) is not { } createdType || !IsRelevantModelType(createdType))
			return;

		AssignmentExpressionSyntax[] assignments = [.. initializer.Expressions.OfType<AssignmentExpressionSyntax>()];

		foreach (AssignmentExpressionSyntax assignment in assignments)
		{
			if (!TryGetDynamicImageVersionTokenPropertyName(context.SemanticModel, createdType, assignment.Left, context.CancellationToken, out string urlPropertyName, out string versionTokenPropertyName))
				continue;

			if (TryGetMatchingProperty(createdType.GetMembers().OfType<IPropertySymbol>().ToImmutableArray(), versionTokenPropertyName) is not { Type.SpecialType: SpecialType.System_String })
				continue;

			bool hasMatchingAssignment = assignments.Any(x =>
				!ReferenceEquals(x, assignment) &&
				TryGetAssignedPropertyName(context.SemanticModel, createdType, x.Left, context.CancellationToken, out string propertyName) &&
				string.Equals(propertyName, versionTokenPropertyName, StringComparison.Ordinal));

			if (hasMatchingAssignment)
				continue;

			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenAssignmentRule,
				GetAssignmentLocation(assignment.Left),
				urlPropertyName,
				versionTokenPropertyName));
		}
	}

	private static void AnalyzeAssignmentExpression(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var assignment = (AssignmentExpressionSyntax)context.Node;

		if (assignment.Parent is InitializerExpressionSyntax || assignment.Parent is not ExpressionStatementSyntax)
			return;

		if (!TryGetAssignedModelProperty(context.SemanticModel, assignment.Left, context.CancellationToken, out IPropertySymbol? propertySymbol, out ISymbol? receiverSymbol, out string receiverText))
			return;

		if (propertySymbol is null)
			return;

		if (!TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out string versionTokenPropertyName))
			return;

		var properties = propertySymbol.ContainingType.GetMembers().OfType<IPropertySymbol>().ToImmutableArray();
		if (TryGetMatchingProperty(properties, versionTokenPropertyName) is not { Type.SpecialType: SpecialType.System_String })
			return;

		BlockSyntax? containingBlock = assignment.FirstAncestorOrSelf<BlockSyntax>();

		if (containingBlock is null)
			return;

		bool hasMatchingAssignment = containingBlock.Statements
			.OfType<ExpressionStatementSyntax>()
			.Select(x => x.Expression)
			.OfType<AssignmentExpressionSyntax>()
			.Where(x => !ReferenceEquals(x, assignment))
			.Any(x => IsMatchingVersionTokenAssignment(context.SemanticModel, x, versionTokenPropertyName, receiverSymbol, receiverText, context.CancellationToken));

		if (hasMatchingAssignment)
			return;

		state.AddDiagnostic(Diagnostic.Create(
			MissingVersionTokenAssignmentRule,
			GetAssignmentLocation(assignment.Left),
			propertySymbol.Name,
			versionTokenPropertyName));
	}

	private static void AnalyzeBlock(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state)
	{
		var block = (BlockSyntax)context.Node;

		AnalyzeBlazorComponentUsages(context, state, block);
		AnalyzeTagHelperUsages(context, state, block);

		if (!IsRazorGeneratedSyntaxTree(block.SyntaxTree))
		{
			AnalyzeBlazorComponentVariantDiscoveryCoverage(context, block);
			AnalyzeTagHelperVariantDiscoveryCoverage(context, block);
		}
	}

	private static void AnalyzeRazorVariantDiscoveryCoverage(CompilationAnalysisContext context)
	{
		bool hasComponentType = context.Compilation.GetTypeByMetadataName("Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage") is not null;
		bool hasImageTagHelperType = context.Compilation.GetTypeByMetadataName("Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper") is not null;
		bool hasPictureSourceTagHelperType = context.Compilation.GetTypeByMetadataName("Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper") is not null;

		if (!hasComponentType && !hasImageTagHelperType && !hasPictureSourceTagHelperType)
			return;

		DynamicImageRazorDocument[] documents =
		[
			.. context.Options.AdditionalFiles
				.Where(x => IsRazorFile(x.Path))
				.Select(x => CreateRazorDocument(context, x))
				.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
				.Select(x => x.First())
		];

		foreach (DynamicImageRazorUsage usage in DynamicImageRazorSourceParser.Parse(
			documents,
			hasComponentType,
			hasImageTagHelperType,
			hasPictureSourceTagHelperType))
		{
			bool isTagHelper = usage.Kind is not DynamicImageRazorUsageKind.Component;
			string urlName = isTagHelper ? "src" : "Url";
			DynamicImageRazorAttribute? urlAttribute = usage.Attributes.FirstOrDefault(x => string.Equals(x.Name, urlName, StringComparison.OrdinalIgnoreCase));

			if (urlAttribute is not null &&
				DynamicImageRazorSourceParser.TryGetStaticString(urlAttribute.Value, out string url) &&
				IsStaticHttpUrl(url))
			{
				continue;
			}

			var nonStaticInputs = new List<(string Name, DynamicImageRazorAttribute Attribute)>();

			foreach (DynamicImageRazorAttribute attribute in usage.Attributes)
			{
				string normalizedName = NormalizeRazorAttributeName(attribute.Name, isTagHelper);

				if (!TryGetVariantShapingInputName(normalizedName, isTagHelper, out string displayName) ||
					DynamicImageRazorSourceParser.IsDiscoverableValue(normalizedName, attribute.Value, isTagHelper))
				{
					continue;
				}

				if (!nonStaticInputs.Any(x => string.Equals(x.Name, displayName, StringComparison.Ordinal)))
					nonStaticInputs.Add((displayName, attribute));
			}

			if (nonStaticInputs.Count is 0)
				continue;

			var sourceText = SourceText.From(usage.Document.Text);
			DynamicImageRazorAttribute firstAttribute = nonStaticInputs[0].Attribute;
			var span = new TextSpan(firstAttribute.NameStart, firstAttribute.NameLength);
			var location = Location.Create(
				usage.Document.Path,
				span,
				sourceText.Lines.GetLinePositionSpan(span));

			context.ReportDiagnostic(Diagnostic.Create(
				NonStaticVariantShapingInputRule,
				location,
				string.Join(", ", nonStaticInputs.Select(x => x.Name))));
		}
	}

	private static string NormalizeRazorAttributeName(string name, bool isTagHelper)
	{
		if (!isTagHelper)
			return name;

		return name.ToLowerInvariant() switch
		{
			"width-request" => "WidthRequest",
			"height-request" => "HeightRequest",
			"resize-mode" => "ResizeMode",
			"image-format" => "ImageFormat",
			"image-density" => "ImageMaxPixelDensity",
			"size-widths" => "SizeWidths",
			_ => name
		};
	}

	private static bool IsRazorGeneratedSyntaxTree(SyntaxTree syntaxTree)
	{
		string path = syntaxTree.FilePath;
		return path.EndsWith(".razor.g.cs", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".cshtml.g.cs", StringComparison.OrdinalIgnoreCase) ||
			   path.IndexOf("_razor.g.cs", StringComparison.OrdinalIgnoreCase) >= 0 ||
			   path.IndexOf("_cshtml.g.cs", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool IsRazorFile(string path)
	{
		if (path.EndsWith(".umbrella-dynamic-image", StringComparison.OrdinalIgnoreCase))
			path = path.Substring(0, path.Length - ".umbrella-dynamic-image".Length);

		string extension = Path.GetExtension(path);
		return string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase);
	}

	private static DynamicImageRazorDocument CreateRazorDocument(CompilationAnalysisContext context, AdditionalText file)
	{
		AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(file);
		_ = options.TryGetValue("build_metadata.AdditionalFiles.UmbrellaDynamicImageOriginalSourcePath", out string? originalSourcePath);
		string path = string.IsNullOrWhiteSpace(originalSourcePath) ? file.Path : originalSourcePath!;
		string text = (file.GetText(context.CancellationToken) ?? SourceText.From(string.Empty)).ToString();
		return new DynamicImageRazorDocument(path, text, string.Empty);
	}

	private static void ReportDiagnostics(CompilationAnalysisContext context, DynamicImageVersioningState state)
	{
		if (!state.IsEnabled)
			return;

		foreach (Diagnostic diagnostic in state.Diagnostics)
			context.ReportDiagnostic(diagnostic);
	}

	private static void AnalyzeBlazorComponentUsages(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state, BlockSyntax block)
	{
		var statements = block.Statements;

		for (int i = 0; i < statements.Count; i++)
		{
			if (!TryGetInvocationStatement(statements[i], out InvocationExpressionSyntax? invocation) || invocation is null)
				continue;

			if (!TryGetInvocationReceiverText(invocation, out string? receiverText) ||
				!IsOpenComponentInvocation(context.SemanticModel, invocation, context.CancellationToken, out bool isDynamicImageComponent) ||
				!isDynamicImageComponent)
			{
				continue;
			}

			int nestingDepth = 1;
			string? urlPropertyName = null;
			string? versionTokenPropertyName = null;
			Location? diagnosticLocation = null;
			bool hasVersionTokenAssignment = false;

			for (int j = i + 1; j < statements.Count; j++)
			{
				if (!TryGetInvocationStatement(statements[j], out InvocationExpressionSyntax? nextInvocation) || nextInvocation is null)
					continue;

				if (!TryGetInvocationReceiverText(nextInvocation, out string? nextReceiverText) ||
					!string.Equals(receiverText, nextReceiverText, StringComparison.Ordinal))
				{
					continue;
				}

				if (IsOpenComponentInvocation(context.SemanticModel, nextInvocation, context.CancellationToken, out _))
				{
					nestingDepth++;
					continue;
				}

				if (IsCloseComponentInvocation(context.SemanticModel, nextInvocation))
				{
					nestingDepth--;

					if (nestingDepth is 0)
					{
						if (urlPropertyName is not null && versionTokenPropertyName is not null && !hasVersionTokenAssignment)
						{
							state.AddDiagnostic(Diagnostic.Create(
								MissingVersionTokenUsageRule,
								diagnosticLocation ?? nextInvocation.GetLocation(),
								urlPropertyName,
								versionTokenPropertyName));
						}

						i = j;
						break;
					}

					continue;
				}

				if (nestingDepth is not 1 ||
					!TryGetRenderTreeAttribute(nextInvocation, context.SemanticModel, context.CancellationToken, out string attributeName, out ExpressionSyntax? valueExpression, out Location attributeLocation))
				{
					continue;
				}

				if (string.Equals(attributeName, "Url", StringComparison.Ordinal) &&
					valueExpression is not null &&
					TryGetDynamicImageModelPropertyReference(context.SemanticModel, valueExpression, context.CancellationToken, out IPropertySymbol? propertySymbol, out string expectedVersionTokenPropertyName))
				{
					urlPropertyName = propertySymbol.Name;
					versionTokenPropertyName = expectedVersionTokenPropertyName;
					diagnosticLocation = attributeLocation;
					continue;
				}

				if (string.Equals(attributeName, "VersionToken", StringComparison.Ordinal))
					hasVersionTokenAssignment = true;
			}
		}
	}

	private static void AnalyzeTagHelperUsages(SyntaxNodeAnalysisContext context, DynamicImageVersioningState state, BlockSyntax block)
	{
		var urlAssignments = new Dictionary<string, (string UrlPropertyName, string VersionTokenPropertyName, Location Location)>(StringComparer.Ordinal);
		var versionTokenAssignments = new HashSet<string>(StringComparer.Ordinal);

		foreach (StatementSyntax statement in block.Statements)
		{
			if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
				continue;

			if (!TryGetAssignedTagHelperProperty(context.SemanticModel, assignment.Left, context.CancellationToken, out string receiverText, out string propertyName))
				continue;

			if (string.Equals(propertyName, "VersionToken", StringComparison.Ordinal))
			{
				_ = versionTokenAssignments.Add(receiverText);
				continue;
			}

			if (!string.Equals(propertyName, "Src", StringComparison.Ordinal) ||
				!TryGetDynamicImageModelPropertyReference(context.SemanticModel, assignment.Right, context.CancellationToken, out IPropertySymbol? propertySymbol, out string versionTokenPropertyName))
			{
				continue;
			}

			urlAssignments[receiverText] = (propertySymbol.Name, versionTokenPropertyName, GetAssignmentLocation(assignment.Left));
		}

		foreach (KeyValuePair<string, (string UrlPropertyName, string VersionTokenPropertyName, Location Location)> entry in urlAssignments)
		{
			string receiverText = entry.Key;

			if (versionTokenAssignments.Contains(receiverText))
				continue;

			state.AddDiagnostic(Diagnostic.Create(
				MissingVersionTokenUsageRule,
				entry.Value.Location,
				entry.Value.UrlPropertyName,
				entry.Value.VersionTokenPropertyName));
		}
	}

	private static void AnalyzeBlazorComponentVariantDiscoveryCoverage(SyntaxNodeAnalysisContext context, BlockSyntax block)
	{
		var statements = block.Statements;

		for (int i = 0; i < statements.Count; i++)
		{
			if (!TryGetInvocationStatement(statements[i], out InvocationExpressionSyntax? invocation) || invocation is null)
				continue;

			if (!TryGetInvocationReceiverText(invocation, out string? receiverText) ||
				!IsOpenComponentInvocation(context.SemanticModel, invocation, context.CancellationToken, out bool isDynamicImageComponent) ||
				!isDynamicImageComponent)
			{
				continue;
			}

			int nestingDepth = 1;
			bool hasUrlAssignment = false;
			bool isStaticHttpUrl = false;
			var nonStaticInputs = new List<string>();
			Location? diagnosticLocation = null;

			for (int j = i + 1; j < statements.Count; j++)
			{
				if (!TryGetInvocationStatement(statements[j], out InvocationExpressionSyntax? nextInvocation) || nextInvocation is null)
					continue;

				if (!TryGetInvocationReceiverText(nextInvocation, out string? nextReceiverText) ||
					!string.Equals(receiverText, nextReceiverText, StringComparison.Ordinal))
				{
					continue;
				}

				if (IsOpenComponentInvocation(context.SemanticModel, nextInvocation, context.CancellationToken, out _))
				{
					nestingDepth++;
					continue;
				}

				if (IsCloseComponentInvocation(context.SemanticModel, nextInvocation))
				{
					nestingDepth--;

					if (nestingDepth is 0)
					{
						ReportVariantDiscoveryCoverageDiagnostic(context, hasUrlAssignment, isStaticHttpUrl, nonStaticInputs, diagnosticLocation);
						i = j;
						break;
					}

					continue;
				}

				if (nestingDepth is not 1 ||
					!TryGetRenderTreeAttribute(nextInvocation, context.SemanticModel, context.CancellationToken, out string attributeName, out ExpressionSyntax? valueExpression, out Location attributeLocation))
				{
					continue;
				}

				if (string.Equals(attributeName, "Url", StringComparison.Ordinal))
				{
					hasUrlAssignment = true;
					isStaticHttpUrl = TryGetStaticString(valueExpression, context.SemanticModel, context.CancellationToken, out string? urlValue) && IsStaticHttpUrl(urlValue);
					continue;
				}

				if (!TryGetVariantShapingInputName(attributeName, isTagHelper: false, out string displayName) ||
					IsStaticVariantShapingValue(attributeName, valueExpression, context.SemanticModel, isTagHelper: false, context.CancellationToken))
				{
					continue;
				}

				AddNonStaticVariantInput(nonStaticInputs, displayName);
				diagnosticLocation ??= attributeLocation;
			}
		}
	}

	private static void AnalyzeTagHelperVariantDiscoveryCoverage(SyntaxNodeAnalysisContext context, BlockSyntax block)
	{
		var statesByReceiver = new Dictionary<string, VariantDiscoveryUsageState>(StringComparer.Ordinal);

		foreach (StatementSyntax statement in block.Statements)
		{
			if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
				continue;

			if (!TryGetAssignedDynamicImageTagHelperProperty(context.SemanticModel, assignment.Left, context.CancellationToken, out string receiverText, out string propertyName))
				continue;

			if (!statesByReceiver.TryGetValue(receiverText, out VariantDiscoveryUsageState? usageState))
			{
				usageState = new VariantDiscoveryUsageState();
				statesByReceiver.Add(receiverText, usageState);
			}

			if (string.Equals(propertyName, "Src", StringComparison.Ordinal))
			{
				usageState.HasUrlAssignment = true;
				usageState.IsStaticHttpUrl = TryGetStaticString(assignment.Right, context.SemanticModel, context.CancellationToken, out string? urlValue) && IsStaticHttpUrl(urlValue);
				continue;
			}

			if (!TryGetVariantShapingInputName(propertyName, isTagHelper: true, out string displayName) ||
				IsStaticVariantShapingValue(propertyName, assignment.Right, context.SemanticModel, isTagHelper: true, context.CancellationToken))
			{
				continue;
			}

			AddNonStaticVariantInput(usageState.NonStaticInputs, displayName);
			usageState.DiagnosticLocation ??= GetAssignmentLocation(assignment.Left);
		}

		foreach (VariantDiscoveryUsageState usageState in statesByReceiver.Values)
			ReportVariantDiscoveryCoverageDiagnostic(context, usageState.HasUrlAssignment, usageState.IsStaticHttpUrl, usageState.NonStaticInputs, usageState.DiagnosticLocation);
	}

	private static bool IsMatchingVersionTokenAssignment(
		SemanticModel semanticModel,
		AssignmentExpressionSyntax assignment,
		string versionTokenPropertyName,
		ISymbol? expectedReceiverSymbol,
		string expectedReceiverText,
		CancellationToken cancellationToken)
	{
		if (!TryGetAssignedModelProperty(semanticModel, assignment.Left, cancellationToken, out IPropertySymbol? propertySymbol, out ISymbol? receiverSymbol, out string receiverText))
			return false;

		if (!string.Equals(propertySymbol.Name, versionTokenPropertyName, StringComparison.Ordinal))
			return false;

		if (expectedReceiverSymbol is not null && receiverSymbol is not null)
			return SymbolEqualityComparer.Default.Equals(expectedReceiverSymbol, receiverSymbol);

		return string.Equals(receiverText, expectedReceiverText, StringComparison.Ordinal);
	}

	private static bool TryGetAssignedModelProperty(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		[NotNullWhen(true)] out IPropertySymbol? propertySymbol,
		out ISymbol? receiverSymbol,
		out string receiverText)
	{
		propertySymbol = null;
		receiverSymbol = null;
		receiverText = string.Empty;

		if (expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		propertySymbol = semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol as IPropertySymbol;

		if (propertySymbol is null || !IsRelevantModelType(propertySymbol.ContainingType))
			return false;

		if (propertySymbol.IsStatic || propertySymbol.Parameters.Length > 0)
			return false;

		receiverText = memberAccess.Expression.ToString();
		receiverSymbol = semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol;

		return true;
	}

	private static bool TryGetAssignedTagHelperProperty(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string receiverText,
		out string propertyName)
	{
		receiverText = string.Empty;
		propertyName = string.Empty;

		if (expression is not MemberAccessExpressionSyntax memberAccess ||
			semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is not IPropertySymbol propertySymbol ||
			!IsDynamicImageTagHelperProperty(propertySymbol))
		{
			return false;
		}

		receiverText = memberAccess.Expression.ToString();
		propertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetAssignedDynamicImageTagHelperProperty(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string receiverText,
		out string propertyName)
	{
		receiverText = string.Empty;
		propertyName = string.Empty;

		if (expression is not MemberAccessExpressionSyntax memberAccess ||
			semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is not IPropertySymbol propertySymbol ||
			!IsDynamicImageTagHelperType(propertySymbol.ContainingType))
		{
			return false;
		}

		receiverText = memberAccess.Expression.ToString();
		propertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetAssignedPropertyName(
		SemanticModel semanticModel,
		INamedTypeSymbol createdType,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string propertyName)
	{
		propertyName = string.Empty;

		if (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol is not IPropertySymbol propertySymbol)
			return false;

		if (!SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, createdType))
			return false;

		propertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetDynamicImageVersionTokenPropertyName(
		SemanticModel semanticModel,
		INamedTypeSymbol createdType,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		out string urlPropertyName,
		out string versionTokenPropertyName)
	{
		urlPropertyName = string.Empty;
		versionTokenPropertyName = string.Empty;

		if (!TryGetAssignedPropertyName(semanticModel, createdType, expression, cancellationToken, out string propertyName))
			return false;

		IPropertySymbol? propertySymbol = createdType.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();

		if (propertySymbol is null)
			return false;

		if (!TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out versionTokenPropertyName))
			return false;

		urlPropertyName = propertySymbol.Name;
		return true;
	}

	private static bool TryGetDynamicImageVersionTokenPropertyName(IPropertySymbol propertySymbol, out string versionTokenPropertyName)
	{
		versionTokenPropertyName = string.Empty;

		if (propertySymbol.Type.SpecialType is not SpecialType.System_String)
			return false;

		if (!TryGetDynamicImageUrlPrefix(propertySymbol.Name, out string prefix))
			return false;

		versionTokenPropertyName = $"{prefix}VersionToken";
		return true;
	}

	private static bool TryGetDynamicImageModelPropertyReference(
		SemanticModel semanticModel,
		ExpressionSyntax expression,
		CancellationToken cancellationToken,
		[NotNullWhen(true)] out IPropertySymbol? propertySymbol,
		out string versionTokenPropertyName)
	{
		versionTokenPropertyName = string.Empty;

		ExpressionSyntax unwrappedExpression = UnwrapExpression(expression, semanticModel, cancellationToken);

		propertySymbol = semanticModel.GetSymbolInfo(unwrappedExpression, cancellationToken).Symbol as IPropertySymbol;

		return propertySymbol is not null &&
			   IsRelevantModelType(propertySymbol.ContainingType) &&
			   TryGetDynamicImageVersionTokenPropertyName(propertySymbol, out versionTokenPropertyName);
	}

	private static bool TryGetDynamicImageUrlPrefix(string propertyName, out string prefix)
	{
		prefix = string.Empty;

		if (!propertyName.EndsWith("Url", StringComparison.Ordinal) || propertyName.Length <= 3)
			return false;

		string candidatePrefix = propertyName[..^3];

		if (!_dynamicImagePropertyIndicators.Any(x => candidatePrefix.Contains(x, StringComparison.OrdinalIgnoreCase)))
			return false;

		prefix = candidatePrefix;
		return true;
	}

	private static IPropertySymbol? TryGetMatchingProperty(ImmutableArray<IPropertySymbol> properties, string propertyName)
		=> properties.FirstOrDefault(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal));

	private static bool TryGetVariantShapingInputName(string name, bool isTagHelper, out string displayName)
	{
		displayName = name;

		return isTagHelper
			? name is "WidthRequest" or "HeightRequest" or "ResizeMode" or "ImageFormat" or "ImageMaxPixelDensity" or "SizeWidths"
			: name is "WidthRequest" or "HeightRequest" or "ResizeMode" or "ImageFormat" or "MaxPixelDensity" or "SizeWidths";
	}

	private static bool TryGetInvocationStatement(StatementSyntax statement, [NotNullWhen(true)] out InvocationExpressionSyntax? invocation)
	{
		if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax expression })
		{
			invocation = expression;
			return true;
		}

		invocation = null;
		return false;
	}

	private static bool TryGetInvocationReceiverText(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out string? receiverText)
	{
		receiverText = null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		receiverText = memberAccess.Expression.ToString();
		return true;
	}

	private static bool IsOpenComponentInvocation(
		SemanticModel semanticModel,
		InvocationExpressionSyntax invocation,
		CancellationToken cancellationToken,
		out bool isDynamicImageComponent)
	{
		isDynamicImageComponent = false;

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!string.Equals(methodSymbol.Name, "OpenComponent", StringComparison.Ordinal))
		{
			return false;
		}

		isDynamicImageComponent = methodSymbol.TypeArguments.Length is 1 &&
			string.Equals(methodSymbol.TypeArguments[0].ToDisplayString(), "Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage", StringComparison.Ordinal);

		return true;
	}

	private static bool IsCloseComponentInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocation)
		=> semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol { Name: "CloseComponent" };

	private static bool TryGetRenderTreeAttribute(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string attributeName,
		out ExpressionSyntax? valueExpression,
		out Location attributeLocation)
	{
		attributeName = string.Empty;
		valueExpression = null;
		attributeLocation = invocation.GetLocation();

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			methodSymbol.Name is not ("AddAttribute" or "AddComponentParameter") ||
			invocation.ArgumentList.Arguments.Count < 2)
		{
			return false;
		}

		Optional<object?> attributeNameValue = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[1].Expression, cancellationToken);

		if (!attributeNameValue.HasValue || attributeNameValue.Value is not string attributeNameString)
			return false;

		attributeName = attributeNameString;
		valueExpression = invocation.ArgumentList.Arguments.Count > 2 ? invocation.ArgumentList.Arguments[2].Expression : null;
		attributeLocation = methodSymbol.Name is "AddComponentParameter"
			? GetGeneratedComponentParameterNameLocation(invocation.ArgumentList.Arguments[1].Expression, attributeNameString)
			: GetInvocationLocation(invocation);

		return true;
	}

	private static Location GetGeneratedComponentParameterNameLocation(ExpressionSyntax expression, string attributeName)
	{
		SimpleNameSyntax? matchingName = expression
			.DescendantNodesAndSelf()
			.OfType<SimpleNameSyntax>()
			.LastOrDefault(x => string.Equals(x.Identifier.ValueText, attributeName, StringComparison.Ordinal));

		return matchingName?.GetLocation() ?? expression.GetLocation();
	}

	private static Location GetInvocationLocation(InvocationExpressionSyntax invocation)
	{
		if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
			return memberAccess.Name.GetLocation();

		return invocation.GetLocation();
	}

	private static bool IsStaticVariantShapingValue(
		string propertyName,
		ExpressionSyntax? expression,
		SemanticModel semanticModel,
		bool isTagHelper,
		CancellationToken cancellationToken)
	{
		if (propertyName is "SizeWidths")
			return TryGetStaticString(expression, semanticModel, cancellationToken, out _);

		if (propertyName is "ResizeMode" or "ImageFormat")
			return TryGetStaticInt32(expression, semanticModel, cancellationToken, out _);

		if ((isTagHelper && propertyName is "ImageMaxPixelDensity") ||
			propertyName is "WidthRequest" or "HeightRequest" or "MaxPixelDensity")
		{
			return TryGetStaticInt32(expression, semanticModel, cancellationToken, out _);
		}

		return false;
	}

	private static ExpressionSyntax UnwrapExpression(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		ExpressionSyntax currentExpression = expression;

		while (true)
		{
			switch (currentExpression)
			{
				case ParenthesizedExpressionSyntax parenthesizedExpression:
					currentExpression = parenthesizedExpression.Expression;
					continue;
				case CastExpressionSyntax castExpression:
					currentExpression = castExpression.Expression;
					continue;
				case InvocationExpressionSyntax invocation when IsRuntimeTypeCheckInvocation(invocation, semanticModel, cancellationToken):
					currentExpression = invocation.ArgumentList.Arguments[0].Expression;
					continue;
				default:
					return currentExpression;
			}
		}
	}

	private static bool IsRuntimeTypeCheckInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (invocation.ArgumentList.Arguments.Count is 0 ||
			semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
		{
			return false;
		}

		return string.Equals(methodSymbol.Name, "TypeCheck", StringComparison.Ordinal) &&
			   string.Equals(methodSymbol.ContainingType.ToDisplayString(), "Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers", StringComparison.Ordinal);
	}

	private static INamedTypeSymbol? GetCreatedTypeSymbol(SyntaxNodeAnalysisContext context, InitializerExpressionSyntax initializer)
	{
		return initializer.Parent switch
		{
			ObjectCreationExpressionSyntax objectCreation => context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol,
			ImplicitObjectCreationExpressionSyntax objectCreation => context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type as INamedTypeSymbol,
			_ => null
		};
	}

	private static Location GetAssignmentLocation(ExpressionSyntax expression)
	{
		return expression switch
		{
			IdentifierNameSyntax identifierName => identifierName.GetLocation(),
			MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
			_ => expression.GetLocation()
		};
	}

	private static bool IsRelevantModelType(INamedTypeSymbol? typeSymbol)
	{
		if (typeSymbol is null)
			return false;

		string typeName = typeSymbol.Name;

		return typeName.EndsWith("Model", StringComparison.Ordinal) ||
			   typeName.EndsWith("ModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModel", StringComparison.Ordinal) ||
			   typeName.EndsWith("ViewModelBase", StringComparison.Ordinal) ||
			   typeName.EndsWith("QueryResult", StringComparison.Ordinal);
	}

	private static bool IsDynamicImageTagHelperProperty(IPropertySymbol propertySymbol)
	{
		if (propertySymbol.Name is not ("Src" or "VersionToken"))
			return false;

		return IsDynamicImageTagHelperType(propertySymbol.ContainingType);
	}

	private static bool IsDynamicImageTagHelperType(INamedTypeSymbol? typeSymbol)
	{
		for (INamedTypeSymbol? type = typeSymbol; type is not null; type = type.BaseType)
		{
			if (string.Equals(type.ToDisplayString(), "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelperBase", StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static void AddNonStaticVariantInput(List<string> nonStaticInputs, string displayName)
	{
		if (!nonStaticInputs.Contains(displayName, StringComparer.Ordinal))
			nonStaticInputs.Add(displayName);
	}

	private static void ReportVariantDiscoveryCoverageDiagnostic(
		SyntaxNodeAnalysisContext context,
		bool hasUrlAssignment,
		bool isStaticHttpUrl,
		List<string> nonStaticInputs,
		Location? diagnosticLocation)
	{
		if (!hasUrlAssignment || isStaticHttpUrl || nonStaticInputs.Count is 0)
			return;

		context.ReportDiagnostic(Diagnostic.Create(
			NonStaticVariantShapingInputRule,
			diagnosticLocation ?? context.Node.GetLocation(),
			string.Join(", ", nonStaticInputs)));
	}

	private static bool TryGetStaticInt32(ExpressionSyntax? expression, SemanticModel semanticModel, CancellationToken cancellationToken, out int value)
	{
		value = default;

		if (expression is null)
			return false;

		Optional<object?> constantValue = semanticModel.GetConstantValue(UnwrapExpression(expression, semanticModel, cancellationToken), cancellationToken);

		if (!constantValue.HasValue || constantValue.Value is null)
			return false;

		switch (constantValue.Value)
		{
			case int intValue:
				value = intValue;
				return true;
			case byte byteValue:
				value = byteValue;
				return true;
			case sbyte sbyteValue:
				value = sbyteValue;
				return true;
			case short shortValue:
				value = shortValue;
				return true;
			case ushort ushortValue:
				value = ushortValue;
				return true;
			case uint uintValue when uintValue <= int.MaxValue:
				value = (int)uintValue;
				return true;
			case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
				value = (int)longValue;
				return true;
			case ulong ulongValue when ulongValue <= int.MaxValue:
				value = (int)ulongValue;
				return true;
			default:
				return false;
		}
	}

	private static bool TryGetStaticString(ExpressionSyntax? expression, SemanticModel semanticModel, CancellationToken cancellationToken, out string? value)
	{
		value = null;

		if (expression is null)
			return false;

		Optional<object?> constantValue = semanticModel.GetConstantValue(UnwrapExpression(expression, semanticModel, cancellationToken), cancellationToken);

		if (!constantValue.HasValue)
			return false;

		value = constantValue.Value as string;
		return constantValue.Value is null || constantValue.Value is string;
	}

	private static bool IsStaticHttpUrl(string? url)
		=> url is not null &&
		   (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
			url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

	private sealed class DynamicImageVersioningState
	{
		private readonly ConcurrentBag<Diagnostic> _diagnostics = [];
		private readonly bool _buildPropertyEnabled;
		private int _registrationState;

		public DynamicImageVersioningState(bool buildPropertyEnabled)
		{
			_buildPropertyEnabled = buildPropertyEnabled;
		}

		public ImmutableArray<Diagnostic> Diagnostics => [.. _diagnostics];

		public bool IsEnabled
		{
			get
			{
				int registrationState = Volatile.Read(ref _registrationState);
				return registrationState is 0 ? _buildPropertyEnabled : registrationState is 3;
			}
		}

		public void AddDiagnostic(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

		public void MarkRegistration(bool explicitlyEnabled)
		{
			int flags = explicitlyEnabled ? 3 : 5;
			int current;
			int updated;

			do
			{
				current = Volatile.Read(ref _registrationState);
				updated = current | flags;
			}
			while (Interlocked.CompareExchange(ref _registrationState, updated, current) != current);
		}
	}

	private sealed class VariantDiscoveryUsageState
	{
		public Location? DiagnosticLocation { get; set; }

		public bool HasUrlAssignment { get; set; }

		public bool IsStaticHttpUrl { get; set; }

		public List<string> NonStaticInputs { get; } = [];
	}

	private static bool IsDynamicImageRegistrationMethod(
		IMethodSymbol methodSymbol,
		INamedTypeSymbol optionsType)
	{
		IMethodSymbol comparisonMethod = methodSymbol.ReducedFrom ?? methodSymbol;

		if (!string.Equals(comparisonMethod.Name, "AddUmbrellaWebUtilitiesDynamicImage", StringComparison.Ordinal) ||
			!SymbolEqualityComparer.Default.Equals(comparisonMethod.ContainingAssembly, optionsType.ContainingAssembly))
		{
			return false;
		}

		if (!string.Equals(comparisonMethod.ContainingNamespace.ToDisplayString(), "Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
			return false;

		IParameterSymbol? optionsBuilderParameter = comparisonMethod.Parameters.FirstOrDefault(x => string.Equals(x.Name, "optionsBuilder", StringComparison.Ordinal));

		if (optionsBuilderParameter?.Type is not INamedTypeSymbol delegateType)
			return false;

		if (delegateType.DelegateInvokeMethod is not IMethodSymbol delegateInvokeMethod)
			return false;

		return delegateInvokeMethod.Parameters.Length is 2 &&
			SymbolEqualityComparer.Default.Equals(delegateInvokeMethod.Parameters[1].Type, optionsType);
	}

	private static ArgumentSyntax? GetOptionsBuilderArgument(InvocationExpressionSyntax invocation, IMethodSymbol methodSymbol)
	{
		ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

		for (int i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			ArgumentSyntax argument = invocation.ArgumentList.Arguments[i];
			IParameterSymbol? parameter = null;

			if (argument.NameColon is not null)
			{
				string parameterName = argument.NameColon.Name.Identifier.ValueText;
				parameter = parameters.FirstOrDefault(x => string.Equals(x.Name, parameterName, StringComparison.Ordinal));
			}
			else if (i < parameters.Length)
			{
				parameter = parameters[i];
			}

			if (parameter is not null && string.Equals(parameter.Name, "optionsBuilder", StringComparison.Ordinal))
				return argument;
		}

		return null;
	}

	private static bool HasExplicitTrueFingerprintingAssignment(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		IPropertySymbol enableUrlFingerprintingProperty,
		CancellationToken cancellationToken)
	{
		IParameterSymbol? optionsParameter = GetOptionsParameter(expression, semanticModel, cancellationToken);

		if (optionsParameter is null)
			return false;

		AssignmentExpressionSyntax[] allAssignments =
		[
			.. expression.DescendantNodesAndSelf()
				.OfType<AssignmentExpressionSyntax>()
				.Where(x => IsFingerprintingAssignment(
					x,
					semanticModel,
					optionsParameter,
					enableUrlFingerprintingProperty,
					cancellationToken))
		];

		if (allAssignments.Length is 0)
			return false;

		AssignmentExpressionSyntax[] directAssignments = expression switch
		{
			ParenthesizedLambdaExpressionSyntax { Body: BlockSyntax body } => GetDirectFingerprintingAssignments(
				body,
				semanticModel,
				optionsParameter,
				enableUrlFingerprintingProperty,
				cancellationToken),
			ParenthesizedLambdaExpressionSyntax { Body: AssignmentExpressionSyntax assignment }
				when IsFingerprintingAssignment(assignment, semanticModel, optionsParameter, enableUrlFingerprintingProperty, cancellationToken)
				=> [assignment],
			SimpleLambdaExpressionSyntax { Body: BlockSyntax body } => GetDirectFingerprintingAssignments(
				body,
				semanticModel,
				optionsParameter,
				enableUrlFingerprintingProperty,
				cancellationToken),
			SimpleLambdaExpressionSyntax { Body: AssignmentExpressionSyntax assignment }
				when IsFingerprintingAssignment(assignment, semanticModel, optionsParameter, enableUrlFingerprintingProperty, cancellationToken)
				=> [assignment],
			AnonymousMethodExpressionSyntax { Block: { } body } => GetDirectFingerprintingAssignments(
				body,
				semanticModel,
				optionsParameter,
				enableUrlFingerprintingProperty,
				cancellationToken),
			_ => []
		};

		if (directAssignments.Length is 0 || directAssignments.Length != allAssignments.Length)
			return false;

		Optional<object?> finalValue = semanticModel.GetConstantValue(directAssignments[^1].Right, cancellationToken);
		return finalValue.HasValue && finalValue.Value is true;
	}

	private static AssignmentExpressionSyntax[] GetDirectFingerprintingAssignments(
		BlockSyntax body,
		SemanticModel semanticModel,
		IParameterSymbol optionsParameter,
		IPropertySymbol enableUrlFingerprintingProperty,
		CancellationToken cancellationToken)
		=>
		[
			.. body.Statements
				.OfType<ExpressionStatementSyntax>()
				.Select(x => x.Expression)
				.OfType<AssignmentExpressionSyntax>()
				.Where(x => IsFingerprintingAssignment(
					x,
					semanticModel,
					optionsParameter,
					enableUrlFingerprintingProperty,
					cancellationToken))
		];

	private static bool IsFingerprintingAssignment(
		AssignmentExpressionSyntax assignment,
		SemanticModel semanticModel,
		IParameterSymbol optionsParameter,
		IPropertySymbol enableUrlFingerprintingProperty,
		CancellationToken cancellationToken)
	{
		if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
			assignment.Left is not MemberAccessExpressionSyntax memberAccess ||
			semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol is not IPropertySymbol propertySymbol ||
			!SymbolEqualityComparer.Default.Equals(propertySymbol, enableUrlFingerprintingProperty))
		{
			return false;
		}

		return SymbolEqualityComparer.Default.Equals(
			semanticModel.GetSymbolInfo(memberAccess.Expression, cancellationToken).Symbol,
			optionsParameter);
	}

	private static IParameterSymbol? GetOptionsParameter(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		CancellationToken cancellationToken)
	{
		ParameterSyntax? parameter = expression switch
		{
			ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.ParameterList.Parameters.Count > 0
				=> parenthesizedLambda.ParameterList.Parameters[^1],
			SimpleLambdaExpressionSyntax simpleLambda
				=> simpleLambda.Parameter,
			AnonymousMethodExpressionSyntax anonymousMethod when anonymousMethod.ParameterList?.Parameters.Count > 0
				=> anonymousMethod.ParameterList.Parameters[^1],
			_ => null
		};

		return parameter is null
			? null
			: semanticModel.GetDeclaredSymbol(parameter, cancellationToken);
	}

	private sealed record DynamicImageActivationSymbols(
		INamedTypeSymbol OptionsType,
		IPropertySymbol EnableUrlFingerprintingProperty)
	{
		public static DynamicImageActivationSymbols? Create(Compilation compilation)
		{
			INamedTypeSymbol? optionsType = compilation.GetTypeByMetadataName(DynamicImageMiddlewareOptionsMetadataName);
			if (optionsType is null)
				return null;

			IPropertySymbol? enableUrlFingerprintingProperty = optionsType
				.GetMembers(EnableUrlFingerprintingPropertyName)
				.OfType<IPropertySymbol>()
				.FirstOrDefault(x =>
					!x.IsStatic &&
					x.Type.SpecialType is SpecialType.System_Boolean &&
					x.SetMethod is not null);

			return enableUrlFingerprintingProperty is null
				? null
				: new DynamicImageActivationSymbols(optionsType, enableUrlFingerprintingProperty);
		}
	}
}
