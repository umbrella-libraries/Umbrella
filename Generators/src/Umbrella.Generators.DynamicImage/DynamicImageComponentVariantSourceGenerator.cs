using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Umbrella.Generators.DynamicImage;

/// <summary>
/// Generates a catalog of <c>DynamicImageVariant</c> entries inferred from statically declared
/// <c>UmbrellaDynamicImage</c> Blazor component usages and MVC <c>DynamicImageTagHelper</c> /
/// <c>DynamicImagePictureSourceTagHelper</c> usages found in Razor-generated C# syntax trees.
/// </summary>
[Generator]
public sealed class DynamicImageComponentVariantSourceGenerator : IIncrementalGenerator
{
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

		IncrementalValueProvider<(Compilation Compilation, ImmutableArray<BlockSyntax> CandidateBlocks)> combined =
			context.CompilationProvider.Combine(candidateBlocks.Collect());

		context.RegisterSourceOutput(combined, static (spc, state) => Execute(spc, state.Compilation, state.CandidateBlocks));
	}

	private static void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<BlockSyntax> candidateBlocks)
	{
		if (compilation.GetTypeByMetadataName(DynamicImageVariantTypeName) is null ||
			compilation.GetTypeByMetadataName(DynamicResizeModeTypeName) is not INamedTypeSymbol resizeModeType ||
			compilation.GetTypeByMetadataName(DynamicImageFormatTypeName) is not INamedTypeSymbol imageFormatType)
		{
			return;
		}

		bool hasComponentType = compilation.GetTypeByMetadataName(DynamicImageComponentTypeName) is not null;
		bool hasTagHelperType = compilation.GetTypeByMetadataName(DynamicImageTagHelperTypeName) is not null ||
								compilation.GetTypeByMetadataName(DynamicImagePictureSourceTagHelperTypeName) is not null;

		if (!hasComponentType && !hasTagHelperType)
			return;

		HashSet<int> validResizeModes = GetEnumValues(resizeModeType);
		HashSet<int> validImageFormats = GetEnumValues(imageFormatType);
		HashSet<VariantEntry> variants = [];

		foreach (BlockSyntax block in candidateBlocks)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			SemanticModel semanticModel = compilation.GetSemanticModel(block.SyntaxTree);

			if (hasComponentType)
				CollectBlockVariants(block, semanticModel, validResizeModes, validImageFormats, variants, context.CancellationToken);

			if (hasTagHelperType)
				CollectTagHelperBlockVariants(block, semanticModel, validResizeModes, validImageFormats, variants, context.CancellationToken);
		}

		string source = GenerateSource(variants);
		context.AddSource("UmbrellaDynamicImageComponentVariantCatalog.g.cs", SourceText.From(source, Encoding.UTF8));
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
				parameters.WidthRequest = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultWidthRequest, cancellationToken);
				break;
			case "HeightRequest":
				parameters.HeightRequest = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultHeightRequest, cancellationToken);
				break;
			case "ResizeMode":
				parameters.ResizeMode = GetStaticEnumValueOrDefault(valueExpression, semanticModel, validResizeModes, DefaultResizeMode, cancellationToken);
				break;
			case "ImageFormat":
				parameters.ImageFormat = GetStaticEnumValueOrDefault(valueExpression, semanticModel, validImageFormats, DefaultImageFormat, cancellationToken);
				break;
			case "MaxPixelDensity":
				parameters.MaxPixelDensity = GetStaticPositiveIntOrDefault(valueExpression, semanticModel, DefaultMaxPixelDensity, cancellationToken);
				break;
			case "SizeWidths":
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
				parameters.WidthRequest = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, 0, cancellationToken);
				break;
			case "HeightRequest":
				parameters.HeightRequest = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, 0, cancellationToken);
				break;
			case "ResizeMode":
				parameters.ResizeMode = GetStaticEnumValueOrDefault(valueExpr, semanticModel, validResizeModes, DefaultResizeMode, cancellationToken);
				break;
			case "ImageFormat":
				parameters.ImageFormat = GetStaticEnumValueOrDefault(valueExpr, semanticModel, validImageFormats, DefaultImageFormat, cancellationToken);
				break;
			case "ImageMaxPixelDensity":
				parameters.MaxPixelDensity = GetStaticPositiveIntOrDefault(valueExpr, semanticModel, DefaultMaxPixelDensity, cancellationToken);
				break;
			case "SizeWidths" when supportsSizeWidths:
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

	private static HashSet<int> GetEnumValues(INamedTypeSymbol enumType)
		=> [.. enumType.GetMembers().OfType<IFieldSymbol>().Where(x => x.HasConstantValue && x.ConstantValue is not null).Select(x => Convert.ToInt32(x.ConstantValue, System.Globalization.CultureInfo.InvariantCulture))];

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

		if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { Name: "AddAttribute" } ||
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

	private static string GenerateSource(IEnumerable<VariantEntry> variants)
	{
		VariantEntry[] orderedVariants = [.. variants.OrderBy(x => x.Width).ThenBy(x => x.Height).ThenBy(x => x.ResizeMode).ThenBy(x => x.ImageFormat)];
		var builder = new StringBuilder();

		_ = builder.AppendLine("// <auto-generated />");
		_ = builder.AppendLine("#nullable enable");
		_ = builder.AppendLine();
		_ = builder.AppendLine("namespace Umbrella.Generated.DynamicImage");
		_ = builder.AppendLine("{");
		_ = builder.AppendLine("\t/// <summary>");
		_ = builder.AppendLine("\t/// Contains Dynamic Image variants inferred from statically declared UmbrellaDynamicImage component usages");
		_ = builder.AppendLine("\t/// and MVC DynamicImageTagHelper / DynamicImagePictureSourceTagHelper usages.");
		_ = builder.AppendLine("\t/// </summary>");
		_ = builder.AppendLine("\tpublic static class UmbrellaDynamicImageComponentVariantCatalog");
		_ = builder.AppendLine("\t{");
		_ = builder.AppendLine("\t\t/// <summary>");
		_ = builder.AppendLine("\t\t/// Gets all generated Dynamic Image variants.");
		_ = builder.AppendLine("\t\t/// </summary>");
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
		_ = builder.AppendLine("}");

		return builder.ToString();
	}

	private sealed class TagHelperVariantParameters
	{
		public int WidthRequest { get; set; }
		public int HeightRequest { get; set; }
		public int ResizeMode { get; set; } = DefaultResizeMode;
		public int ImageFormat { get; set; } = DefaultImageFormat;
		public int MaxPixelDensity { get; set; } = DefaultMaxPixelDensity;
		public HashSet<int>? SizeWidths { get; set; }
	}

	private sealed class ComponentVariantParameters
	{
		public string? StaticUrl { get; set; }

		public int WidthRequest { get; set; } = DefaultWidthRequest;

		public int HeightRequest { get; set; } = DefaultHeightRequest;

		public int ResizeMode { get; set; } = DefaultResizeMode;

		public int ImageFormat { get; set; } = DefaultImageFormat;

		public int MaxPixelDensity { get; set; } = DefaultMaxPixelDensity;

		public HashSet<int>? SizeWidths { get; set; }
	}

	private readonly record struct VariantEntry(int Width, int Height, int ResizeMode, int ImageFormat);
}
