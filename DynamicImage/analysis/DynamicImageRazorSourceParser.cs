using System.Collections.Immutable;
using System.Text;

namespace Umbrella.DynamicImage.RazorAnalysis;

internal enum DynamicImageRazorUsageKind
{
	Component,
	ImageTagHelper,
	PictureSourceTagHelper
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

	public DynamicImageRazorUsage(
		DynamicImageRazorDocument document,
		DynamicImageRazorUsageKind kind,
		ImmutableArray<DynamicImageRazorAttribute> attributes)
	{
		Document = document;
		Kind = kind;
		Attributes = attributes;
	}
}

internal static class DynamicImageRazorSourceParser
{
	private const string ComponentNamespace = "Umbrella.AspNetCore.Blazor.Components.DynamicImage";
	private const string FullyQualifiedComponentName = ComponentNamespace + ".UmbrellaDynamicImage";
	private const string TagHelperAssemblyName = "Umbrella.AspNetCore.WebUtilities.DynamicImage";
	private const string DynamicImageTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImageTagHelper";
	private const string DynamicImagePictureSourceTagHelperTypeName = "Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.DynamicImagePictureSourceTagHelper";
	private const string DynamicResizeModeTypeName = "Umbrella.DynamicImage.Abstractions.DynamicResizeMode";
	private const string DynamicImageFormatTypeName = "Umbrella.DynamicImage.Abstractions.DynamicImageFormat";
	private const string PreparedExternalSourceSuffix = ".umbrella-dynamic-image";

	public static ImmutableArray<DynamicImageRazorUsage> Parse(
		IEnumerable<DynamicImageRazorDocument> documents,
		bool hasComponentType,
		bool hasImageTagHelperType,
		bool hasPictureSourceTagHelperType)
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
			bool componentIsActive = hasComponentType &&
				(isComponentDocument && ContainsUsing(effectiveDirectives, ComponentNamespace));
			(bool imageTagHelperIsActive, bool pictureSourceTagHelperIsActive) = isViewDocument
				? GetActiveTagHelpers(effectiveDirectives)
				: (false, false);

			ParseDocument(
				document,
				componentIsActive,
				hasImageTagHelperType && imageTagHelperIsActive,
				hasPictureSourceTagHelperType && pictureSourceTagHelperIsActive,
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

	public static bool IsDiscoverableValue(string attributeName, string value, bool isTagHelper)
	{
		if (attributeName is "WidthRequest" or "HeightRequest" or "MaxPixelDensity" ||
			(isTagHelper && attributeName is "ImageMaxPixelDensity"))
		{
			return TryGetStaticPositiveInt(value, out _);
		}

		if (attributeName is "ResizeMode")
			return TryGetStaticEnumMember(value, DynamicResizeModeTypeName, isTagHelper, out _);

		if (attributeName is "ImageFormat")
			return TryGetStaticEnumMember(value, DynamicImageFormatTypeName, isTagHelper, out _);

		if (attributeName is "SizeWidths")
			return TryGetStaticString(value, out _);

		return true;
	}

	private static void ParseDocument(
		DynamicImageRazorDocument document,
		bool componentIsActive,
		bool imageTagHelperIsActive,
		bool pictureSourceTagHelperIsActive,
		ImmutableArray<DynamicImageRazorUsage>.Builder result)
	{
		string maskedText = MaskIgnoredContent(document.Text);

		for (int index = 0; index < maskedText.Length; index++)
		{
			if (maskedText[index] is not '<' ||
				index + 1 >= maskedText.Length ||
				maskedText[index + 1] is '/' or '!' or '?')
			{
				continue;
			}

			int nameStart = index + 1;
			int nameEnd = nameStart;

			while (nameEnd < maskedText.Length && IsTagNameCharacter(maskedText[nameEnd]))
				nameEnd++;

			if (nameEnd == nameStart)
				continue;

			string tagName = maskedText.Substring(nameStart, nameEnd - nameStart);
			DynamicImageRazorUsageKind? kind = GetUsageKind(
				tagName,
				componentIsActive,
				imageTagHelperIsActive,
				pictureSourceTagHelperIsActive);

			if (kind is null)
				continue;

			int tagEnd = FindTagEnd(maskedText, nameEnd);

			if (tagEnd < 0)
				break;

			ImmutableArray<DynamicImageRazorAttribute> attributes = ParseAttributes(document.Text, nameEnd, tagEnd);
			result.Add(new DynamicImageRazorUsage(document, kind.Value, attributes));
			index = tagEnd;
		}
	}

	private static DynamicImageRazorUsageKind? GetUsageKind(
		string tagName,
		bool componentIsActive,
		bool imageTagHelperIsActive,
		bool pictureSourceTagHelperIsActive)
	{
		if (string.Equals(tagName, FullyQualifiedComponentName, StringComparison.Ordinal) ||
			(componentIsActive && string.Equals(tagName, "UmbrellaDynamicImage", StringComparison.Ordinal)))
		{
			return DynamicImageRazorUsageKind.Component;
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
