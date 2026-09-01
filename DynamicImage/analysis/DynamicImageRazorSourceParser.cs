using System.Collections.Immutable;
using System.Text;

namespace Umbrella.DynamicImage.RazorAnalysis;

internal enum DynamicImageRazorUsageKind
{
	Component,
	FileImagePreviewUploadComponent,
	ImageTagHelper,
	PictureSourceTagHelper,
	SourceComponent
}

internal sealed class DynamicImageRazorDocument
{
	public string Path { get; }
	public string Text { get; }
	public string CatalogName { get; }

	public DynamicImageRazorDocument(string path, string text, string catalogName)
	{
		Path = path;
		Text = text;
		CatalogName = catalogName;
	}
}

internal sealed class DynamicImageRazorAttribute
{
	public string Name { get; }
	public string Value { get; }
	public int NameStart { get; }
	public int NameLength { get; }

	public DynamicImageRazorAttribute(string name, string value, int nameStart, int nameLength)
	{
		Name = name;
		Value = value;
		NameStart = nameStart;
		NameLength = nameLength;
	}
}

internal sealed class DynamicImageRazorUsage
{
	public DynamicImageRazorDocument Document { get; }
	public DynamicImageRazorUsageKind Kind { get; }
	public ImmutableArray<DynamicImageRazorAttribute> Attributes { get; }
	public ImmutableHashSet<string> StaticUsingTypeNames { get; }

	/// <summary>
	/// The enclosing image usage when this usage is a nested source, otherwise <see langword="null"/>. A nested source inherits any
	/// attribute it does not declare itself, so the variant it resolves to depends on its parent.
	/// </summary>
	public DynamicImageRazorUsage? Parent { get; }

	public DynamicImageRazorUsage(
		DynamicImageRazorDocument document,
		DynamicImageRazorUsageKind kind,
		ImmutableArray<DynamicImageRazorAttribute> attributes,
		ImmutableHashSet<string> staticUsingTypeNames,
		DynamicImageRazorUsage? parent = null)
	{
		Document = document;
		Kind = kind;
		Attributes = attributes;
		StaticUsingTypeNames = staticUsingTypeNames;
		Parent = parent;
	}
}

internal static class DynamicImageRazorSourceParser
{
	private const string DynamicImageComponentNamespace = "Umbrella.AspNetCore.Blazor.Components.DynamicImage";
	private const string FullyQualifiedDynamicImageComponentName = DynamicImageComponentNamespace + ".UmbrellaDynamicImage";
	private const string FullyQualifiedDynamicImageSourceComponentName = DynamicImageComponentNamespace + ".UmbrellaDynamicImageSource";
	private const string FileImagePreviewUploadComponentNamespace = "Umbrella.AspNetCore.Blazor.Components.FileImagePreviewUpload";
	private const string FullyQualifiedFileImagePreviewUploadComponentName = FileImagePreviewUploadComponentNamespace + ".UmbrellaFileImagePreviewUpload";
	private const string TagHelperAssemblyName = "Umbrella.AspNetCore.WebUtilities.DynamicImage";
	private const string DynamicImageTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper";
	private const string DynamicImagePictureSourceTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper";
	private const string DynamicResizeModeTypeName = "Umbrella.DynamicImage.Abstractions.DynamicResizeMode";
	private const string DynamicImageFormatTypeName = "Umbrella.DynamicImage.Abstractions.DynamicImageFormat";
	private const string PreparedExternalSourceSuffix = ".umbrella-dynamic-image";

	public static ImmutableArray<DynamicImageRazorUsage> Parse(
		IEnumerable<DynamicImageRazorDocument> documents,
		bool hasDynamicImageComponentType,
		bool hasFileImagePreviewUploadComponentType,
		bool hasImageTagHelperType,
		bool hasPictureSourceTagHelperType,
		bool hasDynamicImageSourceComponentType)
	{
		DynamicImageRazorDocument[] allDocuments = [.. documents];
		DynamicImageRazorDocument[] imports = [.. allDocuments.Where(IsImportsDocument)];
		var result = ImmutableArray.CreateBuilder<DynamicImageRazorUsage>();

		foreach (DynamicImageRazorDocument document in allDocuments.Where(x => !IsImportsDocument(x)))
		{
			string extension = GetRazorExtension(document.Path);
			bool isComponentDocument = string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase);
			bool isViewDocument = string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase);

			if (!isComponentDocument && !isViewDocument)
				continue;

			string effectiveDirectives = BuildEffectiveDirectives(document, imports);
			ImmutableHashSet<string> staticUsingTypeNames = GetStaticUsingTypeNames(effectiveDirectives);
			bool dynamicImageComponentIsActive = hasDynamicImageComponentType &&
				(isComponentDocument && ContainsUsing(effectiveDirectives, DynamicImageComponentNamespace));
			// The source component shares the namespace of the image component, so a single using directive activates both.
			bool dynamicImageSourceComponentIsActive = hasDynamicImageSourceComponentType &&
				(isComponentDocument && ContainsUsing(effectiveDirectives, DynamicImageComponentNamespace));
			bool fileImagePreviewUploadComponentIsActive = hasFileImagePreviewUploadComponentType &&
				(isComponentDocument && ContainsUsing(effectiveDirectives, FileImagePreviewUploadComponentNamespace));
			(bool imageTagHelperIsActive, bool pictureSourceTagHelperIsActive) = isViewDocument
				? GetActiveTagHelpers(effectiveDirectives)
				: (false, false);

			ParseDocument(
				document,
				hasDynamicImageComponentType,
				dynamicImageComponentIsActive,
				hasFileImagePreviewUploadComponentType,
				fileImagePreviewUploadComponentIsActive,
				hasImageTagHelperType && imageTagHelperIsActive,
				hasPictureSourceTagHelperType && pictureSourceTagHelperIsActive,
				hasDynamicImageSourceComponentType,
				dynamicImageSourceComponentIsActive,
				staticUsingTypeNames,
				result);
		}

		return result.ToImmutable();
	}

	public static bool TryGetStaticPositiveInt(string value, out int result)
	{
		string normalized = NormalizeAttributeValue(value);
		return int.TryParse(normalized, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result) &&
			result > 0;
	}

	public static bool TryGetStaticString(string value, out string result)
	{
		if (ContainsRazorTransition(value))
		{
			result = string.Empty;
			return false;
		}

		string normalized = NormalizeAttributeValue(value);
		result = normalized;
		return true;
	}

	public static bool TryGetStaticBoolean(string value, out bool result)
	{
		string normalized = NormalizeAttributeValue(value);

		if (normalized.Length is 0)
		{
			result = true;
			return true;
		}

		return bool.TryParse(normalized, out result);
	}

	public static bool TryGetStaticEnumMember(
		string value,
		string expectedEnumTypeName,
		bool allowUnqualifiedMember,
		out string memberName)
	{
		string normalized = value.Trim();
		bool hasRazorTransition = false;

		if (normalized.StartsWith("@(", StringComparison.Ordinal) &&
			normalized.EndsWith(")", StringComparison.Ordinal) &&
			normalized.Length > 3)
		{
			hasRazorTransition = true;
			normalized = normalized.Substring(2, normalized.Length - 3).Trim();
		}
		else if (normalized.StartsWith("@", StringComparison.Ordinal))
		{
			hasRazorTransition = true;
			normalized = normalized.Substring(1).Trim();
		}

		if (normalized.StartsWith("(", StringComparison.Ordinal) && normalized.EndsWith(")", StringComparison.Ordinal))
			normalized = normalized.Substring(1, normalized.Length - 2).Trim();

		if (ContainsRazorTransition(normalized))
		{
			memberName = string.Empty;
			return false;
		}

		if (normalized.StartsWith("global::", StringComparison.Ordinal))
			normalized = normalized.Substring(8);

		string[] parts = normalized.Split('.');
		string expectedSimpleTypeName = expectedEnumTypeName.Substring(expectedEnumTypeName.LastIndexOf('.') + 1);

		if (parts.Length is 1)
		{
			memberName = normalized;
			return allowUnqualifiedMember &&
				!hasRazorTransition &&
				IsIdentifier(memberName);
		}

		memberName = parts[parts.Length - 1];
		string qualifier = string.Join(".", parts.Take(parts.Length - 1));
		return parts.Length > 1 &&
			(string.Equals(qualifier, expectedSimpleTypeName, StringComparison.Ordinal) ||
			 string.Equals(qualifier, expectedEnumTypeName, StringComparison.Ordinal)) &&
			parts.All(IsIdentifier);
	}

	public static bool IsDiscoverableValue(DynamicImageRazorUsage usage, string attributeName, string value, bool isTagHelper)
	{
		if (attributeName is "WidthRequest" or "HeightRequest" or "MaxPixelDensity" ||
			(isTagHelper && attributeName is "ImageMaxPixelDensity"))
		{
			return TryGetStaticPositiveInt(value, out _);
		}

		if (attributeName is "ResizeMode")
			return TryGetStaticEnumMember(value, DynamicResizeModeTypeName, AllowsUnqualifiedEnumMember(usage, DynamicResizeModeTypeName, isTagHelper), out _);

		if (attributeName is "ImageFormat")
			return TryGetStaticEnumMember(value, DynamicImageFormatTypeName, AllowsUnqualifiedEnumMember(usage, DynamicImageFormatTypeName, isTagHelper), out _);

		if (attributeName is "SizeWidths")
			return TryGetStaticString(value, out _);

		if (attributeName is "EnableFocalPointSelection")
			return TryGetStaticBoolean(value, out _);

		return true;
	}

	public static bool AllowsUnqualifiedEnumMember(DynamicImageRazorUsage usage, string expectedEnumTypeName, bool isTagHelper)
	{
		if (isTagHelper || usage.StaticUsingTypeNames.Contains(expectedEnumTypeName))
			return true;

		int lastDotIndex = expectedEnumTypeName.LastIndexOf('.');
		string simpleTypeName = lastDotIndex >= 0
			? expectedEnumTypeName.Substring(lastDotIndex + 1)
			: expectedEnumTypeName;

		return usage.StaticUsingTypeNames.Contains(simpleTypeName);
	}

	private static void ParseDocument(
		DynamicImageRazorDocument document,
		bool hasDynamicImageComponentType,
		bool dynamicImageComponentIsActive,
		bool hasFileImagePreviewUploadComponentType,
		bool fileImagePreviewUploadComponentIsActive,
		bool imageTagHelperIsActive,
		bool pictureSourceTagHelperIsActive,
		bool hasDynamicImageSourceComponentType,
		bool dynamicImageSourceComponentIsActive,
		ImmutableHashSet<string> staticUsingTypeNames,
		ImmutableArray<DynamicImageRazorUsage>.Builder result)
	{
		string maskedText = MaskIgnoredContent(document.Text);
		// Nested sources inherit from the element they are declared inside, so the open image elements have to be tracked as the document
		// is scanned.
		var openImageUsages = new Stack<KeyValuePair<string, DynamicImageRazorUsage>>();

		for (int index = 0; index < maskedText.Length; index++)
		{
			if (maskedText[index] is not '<' ||
				index + 1 >= maskedText.Length ||
				maskedText[index + 1] is '!' or '?')
			{
				continue;
			}

			bool isClosingTag = maskedText[index + 1] is '/';
			int nameStart = index + (isClosingTag ? 2 : 1);
			int nameEnd = nameStart;

			while (nameEnd < maskedText.Length && IsTagNameCharacter(maskedText[nameEnd]))
				nameEnd++;

			if (nameEnd == nameStart)
				continue;

			string tagName = maskedText.Substring(nameStart, nameEnd - nameStart);

			if (isClosingTag)
			{
				if (openImageUsages.Count > 0 && string.Equals(openImageUsages.Peek().Key, tagName, StringComparison.OrdinalIgnoreCase))
					_ = openImageUsages.Pop();

				continue;
			}

			DynamicImageRazorUsageKind? kind = GetUsageKind(
				tagName,
				hasDynamicImageComponentType,
				dynamicImageComponentIsActive,
				hasFileImagePreviewUploadComponentType,
				fileImagePreviewUploadComponentIsActive,
				imageTagHelperIsActive,
				pictureSourceTagHelperIsActive,
				hasDynamicImageSourceComponentType,
				dynamicImageSourceComponentIsActive);

			if (kind is null)
				continue;

			int tagEnd = FindTagEnd(maskedText, nameEnd);

			if (tagEnd < 0)
				break;

			ImmutableArray<DynamicImageRazorAttribute> attributes = ParseAttributes(document.Text, nameEnd, tagEnd);
			DynamicImageRazorUsage? parent = IsNestedSourceKind(kind.Value) && openImageUsages.Count > 0
				? openImageUsages.Peek().Value
				: null;

			var usage = new DynamicImageRazorUsage(document, kind.Value, attributes, staticUsingTypeNames, parent);
			result.Add(usage);

			bool isSelfClosing = maskedText[tagEnd - 1] is '/';

			if (!isSelfClosing && IsImageKind(kind.Value))
				openImageUsages.Push(new KeyValuePair<string, DynamicImageRazorUsage>(tagName, usage));

			index = tagEnd;
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> for the kinds that render a picture element which nested sources can be declared inside.
	/// </summary>
	public static bool IsImageKind(DynamicImageRazorUsageKind kind)
		=> kind is DynamicImageRazorUsageKind.ImageTagHelper or DynamicImageRazorUsageKind.Component;

	/// <summary>
	/// Returns <see langword="true"/> for the kinds that are declared inside an image element and inherit from it.
	/// </summary>
	public static bool IsNestedSourceKind(DynamicImageRazorUsageKind kind)
		=> kind is DynamicImageRazorUsageKind.PictureSourceTagHelper or DynamicImageRazorUsageKind.SourceComponent;

	private static DynamicImageRazorUsageKind? GetUsageKind(
		string tagName,
		bool hasDynamicImageComponentType,
		bool dynamicImageComponentIsActive,
		bool hasFileImagePreviewUploadComponentType,
		bool fileImagePreviewUploadComponentIsActive,
		bool imageTagHelperIsActive,
		bool pictureSourceTagHelperIsActive,
		bool hasDynamicImageSourceComponentType,
		bool dynamicImageSourceComponentIsActive)
	{
		if (hasDynamicImageSourceComponentType &&
			(string.Equals(tagName, FullyQualifiedDynamicImageSourceComponentName, StringComparison.Ordinal) ||
			 dynamicImageSourceComponentIsActive && string.Equals(tagName, "UmbrellaDynamicImageSource", StringComparison.Ordinal)))
		{
			return DynamicImageRazorUsageKind.SourceComponent;
		}

		if (hasDynamicImageComponentType &&
			(string.Equals(tagName, FullyQualifiedDynamicImageComponentName, StringComparison.Ordinal) ||
			 dynamicImageComponentIsActive && string.Equals(tagName, "UmbrellaDynamicImage", StringComparison.Ordinal)))
		{
			return DynamicImageRazorUsageKind.Component;
		}

		if (hasFileImagePreviewUploadComponentType &&
			(string.Equals(tagName, FullyQualifiedFileImagePreviewUploadComponentName, StringComparison.Ordinal) ||
			 fileImagePreviewUploadComponentIsActive && string.Equals(tagName, "UmbrellaFileImagePreviewUpload", StringComparison.Ordinal)))
		{
			return DynamicImageRazorUsageKind.FileImagePreviewUploadComponent;
		}

		if (imageTagHelperIsActive && string.Equals(tagName, "dynamic-image", StringComparison.OrdinalIgnoreCase))
			return DynamicImageRazorUsageKind.ImageTagHelper;

		if (pictureSourceTagHelperIsActive && string.Equals(tagName, "dynamic-source", StringComparison.OrdinalIgnoreCase))
			return DynamicImageRazorUsageKind.PictureSourceTagHelper;

		return null;
	}

	private static ImmutableArray<DynamicImageRazorAttribute> ParseAttributes(string text, int start, int end)
	{
		var result = ImmutableArray.CreateBuilder<DynamicImageRazorAttribute>();
		int index = start;

		while (index < end)
		{
			while (index < end && (char.IsWhiteSpace(text[index]) || text[index] is '/'))
				index++;

			if (index >= end)
				break;

			int nameStart = index;

			while (index < end && IsAttributeNameCharacter(text[index]))
				index++;

			if (index == nameStart)
			{
				index++;
				continue;
			}

			string name = text.Substring(nameStart, index - nameStart);

			while (index < end && char.IsWhiteSpace(text[index]))
				index++;

			string value = string.Empty;

			if (index < end && text[index] is '=')
			{
				index++;

				while (index < end && char.IsWhiteSpace(text[index]))
					index++;

				if (index < end && text[index] is '"' or '\'')
				{
					char quote = text[index++];
					int valueStart = index;

					while (index < end && text[index] != quote)
						index++;

					value = text.Substring(valueStart, index - valueStart);

					if (index < end)
						index++;
				}
				else
				{
					int valueStart = index;

					while (index < end && !char.IsWhiteSpace(text[index]) && text[index] is not '>')
						index++;

					value = text.Substring(valueStart, index - valueStart);
				}
			}

			result.Add(new DynamicImageRazorAttribute(name, value, nameStart, name.Length));
		}

		return result.ToImmutable();
	}

	private static string BuildEffectiveDirectives(DynamicImageRazorDocument document, IEnumerable<DynamicImageRazorDocument> imports)
	{
		string documentDirectory = NormalizeDirectory(Path.GetDirectoryName(document.Path) ?? string.Empty);
		var applicableImports = imports
			.Where(x =>
			{
				string importDirectory = NormalizeDirectory(Path.GetDirectoryName(x.Path) ?? string.Empty);
				return IsSameOrParentDirectory(importDirectory, documentDirectory) &&
					string.Equals(GetRazorExtension(x.Path), GetRazorExtension(document.Path), StringComparison.OrdinalIgnoreCase);
			})
			.OrderBy(x => NormalizeDirectory(Path.GetDirectoryName(x.Path) ?? string.Empty).Length)
			.ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase);

		var builder = new StringBuilder();

		foreach (DynamicImageRazorDocument import in applicableImports)
			_ = builder.AppendLine(import.Text);

		_ = builder.AppendLine(document.Text);
		return builder.ToString();
	}

	private static bool ContainsUsing(string directives, string targetNamespace)
	{
		foreach (string line in SplitLines(directives))
		{
			string trimmed = line.Trim();

			if (!trimmed.StartsWith("@using ", StringComparison.Ordinal))
				continue;

			string value = trimmed.Substring(7).Trim().TrimEnd(';');

			if (string.Equals(value, targetNamespace, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static ImmutableHashSet<string> GetStaticUsingTypeNames(string directives)
	{
		var result = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

		foreach (string line in SplitLines(directives))
		{
			string trimmed = line.Trim();

			if (!TryGetStaticUsingTypeName(trimmed, out string typeName))
				continue;

			if (typeName.StartsWith("global::", StringComparison.Ordinal))
				typeName = typeName.Substring(8);

			if (typeName.Length > 0)
				_ = result.Add(typeName);
		}

		return result.ToImmutable();
	}

	private static bool TryGetStaticUsingTypeName(string value, out string typeName)
	{
		const string usingKeyword = "@using";
		const string staticKeyword = "static";
		typeName = string.Empty;

		if (!value.StartsWith(usingKeyword, StringComparison.Ordinal))
			return false;

		int index = usingKeyword.Length;

		if (!SkipRequiredWhitespace(value, ref index) ||
			value.Length - index < staticKeyword.Length ||
			string.CompareOrdinal(value, index, staticKeyword, 0, staticKeyword.Length) is not 0)
		{
			return false;
		}

		index += staticKeyword.Length;

		if (!SkipRequiredWhitespace(value, ref index))
			return false;

		typeName = value.Substring(index).Trim().TrimEnd(';').Trim();
		return typeName.Length > 0;
	}

	private static bool SkipRequiredWhitespace(string value, ref int index)
	{
		if (index >= value.Length || !char.IsWhiteSpace(value[index]))
			return false;

		do
		{
			index++;
		}
		while (index < value.Length && char.IsWhiteSpace(value[index]));

		return true;
	}

	private static (bool ImageTagHelper, bool PictureSourceTagHelper) GetActiveTagHelpers(string directives)
	{
		bool imageTagHelperIsActive = false;
		bool pictureSourceTagHelperIsActive = false;

		foreach (string line in SplitLines(directives))
		{
			string trimmed = line.Trim();
			bool isAdd;
			string directiveValue;

			if (trimmed.StartsWith("@addTagHelper ", StringComparison.Ordinal))
			{
				isAdd = true;
				directiveValue = trimmed.Substring(14).Trim();
			}
			else if (trimmed.StartsWith("@removeTagHelper ", StringComparison.Ordinal))
			{
				isAdd = false;
				directiveValue = trimmed.Substring(17).Trim();
			}
			else
			{
				continue;
			}

			int separatorIndex = directiveValue.IndexOf(',');

			if (separatorIndex < 0)
				continue;

			string typePattern = directiveValue.Substring(0, separatorIndex).Trim();
			string assemblyName = directiveValue.Substring(separatorIndex + 1).Trim();

			if (!string.Equals(assemblyName, TagHelperAssemblyName, StringComparison.Ordinal))
				continue;

			if (MatchesTypePattern(typePattern, DynamicImageTagHelperTypeName))
				imageTagHelperIsActive = isAdd;

			if (MatchesTypePattern(typePattern, DynamicImagePictureSourceTagHelperTypeName))
				pictureSourceTagHelperIsActive = isAdd;
		}

		return (imageTagHelperIsActive, pictureSourceTagHelperIsActive);
	}

	private static bool MatchesTypePattern(string pattern, string typeName)
	{
		int patternIndex = 0;
		int typeIndex = 0;
		int wildcardIndex = -1;
		int wildcardMatchIndex = -1;

		while (typeIndex < typeName.Length)
		{
			if (patternIndex < pattern.Length && pattern[patternIndex] == typeName[typeIndex])
			{
				patternIndex++;
				typeIndex++;
			}
			else if (patternIndex < pattern.Length && pattern[patternIndex] is '*')
			{
				wildcardIndex = patternIndex++;
				wildcardMatchIndex = typeIndex;
			}
			else if (wildcardIndex >= 0)
			{
				patternIndex = wildcardIndex + 1;
				typeIndex = ++wildcardMatchIndex;
			}
			else
			{
				return false;
			}
		}

		while (patternIndex < pattern.Length && pattern[patternIndex] is '*')
			patternIndex++;

		return patternIndex == pattern.Length;
	}

	private static string MaskIgnoredContent(string text)
	{
		char[] chars = text.ToCharArray();
		MaskDelimited(chars, text, "@*", "*@");
		MaskDelimited(chars, text, "<!--", "-->");
		MaskRazorCodeBlock(chars, text, "@code");
		MaskRazorCodeBlock(chars, text, "@functions");
		MaskRazorCodeBlock(chars, text, "@{");
		return new string(chars);
	}

	private static void MaskDelimited(char[] chars, string text, string startToken, string endToken)
	{
		int searchIndex = 0;

		while (searchIndex < text.Length)
		{
			int start = text.IndexOf(startToken, searchIndex, StringComparison.Ordinal);

			if (start < 0)
				return;

			int end = text.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
			end = end < 0 ? text.Length : Math.Min(text.Length, end + endToken.Length);
			Mask(chars, start, end);
			searchIndex = end;
		}
	}

	private static void MaskRazorCodeBlock(char[] chars, string text, string token)
	{
		int searchIndex = 0;

		while (searchIndex < text.Length)
		{
			int start = text.IndexOf(token, searchIndex, StringComparison.Ordinal);

			if (start < 0)
				return;

			int braceStart = token.EndsWith("{", StringComparison.Ordinal)
				? start + token.Length - 1
				: text.IndexOf('{', start + token.Length);

			if (braceStart < 0)
				return;

			int end = FindBalancedBraceEnd(text, braceStart);
			Mask(chars, start, end < 0 ? text.Length : end + 1);
			searchIndex = end < 0 ? text.Length : end + 1;
		}
	}

	private static int FindBalancedBraceEnd(string text, int braceStart)
	{
		int depth = 0;
		char quote = '\0';

		for (int index = braceStart; index < text.Length; index++)
		{
			char current = text[index];

			if (quote != '\0')
			{
				if (current is '\\')
				index++;
				else if (current == quote)
					quote = '\0';

				continue;
			}

			if (current is '"' or '\'')
			{
				quote = current;
				continue;
			}

			if (current is '{')
				depth++;
			else if (current is '}' && --depth is 0)
				return index;
		}

		return -1;
	}

	private static int FindTagEnd(string text, int start)
	{
		char quote = '\0';

		for (int index = start; index < text.Length; index++)
		{
			char current = text[index];

			if (quote != '\0')
			{
				if (current == quote)
					quote = '\0';

				continue;
			}

			if (current is '"' or '\'')
				quote = current;
			else if (current is '>')
				return index;
		}

		return -1;
	}

	private static void Mask(char[] chars, int start, int end)
	{
		for (int index = start; index < end && index < chars.Length; index++)
		{
			if (chars[index] is not ('\r' or '\n'))
				chars[index] = ' ';
		}
	}

	private static bool IsImportsDocument(DynamicImageRazorDocument document)
	{
		string fileName = Path.GetFileName(document.Path);

		if (fileName.EndsWith(PreparedExternalSourceSuffix, StringComparison.OrdinalIgnoreCase))
			fileName = fileName.Substring(0, fileName.Length - PreparedExternalSourceSuffix.Length);

		return string.Equals(fileName, "_Imports.razor", StringComparison.OrdinalIgnoreCase) ||
			   string.Equals(fileName, "_ViewImports.cshtml", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetRazorExtension(string path)
	{
		string logicalPath = path.EndsWith(PreparedExternalSourceSuffix, StringComparison.OrdinalIgnoreCase)
			? path.Substring(0, path.Length - PreparedExternalSourceSuffix.Length)
			: path;

		return Path.GetExtension(logicalPath);
	}

	private static bool IsTagNameCharacter(char value)
		=> char.IsLetterOrDigit(value) || value is '_' or '-' or '.' or ':';

	private static bool IsAttributeNameCharacter(char value)
		=> char.IsLetterOrDigit(value) || value is '_' or '-' or ':' or '@';

	private static string NormalizeAttributeValue(string value)
	{
		string normalized = value.Trim();

		if (normalized.StartsWith("@(", StringComparison.Ordinal) && normalized.EndsWith(")", StringComparison.Ordinal))
			normalized = normalized.Substring(2, normalized.Length - 3).Trim();

		return normalized;
	}

	private static bool ContainsRazorTransition(string value)
	{
		for (int index = 0; index < value.Length; index++)
		{
			if (value[index] is not '@')
				continue;

			if (index + 1 < value.Length && value[index + 1] is '@')
			{
				index++;
				continue;
			}

			return true;
		}

		return false;
	}

	private static bool IsIdentifier(string value)
		=> value.Length > 0 &&
		   (char.IsLetter(value[0]) || value[0] is '_') &&
		   value.Skip(1).All(x => char.IsLetterOrDigit(x) || x is '_');

	private static string NormalizeDirectory(string path)
	{
		string fullPath;

		try
		{
			fullPath = Path.GetFullPath(path);
		}
		catch (ArgumentException)
		{
			fullPath = path;
		}
		catch (NotSupportedException)
		{
			fullPath = path;
		}
		catch (System.Security.SecurityException)
		{
			fullPath = path;
		}

		return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
	}

	private static bool IsSameOrParentDirectory(string candidateParent, string child)
	{
		if (string.Equals(candidateParent, child, StringComparison.OrdinalIgnoreCase))
			return true;

		string prefix = candidateParent + Path.DirectorySeparatorChar;
		return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
	}

	private static string[] SplitLines(string text)
		=> text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
