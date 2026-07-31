using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Umbrella.DynamicImage.RazorAnalysis;

namespace Umbrella.Generators.DynamicImage;

/// <summary>
/// Generates named and aggregate catalogs of <c>DynamicImageVariant</c> entries inferred from
/// statically declared Dynamic Image usages in Razor source and manually authored C#.
/// </summary>
[Generator]
public sealed class DynamicImageComponentVariantSourceGenerator : IIncrementalGenerator
{
	private static readonly DiagnosticDescriptor _invalidCatalogConfigurationRule = new(
		id: "UWDI005",
		title: "Dynamic Image catalog configuration is invalid",
		messageFormat: "Dynamic Image catalog configuration is invalid: {0}",
		category: "DynamicImageGeneration",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	// Blazor component
	private const string DynamicImageComponentTypeName = "Umbrella.AspNetCore.Blazor.Components.DynamicImage.UmbrellaDynamicImage";

	// MVC tag helpers
	private const string DynamicImageTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper";
	private const string DynamicImagePictureSourceTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper";

	// Shared DynamicImage abstractions
	private const string DynamicImageVariantTypeName = "Umbrella.DynamicImage.Abstractions.DynamicImageVariant";
	private const string DynamicResizeModeTypeName = "Umbrella.DynamicImage.Abstractions.DynamicResizeMode";
	private const string DynamicImageFormatTypeName = "Umbrella.DynamicImage.Abstractions.DynamicImageFormat";

	// Blazor render-tree helpers
	private const string RuntimeHelpersTypeName = "Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers";

	private const int DefaultWidthRequest = 1;
	private const int DefaultHeightRequest = 1;
	private const int DefaultResizeMode = 4;
	private const int DefaultImageFormat = 2;
	private const int DefaultMaxPixelDensity = 3;

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Cheap syntax-only filter: only block nodes can contain component/tag-helper invocations.
		IncrementalValuesProvider<BlockSyntax> candidateBlocks = context.SyntaxProvider
			.CreateSyntaxProvider(
				predicate: static (node, _) => node is BlockSyntax,
				transform: static (ctx, _) => (BlockSyntax)ctx.Node);

		IncrementalValuesProvider<RazorFileInput> razorFiles = context.AdditionalTextsProvider
			.Where(static file => IsRazorFile(file.Path))
			.Combine(context.AnalyzerConfigOptionsProvider)
			.Select(static (state, cancellationToken) => CreateRazorFileInput(state.Left, state.Right, cancellationToken));

		IncrementalValueProvider<string> projectCatalogName = context.AnalyzerConfigOptionsProvider
			.Select(static (options, _) => GetProjectCatalogName(options));

		var combined = context.CompilationProvider
			.Combine(candidateBlocks.Collect())
			.Combine(razorFiles.Collect())
			.Combine(projectCatalogName);

		context.RegisterSourceOutput(combined, static (spc, state) =>
		{
			var compilationAndFiles = state.Left;
			var compilationAndBlocks = compilationAndFiles.Left;
			Execute(
				spc,
				compilationAndBlocks.Left,
				compilationAndBlocks.Right,
				compilationAndFiles.Right,
				state.Right);
		});
	}

	private static void Execute(
		SourceProductionContext context,
		Compilation compilation,
		ImmutableArray<BlockSyntax> candidateBlocks,
		ImmutableArray<RazorFileInput> razorFiles,
		string projectCatalogName)
	{
		if (compilation.GetTypeByMetadataName(DynamicImageVariantTypeName) is null ||
			compilation.GetTypeByMetadataName(DynamicResizeModeTypeName) is not INamedTypeSymbol resizeModeType ||
			compilation.GetTypeByMetadataName(DynamicImageFormatTypeName) is not INamedTypeSymbol imageFormatType)
		{
			return;
		}

		bool hasComponentType = compilation.GetTypeByMetadataName(DynamicImageComponentTypeName) is not null;
		bool hasImageTagHelperType = compilation.GetTypeByMetadataName(DynamicImageTagHelperTypeName) is not null;
		bool hasPictureSourceTagHelperType = compilation.GetTypeByMetadataName(DynamicImagePictureSourceTagHelperTypeName) is not null;
		bool hasTagHelperType = hasImageTagHelperType || hasPictureSourceTagHelperType;

		if (!hasComponentType && !hasTagHelperType)
			return;

		HashSet<int> validResizeModes = GetEnumValues(resizeModeType);
		HashSet<int> validImageFormats = GetEnumValues(imageFormatType);
		var variantsByCatalog = new Dictionary<string, HashSet<VariantEntry>>(StringComparer.OrdinalIgnoreCase);

		if (!TryValidateCatalogConfiguration(context, razorFiles, projectCatalogName, out ImmutableArray<RazorFileInput> uniqueRazorFiles))
			return;

		HashSet<VariantEntry> projectVariants = GetOrAddCatalog(variantsByCatalog, projectCatalogName);

		foreach (BlockSyntax block in candidateBlocks)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			if (IsRazorGeneratedSyntaxTree(block.SyntaxTree))
				continue;

			SemanticModel semanticModel = compilation.GetSemanticModel(block.SyntaxTree);

			if (hasComponentType)
				CollectBlockVariants(block, semanticModel, validResizeModes, validImageFormats, projectVariants, context.CancellationToken);

			if (hasTagHelperType)
				CollectTagHelperBlockVariants(block, semanticModel, validResizeModes, validImageFormats, projectVariants, context.CancellationToken);
		}

		DynamicImageRazorDocument[] documents =
		[
			.. uniqueRazorFiles.Select(x => new DynamicImageRazorDocument(x.Path, x.Text.ToString(), x.CatalogName))
		];

		foreach (RazorFileInput file in uniqueRazorFiles)
			_ = GetOrAddCatalog(variantsByCatalog, file.CatalogName);

		ImmutableArray<DynamicImageRazorUsage> usages = DynamicImageRazorSourceParser.Parse(
			documents,
			hasComponentType,
			hasImageTagHelperType,
			hasPictureSourceTagHelperType);
		var resizeModeValues = GetEnumValueMap(resizeModeType);
		var imageFormatValues = GetEnumValueMap(imageFormatType);

		foreach (DynamicImageRazorUsage usage in usages)
		{
			HashSet<VariantEntry> variants = GetOrAddCatalog(variantsByCatalog, usage.Document.CatalogName);
			AddRazorUsageVariants(usage, resizeModeValues, imageFormatValues, variants);
		}

		string source = GenerateSource(variantsByCatalog);
		context.AddSource("DynamicImageVariantCatalog.g.cs", SourceText.From(source, Encoding.UTF8));
	}

	private static void AddRazorUsageVariants(
		DynamicImageRazorUsage usage,
		Dictionary<string, int> validResizeModes,
		Dictionary<string, int> validImageFormats,
		ISet<VariantEntry> variants)
	{
		bool isTagHelper = usage.Kind is not DynamicImageRazorUsageKind.Component;
		bool supportsSizeWidths = usage.Kind is not DynamicImageRazorUsageKind.PictureSourceTagHelper;
		var attributes = new Dictionary<string, DynamicImageRazorAttribute>(StringComparer.OrdinalIgnoreCase);

		foreach (DynamicImageRazorAttribute attribute in usage.Attributes)
		{
			string normalizedName = NormalizeRazorAttributeName(attribute.Name, isTagHelper);

			if (normalizedName.Length > 0)
				attributes[normalizedName] = attribute;
		}

		foreach (KeyValuePair<string, DynamicImageRazorAttribute> entry in attributes)
		{
			if (IsVariantShapingAttribute(entry.Key, isTagHelper) &&
				!DynamicImageRazorSourceParser.IsDiscoverableValue(entry.Key, entry.Value.Value, isTagHelper))
			{
				return;
			}
		}

		if (attributes.TryGetValue(isTagHelper ? "Src" : "Url", out DynamicImageRazorAttribute? urlAttribute) &&
			DynamicImageRazorSourceParser.TryGetStaticString(urlAttribute.Value, out string url) &&
			IsStaticHttpUrl(url))
		{
			return;
		}

		int width = isTagHelper ? 0 : DefaultWidthRequest;
		int height = isTagHelper ? 0 : DefaultHeightRequest;
		int resizeMode = DefaultResizeMode;
		int imageFormat = DefaultImageFormat;
		int maxPixelDensity = DefaultMaxPixelDensity;
		HashSet<int>? sizeWidths = null;

		if (attributes.TryGetValue("WidthRequest", out DynamicImageRazorAttribute? widthAttribute))
			_ = DynamicImageRazorSourceParser.TryGetStaticPositiveInt(widthAttribute.Value, out width);

		if (attributes.TryGetValue("HeightRequest", out DynamicImageRazorAttribute? heightAttribute))
			_ = DynamicImageRazorSourceParser.TryGetStaticPositiveInt(heightAttribute.Value, out height);

		string densityName = isTagHelper ? "ImageMaxPixelDensity" : "MaxPixelDensity";
		if (attributes.TryGetValue(densityName, out DynamicImageRazorAttribute? densityAttribute))
			_ = DynamicImageRazorSourceParser.TryGetStaticPositiveInt(densityAttribute.Value, out maxPixelDensity);

		if (attributes.TryGetValue("ResizeMode", out DynamicImageRazorAttribute? resizeAttribute))
		{
			if (!DynamicImageRazorSourceParser.TryGetStaticEnumMember(
					resizeAttribute.Value,
					DynamicResizeModeTypeName,
					isTagHelper,
					out string resizeMember) ||
				!validResizeModes.TryGetValue(resizeMember, out int parsedResizeMode))
			{
				return;
			}

			resizeMode = parsedResizeMode;
		}

		if (attributes.TryGetValue("ImageFormat", out DynamicImageRazorAttribute? formatAttribute))
		{
			if (!DynamicImageRazorSourceParser.TryGetStaticEnumMember(
					formatAttribute.Value,
					DynamicImageFormatTypeName,
					isTagHelper,
					out string formatMember) ||
				!validImageFormats.TryGetValue(formatMember, out int parsedImageFormat))
			{
				return;
			}

			imageFormat = parsedImageFormat;
		}

		if (supportsSizeWidths &&
			attributes.TryGetValue("SizeWidths", out DynamicImageRazorAttribute? sizeWidthsAttribute) &&
			DynamicImageRazorSourceParser.TryGetStaticString(sizeWidthsAttribute.Value, out string sizeWidthsValue))
		{
			sizeWidths = ParseSizeWidths(sizeWidthsValue);
		}

		if (isTagHelper)
		{
			var parameters = new TagHelperVariantParameters
			{
				WidthRequest = width,
				HeightRequest = height,
				ResizeMode = resizeMode,
				ImageFormat = imageFormat,
				MaxPixelDensity = maxPixelDensity,
				SizeWidths = sizeWidths
			};
			AddTagHelperVariants(parameters, variants);
		}
		else
		{
			var parameters = new ComponentVariantParameters
			{
				WidthRequest = width,
				HeightRequest = height,
				ResizeMode = resizeMode,
				ImageFormat = imageFormat,
				MaxPixelDensity = maxPixelDensity,
				SizeWidths = sizeWidths
			};
			AddVariants(parameters, variants);
		}
	}

	private static bool TryValidateCatalogConfiguration(
		SourceProductionContext context,
		ImmutableArray<RazorFileInput> razorFiles,
		string projectCatalogName,
		out ImmutableArray<RazorFileInput> uniqueRazorFiles)
	{
		var errors = new List<string>();
		RazorFileInput[] normalizedFiles =
		[
			.. razorFiles.Select(x => string.IsNullOrWhiteSpace(x.CatalogName) && !x.IsExternalSource
				? x with { CatalogName = projectCatalogName }
				: x)
		];
		string[] allCatalogNames = normalizedFiles.Select(x => x.CatalogName).Append(projectCatalogName).ToArray();

		foreach (string name in allCatalogNames.Where(string.IsNullOrWhiteSpace).Distinct(StringComparer.Ordinal))
			errors.Add("catalog names cannot be empty");

		foreach (IGrouping<string, string> collision in allCatalogNames
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
			.Where(x => x.Distinct(StringComparer.Ordinal).Count() > 1))
		{
			errors.Add($"catalog names '{string.Join("', '", collision.Distinct(StringComparer.Ordinal))}' differ only by case");
		}

		foreach (string name in allCatalogNames.Where(x => !string.IsNullOrWhiteSpace(x) && SanitizeIdentifier(x).Length is 0).Distinct(StringComparer.OrdinalIgnoreCase))
			errors.Add($"catalog name '{name}' does not contain any valid identifier characters");

		var identifierCollisions = allCatalogNames
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.GroupBy(SanitizeIdentifier, StringComparer.OrdinalIgnoreCase)
			.Where(x => x.Select(y => y).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

		foreach (IGrouping<string, string> collision in identifierCollisions)
			errors.Add($"catalog names '{string.Join("', '", collision.Distinct(StringComparer.OrdinalIgnoreCase))}' produce the same generated identifier '{collision.Key}'");

		var filesByPath = normalizedFiles.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase);
		var unique = ImmutableArray.CreateBuilder<RazorFileInput>();

		foreach (IGrouping<string, RazorFileInput> group in filesByPath)
		{
			string[] owners = [.. group.Select(x => x.CatalogName).Distinct(StringComparer.OrdinalIgnoreCase)];

			if (owners.Length > 1)
			{
				errors.Add($"Razor file '{group.Key}' belongs to more than one catalog: {string.Join(", ", owners)}");
				continue;
			}

			unique.Add(group.First());
		}

		foreach (string error in errors.Distinct(StringComparer.Ordinal))
			context.ReportDiagnostic(Diagnostic.Create(_invalidCatalogConfigurationRule, Location.None, error));

		uniqueRazorFiles = unique.ToImmutable();
		return errors.Count is 0;
	}

	private static HashSet<VariantEntry> GetOrAddCatalog(IDictionary<string, HashSet<VariantEntry>> catalogs, string catalogName)
	{
		if (!catalogs.TryGetValue(catalogName, out HashSet<VariantEntry>? variants))
		{
			variants = [];
			catalogs.Add(catalogName, variants);
		}

		return variants;
	}

	private static void CollectBlockVariants(
		BlockSyntax block,
		SemanticModel semanticModel,
		ISet<int> validResizeModes,
		ISet<int> validImageFormats,
		ISet<VariantEntry> variants,
		CancellationToken cancellationToken)
	{
		SyntaxList<StatementSyntax> statements = block.Statements;

		for (int i = 0; i < statements.Count; i++)
		{
			if (!TryGetInvocationStatement(statements[i], out InvocationExpressionSyntax? invocation) ||
				invocation is null ||
				!TryGetInvocationReceiverText(invocation, out string? receiverText) ||
				!IsOpenComponentInvocation(semanticModel, invocation, cancellationToken))
			{
				continue;
			}

			int nestingDepth = 1;
			var parameters = new ComponentVariantParameters();

			for (int j = i + 1; j < statements.Count; j++)
			{
				if (!TryGetInvocationStatement(statements[j], out InvocationExpressionSyntax? nextInvocation) ||
					nextInvocation is null ||
					!TryGetInvocationReceiverText(nextInvocation, out string? nextReceiverText) ||
					!string.Equals(receiverText, nextReceiverText, StringComparison.Ordinal))
				{
					continue;
				}

				if (IsOpenComponentInvocation(semanticModel, nextInvocation, cancellationToken))
				{
					nestingDepth++;
					continue;
				}

				if (IsCloseComponentInvocation(semanticModel, nextInvocation, cancellationToken))
				{
					nestingDepth--;

					if (nestingDepth is 0)
					{
						AddVariants(parameters, variants);
						i = j;
						break;
					}

					continue;
				}

				if (nestingDepth is not 1 ||
					!TryGetRenderTreeAttribute(nextInvocation, semanticModel, cancellationToken, out string attributeName, out ExpressionSyntax? valueExpression))
				{
					continue;
				}

				ApplyAttribute(parameters, semanticModel, attributeName, valueExpression, validResizeModes, validImageFormats, cancellationToken);
			}
		}
	}

	private static void ApplyAttribute(
		ComponentVariantParameters parameters,
		SemanticModel semanticModel,
		string attributeName,
		ExpressionSyntax? valueExpression,
		ISet<int> validResizeModes,
		ISet<int> validImageFormats,
		CancellationToken cancellationToken)
	{
		switch (attributeName)
		{
			case "Url":
				parameters.StaticUrl = TryGetStaticString(valueExpression, semanticModel, cancellationToken, out string? url) ? url : null;
				break;
			case "WidthRequest":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.WidthRequest = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultWidthRequest, cancellationToken);
				break;
			case "HeightRequest":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.HeightRequest = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultHeightRequest, cancellationToken);
				break;
			case "ResizeMode":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.ResizeMode = GetStaticEnumValueOrDefault(valueExpression, semanticModel, validResizeModes, DefaultResizeMode, cancellationToken);
				break;
			case "ImageFormat":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.ImageFormat = GetStaticEnumValueOrDefault(valueExpression, semanticModel, validImageFormats, DefaultImageFormat, cancellationToken);
				break;
			case "MaxPixelDensity":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.MaxPixelDensity = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultMaxPixelDensity, cancellationToken);
				break;
			case "SizeWidths":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpression, semanticModel, cancellationToken);
				parameters.SizeWidths = TryGetStaticString(valueExpression, semanticModel, cancellationToken, out string? sizeWidths)
					? ParseSizeWidths(sizeWidths)
					: null;
				break;
		}
	}

	// ─── Tag-helper discovery ─────────────────────────────────────────────────

	/// <summary>
	/// Scans a block for Razor-generated tag-helper setup patterns:
	/// <c>field = CreateTagHelper&lt;DynamicImageTagHelper&gt;()</c> followed by
	/// property assignments of the form <c>field.PropertyName = value</c>.
	/// </summary>
	private static void CollectTagHelperBlockVariants(
		BlockSyntax block,
		SemanticModel semanticModel,
		ISet<int> validResizeModes,
		ISet<int> validImageFormats,
		ISet<VariantEntry> variants,
		CancellationToken cancellationToken)
	{
		SyntaxList<StatementSyntax> statements = block.Statements;

		for (int i = 0; i < statements.Count; i++)
		{
			if (!TryGetCreateTagHelperAssignment(statements[i], semanticModel, cancellationToken, out string? fieldName, out bool supportsSizeWidths))
				continue;

			var parameters = new TagHelperVariantParameters();

			for (int j = i + 1; j < statements.Count; j++)
			{
				StatementSyntax stmt = statements[j];

				// Stop when the same field is reassigned by another CreateTagHelper call.
				if (TryGetCreateTagHelperAssignment(stmt, semanticModel, cancellationToken, out string? nextField, out _) &&
					string.Equals(fieldName, nextField, StringComparison.Ordinal))
				{
					break;
				}

				if (TryGetTagHelperPropertyAssignment(stmt, fieldName!, out string? propertyName, out ExpressionSyntax? valueExpr))
					ApplyTagHelperProperty(parameters, semanticModel, propertyName!, valueExpr, validResizeModes, validImageFormats, supportsSizeWidths, cancellationToken);
			}

			AddTagHelperVariants(parameters, variants);
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="statement"/> matches
	/// <c>field = CreateTagHelper&lt;T&gt;()</c> where T is a known dynamic-image tag helper.
	/// </summary>
	private static bool TryGetCreateTagHelperAssignment(
		StatementSyntax statement,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string? fieldName,
		out bool supportsSizeWidths)
	{
		fieldName = null;
		supportsSizeWidths = false;

		if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
			return false;

		if (assignment.Right is not InvocationExpressionSyntax invocation)
			return false;

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol ||
			!string.Equals(methodSymbol.Name, "CreateTagHelper", StringComparison.Ordinal) ||
			methodSymbol.TypeArguments.Length is not 1)
		{
			return false;
		}

		string typeFullName = methodSymbol.TypeArguments[0].ToDisplayString();

		if (string.Equals(typeFullName, DynamicImageTagHelperTypeName, StringComparison.Ordinal))
			supportsSizeWidths = true;
		else if (!string.Equals(typeFullName, DynamicImagePictureSourceTagHelperTypeName, StringComparison.Ordinal))
			return false;

		fieldName = assignment.Left.ToString();
		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="statement"/> is an assignment of the form
	/// <c>fieldName.PropertyName = value</c>.
	/// </summary>
	private static bool TryGetTagHelperPropertyAssignment(
		StatementSyntax statement,
		string fieldName,
		out string? propertyName,
		out ExpressionSyntax? valueExpr)
	{
		propertyName = null;
		valueExpr = null;

		if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment })
			return false;

		if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
			return false;

		if (!string.Equals(memberAccess.Expression.ToString(), fieldName, StringComparison.Ordinal))
			return false;

		propertyName = memberAccess.Name.Identifier.Text;
		valueExpr = assignment.Right;
		return true;
	}

	private static void ApplyTagHelperProperty(
		TagHelperVariantParameters parameters,
		SemanticModel semanticModel,
		string propertyName,
		ExpressionSyntax? valueExpr,
		ISet<int> validResizeModes,
		ISet<int> validImageFormats,
		bool supportsSizeWidths,
		CancellationToken cancellationToken)
	{
		switch (propertyName)
		{
			case "WidthRequest":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.WidthRequest = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, 0, cancellationToken);
				break;
			case "HeightRequest":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.HeightRequest = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, 0, cancellationToken);
				break;
			case "ResizeMode":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.ResizeMode = GetStaticEnumValueOrDefault(valueExpr, semanticModel, validResizeModes, DefaultResizeMode, cancellationToken);
				break;
			case "ImageFormat":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.ImageFormat = GetStaticEnumValueOrDefault(valueExpr, semanticModel, validImageFormats, DefaultImageFormat, cancellationToken);
				break;
			case "ImageMaxPixelDensity":
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.MaxPixelDensity = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, DefaultMaxPixelDensity, cancellationToken);
				break;
			case "SizeWidths" when supportsSizeWidths:
				parameters.HasUndiscoverableShapingInput |= !HasStaticValue(valueExpr, semanticModel, cancellationToken);
				parameters.SizeWidths = TryGetStaticString(valueExpr, semanticModel, cancellationToken, out string? sw)
					? ParseSizeWidths(sw)
					: null;
				break;
		}
	}

	/// <summary>
	/// Emits variants for a tag-helper usage, mirroring the runtime behaviour of
	/// <c>DynamicImageTagHelper</c> and <c>DynamicImagePictureSourceTagHelper</c>:
	/// <list type="bullet">
	///   <item>Without <c>SizeWidths</c>: one variant per pixel density from 1 up to <c>ImageMaxPixelDensity</c>.</item>
	///   <item>With <c>SizeWidths</c>: base variant plus one variant per (sizeWidth × pixelDensity)
	///   combination, up to <c>ImageMaxPixelDensity</c>.</item>
	/// </list>
	/// Skips emission when both <c>WidthRequest</c> and <c>HeightRequest</c> are not statically
	/// known (i.e. both are 0).
	/// </summary>
	private static void AddTagHelperVariants(TagHelperVariantParameters parameters, ISet<VariantEntry> variants)
	{
		if (parameters.HasUndiscoverableShapingInput)
			return;

		int widthRequest = parameters.WidthRequest;
		int heightRequest = parameters.HeightRequest;
		int resizeMode = parameters.ResizeMode;
		int imageFormat = parameters.ImageFormat;
		int maxPixelDensity = parameters.MaxPixelDensity;

		if (widthRequest <= 0 && heightRequest <= 0)
			return;

		if (parameters.SizeWidths is { Count: > 0 } sizeWidths)
		{
			_ = variants.Add(new VariantEntry(widthRequest, heightRequest, resizeMode, imageFormat));

			double aspectRatio = widthRequest > 0 && heightRequest > 0
				? widthRequest / (double)heightRequest
				: 1.0;

			foreach (int sizeWidth in sizeWidths.OrderBy(x => x))
			{
				foreach (int density in Enumerable.Range(1, maxPixelDensity))
				{
					int width = sizeWidth * density;
					int height = (int)Math.Ceiling(width / aspectRatio);
					_ = variants.Add(new VariantEntry(width, height, resizeMode, imageFormat));
				}
			}
		}
		else
		{
			int w = widthRequest > 0 ? widthRequest : heightRequest;
			int h = heightRequest > 0 ? heightRequest : widthRequest;

			foreach (int density in Enumerable.Range(1, maxPixelDensity))
				_ = variants.Add(new VariantEntry(w * density, h * density, resizeMode, imageFormat));
		}
	}

	// ─── Component variant expansion ──────────────────────────────────────────

	private static void AddVariants(ComponentVariantParameters parameters, ISet<VariantEntry> variants)
	{
		if (parameters.HasUndiscoverableShapingInput)
			return;

		if (IsStaticHttpUrl(parameters.StaticUrl))
			return;

		int widthRequest = parameters.WidthRequest;
		int heightRequest = parameters.HeightRequest;
		int resizeMode = parameters.ResizeMode;
		int imageFormat = parameters.ImageFormat;
		int maxPixelDensity = parameters.MaxPixelDensity;

		if (parameters.SizeWidths is { Count: > 0 } sizeWidths)
		{
			_ = variants.Add(new VariantEntry(widthRequest, heightRequest, resizeMode, imageFormat));

			double aspectRatio = widthRequest / (double)heightRequest;

			foreach (int sizeWidth in sizeWidths.OrderBy(x => x))
			{
				foreach (int density in Enumerable.Range(1, maxPixelDensity))
				{
					int width = sizeWidth * density;
					int height = (int)Math.Ceiling(width / aspectRatio);
					_ = variants.Add(new VariantEntry(width, height, resizeMode, imageFormat));
				}
			}

			return;
		}

		foreach (int density in Enumerable.Range(1, maxPixelDensity))
			_ = variants.Add(new VariantEntry(widthRequest * density, heightRequest * density, resizeMode, imageFormat));
	}

	private static HashSet<int>? ParseSizeWidths(string? sizeWidths)
	{
		if (string.IsNullOrWhiteSpace(sizeWidths))
			return null;

		HashSet<int> parsed = [];

		foreach (string item in sizeWidths!.Split([','], StringSplitOptions.RemoveEmptyEntries))
		{
			if (int.TryParse(item, out int value) && value > 0)
				_ = parsed.Add(value);
		}

		return parsed.Count > 0 ? parsed : null;
	}

	private static bool IsStaticHttpUrl(string? url)
		=> url is not null &&
		   (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
			url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

	private static int GetStaticPositiveIntOrDefault(ExpressionSyntax? expression, SemanticModel semanticModel, int defaultValue, CancellationToken cancellationToken)
	{
		return TryGetStaticInt32(expression, semanticModel, cancellationToken, out int value) && value >= 1
			? value
			: defaultValue;
	}

	private static int GetStaticEnumValueOrDefault(
		ExpressionSyntax? expression,
		SemanticModel semanticModel,
		ISet<int> validValues,
		int defaultValue,
		CancellationToken cancellationToken)
	{
		return TryGetStaticInt32(expression, semanticModel, cancellationToken, out int value) && validValues.Contains(value)
			? value
			: defaultValue;
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

	private static bool HasStaticValue(ExpressionSyntax? expression, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (expression is null)
			return false;

		return semanticModel.GetConstantValue(UnwrapExpression(expression, semanticModel, cancellationToken), cancellationToken).HasValue;
	}

	private static HashSet<int> GetEnumValues(INamedTypeSymbol enumType)
		=> [.. enumType.GetMembers().OfType<IFieldSymbol>().Where(x => x.HasConstantValue && x.ConstantValue is not null).Select(x => Convert.ToInt32(x.ConstantValue, System.Globalization.CultureInfo.InvariantCulture))];

	private static Dictionary<string, int> GetEnumValueMap(INamedTypeSymbol enumType)
		=> enumType.GetMembers()
			.OfType<IFieldSymbol>()
			.Where(x => x.HasConstantValue && x.ConstantValue is not null)
			.ToDictionary(
				x => x.Name,
				x => Convert.ToInt32(x.ConstantValue, System.Globalization.CultureInfo.InvariantCulture),
				StringComparer.Ordinal);

	private static bool TryGetInvocationStatement(StatementSyntax statement, out InvocationExpressionSyntax? invocation)
	{
		if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax expression })
		{
			invocation = expression;
			return true;
		}

		invocation = null;
		return false;
	}

	private static bool TryGetInvocationReceiverText(InvocationExpressionSyntax invocation, out string? receiverText)
	{
		receiverText = null;

		if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
			return false;

		receiverText = memberAccess.Expression.ToString();
		return true;
	}

	private static bool IsOpenComponentInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
	{
		return semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol methodSymbol &&
			   string.Equals(methodSymbol.Name, "OpenComponent", StringComparison.Ordinal) &&
			   methodSymbol.TypeArguments.Length is 1 &&
			   string.Equals(methodSymbol.TypeArguments[0].ToDisplayString(), DynamicImageComponentTypeName, StringComparison.Ordinal);
	}

	private static bool IsCloseComponentInvocation(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
		=> semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: "CloseComponent" };

	private static bool TryGetRenderTreeAttribute(
		InvocationExpressionSyntax invocation,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out string attributeName,
		out ExpressionSyntax? valueExpression)
	{
		attributeName = string.Empty;
		valueExpression = null;

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
		return true;
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
		return invocation.ArgumentList.Arguments.Count > 0 &&
			   semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol methodSymbol &&
			   string.Equals(methodSymbol.Name, "TypeCheck", StringComparison.Ordinal) &&
			   string.Equals(methodSymbol.ContainingType.ToDisplayString(), RuntimeHelpersTypeName, StringComparison.Ordinal);
	}

	private static string GenerateSource(IReadOnlyDictionary<string, HashSet<VariantEntry>> variantsByCatalog)
	{
		var builder = new StringBuilder();

		_ = builder.AppendLine("// <auto-generated />");
		_ = builder.AppendLine("#nullable enable");
		_ = builder.AppendLine();
		_ = builder.AppendLine("namespace Umbrella.Generated.DynamicImage");
		_ = builder.AppendLine("{");

		foreach (KeyValuePair<string, HashSet<VariantEntry>> catalog in variantsByCatalog.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
			AppendCatalog(builder, SanitizeIdentifier(catalog.Key) + "DynamicImageVariantCatalog", catalog.Value);

		HashSet<VariantEntry> aggregate = [.. variantsByCatalog.Values.SelectMany(x => x)];
		AppendCatalog(builder, "DynamicImageVariantCatalog", aggregate);
		_ = builder.AppendLine("}");

		return builder.ToString();
	}

	private static void AppendCatalog(StringBuilder builder, string typeName, IEnumerable<VariantEntry> variants)
	{
		VariantEntry[] orderedVariants = [.. variants.OrderBy(x => x.Width).ThenBy(x => x.Height).ThenBy(x => x.ResizeMode).ThenBy(x => x.ImageFormat)];
		_ = builder.AppendLine("\t/// <summary>Contains statically discovered Dynamic Image variants.</summary>");
		_ = builder.AppendLine($"\tpublic static class {typeName}");
		_ = builder.AppendLine("\t{");
		_ = builder.Append("\t\tpublic static readonly global::System.Collections.Generic.IReadOnlyList<global::Umbrella.DynamicImage.Abstractions.DynamicImageVariant> All = ");

		if (orderedVariants.Length is 0)
		{
			_ = builder.AppendLine("global::System.Array.Empty<global::Umbrella.DynamicImage.Abstractions.DynamicImageVariant>();");
		}
		else
		{
			_ = builder.AppendLine("new global::Umbrella.DynamicImage.Abstractions.DynamicImageVariant[]");
			_ = builder.AppendLine("\t\t{");

			foreach (VariantEntry variant in orderedVariants)
			{
				_ = builder.AppendLine(
					$"\t\t\tnew global::Umbrella.DynamicImage.Abstractions.DynamicImageVariant({variant.Width}, {variant.Height}, (global::Umbrella.DynamicImage.Abstractions.DynamicResizeMode){variant.ResizeMode}, (global::Umbrella.DynamicImage.Abstractions.DynamicImageFormat){variant.ImageFormat}),");
			}

			_ = builder.AppendLine("\t\t};");
		}

		_ = builder.AppendLine("\t}");
	}

	private static string NormalizeRazorAttributeName(string name, bool isTagHelper)
	{
		if (!isTagHelper)
			return name;

		return name.ToLowerInvariant() switch
		{
			"src" => "Src",
			"width-request" => "WidthRequest",
			"height-request" => "HeightRequest",
			"resize-mode" => "ResizeMode",
			"image-format" => "ImageFormat",
			"image-density" => "ImageMaxPixelDensity",
			"size-widths" => "SizeWidths",
			_ => string.Empty
		};
	}

	private static bool IsVariantShapingAttribute(string name, bool isTagHelper)
		=> name is "WidthRequest" or "HeightRequest" or "ResizeMode" or "ImageFormat" or "SizeWidths" ||
		   (!isTagHelper && name is "MaxPixelDensity") ||
		   (isTagHelper && name is "ImageMaxPixelDensity");

	private static bool IsRazorFile(string path)
	{
		if (path.EndsWith(".umbrella-dynamic-image", StringComparison.OrdinalIgnoreCase))
			path = path.Substring(0, path.Length - ".umbrella-dynamic-image".Length);

		string extension = Path.GetExtension(path);
		return string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsRazorGeneratedSyntaxTree(SyntaxTree syntaxTree)
	{
		string path = syntaxTree.FilePath;
		return path.EndsWith(".razor.g.cs", StringComparison.OrdinalIgnoreCase) ||
			   path.EndsWith(".cshtml.g.cs", StringComparison.OrdinalIgnoreCase) ||
			   path.IndexOf("_razor.g.cs", StringComparison.OrdinalIgnoreCase) >= 0 ||
			   path.IndexOf("_cshtml.g.cs", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static RazorFileInput CreateRazorFileInput(
		AdditionalText file,
		AnalyzerConfigOptionsProvider optionsProvider,
		CancellationToken cancellationToken)
	{
		AnalyzerConfigOptions options = optionsProvider.GetOptions(file);
		_ = options.TryGetValue("build_metadata.AdditionalFiles.UmbrellaDynamicImageCatalogName", out string? catalogName);
		bool isExternalSource =
			options.TryGetValue("build_metadata.AdditionalFiles.UmbrellaDynamicImageExternalSource", out string? externalSource) &&
			bool.TryParse(externalSource, out bool parsedExternalSource) &&
			parsedExternalSource;
		_ = options.TryGetValue("build_metadata.AdditionalFiles.UmbrellaDynamicImageOriginalSourcePath", out string? originalSourcePath);
		SourceText text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty, Encoding.UTF8);
		return new RazorFileInput(
			string.IsNullOrWhiteSpace(originalSourcePath) ? file.Path : originalSourcePath!,
			text,
			catalogName ?? string.Empty,
			isExternalSource);
	}

	private static string GetProjectCatalogName(AnalyzerConfigOptionsProvider optionsProvider)
	{
		if (optionsProvider.GlobalOptions.TryGetValue("build_property.UmbrellaDynamicImageCatalogName", out string? configuredName) &&
			!string.IsNullOrWhiteSpace(configuredName))
		{
			return configuredName;
		}

		if (optionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectName", out string? projectName) &&
			!string.IsNullOrWhiteSpace(projectName))
		{
			return projectName;
		}

		return "Project";
	}

	private static string SanitizeIdentifier(string value)
	{
		var builder = new StringBuilder(value.Length + 1);

		foreach (char character in value)
		{
			if (char.IsLetterOrDigit(character) || character is '_')
				_ = builder.Append(character);
		}

		if (builder.Length is 0)
			return string.Empty;

		if (!char.IsLetter(builder[0]) && builder[0] is not '_')
			_ = builder.Insert(0, '_');

		return builder.ToString();
	}

	private sealed class TagHelperVariantParameters
	{
		public bool HasUndiscoverableShapingInput { get; set; }
		public int WidthRequest { get; set; }
		public int HeightRequest { get; set; }
		public int ResizeMode { get; set; } = DefaultResizeMode;
		public int ImageFormat { get; set; } = DefaultImageFormat;
		public int MaxPixelDensity { get; set; } = DefaultMaxPixelDensity;
		public HashSet<int>? SizeWidths { get; set; }
	}

	private sealed class ComponentVariantParameters
	{
		public bool HasUndiscoverableShapingInput { get; set; }
		public string? StaticUrl { get; set; }

		public int WidthRequest { get; set; } = DefaultWidthRequest;

		public int HeightRequest { get; set; } = DefaultHeightRequest;

		public int ResizeMode { get; set; } = DefaultResizeMode;

		public int ImageFormat { get; set; } = DefaultImageFormat;

		public int MaxPixelDensity { get; set; } = DefaultMaxPixelDensity;

		public HashSet<int>? SizeWidths { get; set; }
	}

	private readonly record struct VariantEntry(int Width, int Height, int ResizeMode, int ImageFormat);

	private readonly record struct RazorFileInput(string Path, SourceText Text, string CatalogName, bool IsExternalSource);
}
