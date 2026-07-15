using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;

namespace Umbrella.AI.Tools.Services;

internal static partial class CodexMcpConfigManager
{
    public const string RelativePath = ".codex\\config.toml";

    public static string RenderManagedContent(JsonObject servers)
    {
        var builder = new StringBuilder();

        foreach ((string serverName, JsonNode? serverNode) in servers)
        {
            if (serverNode is not JsonObject server)
            {
                throw new InvalidOperationException($"MCP server '{serverName}' must be a JSON object.");
            }

            ValidateServer(serverName, server);

            if (builder.Length > 0)
            {
                _ = builder.AppendLine();
            }

            _ = builder.Append("[mcp_servers.");
            _ = builder.Append(QuoteString(serverName));
            _ = builder.AppendLine("]");

            foreach ((string propertyName, JsonNode? propertyValue) in server)
            {
                if (propertyName.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string codexPropertyName = propertyName.Equals("headers", StringComparison.OrdinalIgnoreCase)
                    ? "http_headers"
                    : propertyName;

                _ = builder.Append(FormatKey(codexPropertyName));
                _ = builder.Append(" = ");
                _ = builder.AppendLine(RenderValue(propertyValue, serverName, propertyName));
            }
        }

        return NormalizeNewLines(builder.ToString()).TrimEnd();
    }

    public static string ComputeManagedHash(string content)
        => HashUtility.ComputeStringHash(NormalizeNewLines(content).Trim());

    public static bool TryBuildUpdatedConfig(
        string existingContent,
        string bundleId,
        JsonObject servers,
        string? expectedManagedHash,
        bool force,
        bool allowUntrackedManagedBlockReplacement,
        out string updatedContent,
        out List<string> conflicts)
    {
        conflicts = [];
        string renderedContent;

        try
        {
            renderedContent = RenderManagedContent(servers);
        }
        catch (InvalidOperationException exception)
        {
            updatedContent = existingContent;
            conflicts.Add(exception.Message);
            return false;
        }

        bool hasManagedBlock = TryGetManagedContent(existingContent, bundleId, out string? managedContent);
        string renderedHash = ComputeManagedHash(renderedContent);

        if (expectedManagedHash is not null)
        {
            if (!hasManagedBlock)
            {
                if (!force)
                {
                    conflicts.Add($"Managed Codex MCP block is missing: {RelativePath}");
                }
            }
            else if (!force && ComputeManagedHash(managedContent!) != expectedManagedHash)
            {
                conflicts.Add($"Managed Codex MCP block was modified: {RelativePath}");
            }
        }
        else if (hasManagedBlock
            && !allowUntrackedManagedBlockReplacement
            && !force
            && ComputeManagedHash(managedContent!) != renderedHash)
        {
            conflicts.Add($"Unowned Codex MCP block already exists: {RelativePath}");
        }

        string unmanagedContent = RemoveManagedBlock(existingContent, bundleId);

        if (!TryReadMcpServerNames(unmanagedContent, out HashSet<string> existingServerNames, out string? parseError))
        {
            conflicts.Add($"Could not parse {RelativePath}: {parseError}");
            updatedContent = existingContent;
            return false;
        }

        string[] collisions = servers
            .Select(x => x.Key)
            .Where(existingServerNames.Contains)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (collisions.Length > 0 && !force)
        {
            conflicts.AddRange(collisions.Select(x => $"Codex MCP server already exists outside the managed block: {x}"));
        }

        if (conflicts.Count > 0)
        {
            updatedContent = existingContent;
            return false;
        }

        if (collisions.Length > 0)
        {
            unmanagedContent = RemoveServerTables(unmanagedContent, collisions);

            if (!TryReadMcpServerNames(unmanagedContent, out existingServerNames, out parseError))
            {
                conflicts.Add($"Could not parse {RelativePath} after taking ownership: {parseError}");
                updatedContent = existingContent;
                return false;
            }

            string[] remainingCollisions = collisions.Where(existingServerNames.Contains).ToArray();

            if (remainingCollisions.Length > 0)
            {
                conflicts.AddRange(remainingCollisions.Select(x => $"Could not take ownership of inline Codex MCP server configuration: {x}"));
                updatedContent = existingContent;
                return false;
            }
        }

        updatedContent = hasManagedBlock && collisions.Length == 0
            ? ReplaceManagedBlock(existingContent, bundleId, renderedContent)
            : AppendManagedBlock(unmanagedContent, bundleId, renderedContent);

        if (!TryParseToml(updatedContent, out parseError))
        {
            conflicts.Add($"Generated {RelativePath} is invalid: {parseError}");
            updatedContent = existingContent;
            return false;
        }

        return true;
    }

    public static bool TryGetManagedContent(string content, string bundleId, out string? managedContent)
    {
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(endMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            managedContent = null;
            return false;
        }

        int contentStart = startIndex + startMarker.Length;
        managedContent = content[contentStart..endIndex].Trim('\r', '\n');
        return true;
    }

    public static string RemoveManagedBlock(string content, string bundleId)
    {
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(endMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            return content;
        }

        int lineStart = content.LastIndexOf('\n', startIndex);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        int afterEndMarker = endIndex + endMarker.Length;
        int lineEnd = content.IndexOf('\n', afterEndMarker);
        lineEnd = lineEnd < 0 ? content.Length : lineEnd + 1;

        return content.Remove(lineStart, lineEnd - lineStart);
    }

    private static string AppendManagedBlock(string unmanagedContent, string bundleId, string renderedContent)
    {
        string newLine = DetectNewLine(unmanagedContent);
        var builder = new StringBuilder();

        if (unmanagedContent.Length > 0)
        {
            _ = builder.Append(unmanagedContent);

            if (!EndsWithNewLine(unmanagedContent))
            {
                _ = builder.Append(newLine);
            }

            if (!EndsWithBlankLine(builder.ToString()))
            {
                _ = builder.Append(newLine);
            }
        }

        _ = builder.Append(GetStartMarker(bundleId));
        _ = builder.Append(newLine);

        if (renderedContent.Length > 0)
        {
            _ = builder.Append(renderedContent.Replace("\n", newLine, StringComparison.Ordinal));
            _ = builder.Append(newLine);
        }

        _ = builder.Append(GetEndMarker(bundleId));
        _ = builder.Append(newLine);
        return builder.ToString();
    }

    private static string ReplaceManagedBlock(string content, string bundleId, string renderedContent)
    {
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        int startIndex = content.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
        string newLine = DetectNewLine(content);
        string replacement = startMarker + newLine;

        if (renderedContent.Length > 0)
        {
            replacement += renderedContent.Replace("\n", newLine, StringComparison.Ordinal) + newLine;
        }

        replacement += endMarker;
        return content[..startIndex] + replacement + content[(endIndex + endMarker.Length)..];
    }

    private static string RemoveServerTables(string content, IReadOnlyCollection<string> serverNames)
    {
        var names = serverNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] lines = NormalizeNewLines(content).Split('\n');
        var builder = new StringBuilder();
        bool skipSection = false;

        foreach (string line in lines)
        {
            Match tableMatch = TableHeaderRegex().Match(line);

            if (tableMatch.Success)
            {
                skipSection = TryGetServerName(tableMatch, out string? serverName) && names.Contains(serverName!);
            }

            if (!skipSection)
            {
                _ = builder.AppendLine(line);
            }
        }

        return TrimExcessBlankLines(builder.ToString());
    }

    private static bool TryGetServerName(Match match, out string? serverName)
    {
        if (match.Groups["double"].Success)
        {
            serverName = JsonSerializer.Deserialize<string>($"\"{match.Groups["double"].Value}\"");
            return serverName is not null;
        }

        if (match.Groups["single"].Success)
        {
            serverName = match.Groups["single"].Value;
            return true;
        }

        serverName = match.Groups["bare"].Value;
        return serverName.Length > 0;
    }

    private static bool TryReadMcpServerNames(string content, out HashSet<string> serverNames, out string? error)
    {
        serverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
        {
            error = null;
            return true;
        }

        try
        {
            TomlTable root = TomlSerializer.Deserialize<TomlTable>(content) ?? [];

            if (root.TryGetValue("mcp_servers", out object? value) && value is TomlTable mcpServers)
            {
                serverNames.UnionWith(mcpServers.Keys);
            }

            error = null;
            return true;
        }
        catch (TomlException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryParseToml(string content, out string? error)
    {
        try
        {
            _ = TomlSerializer.Deserialize<TomlTable>(content);
            error = null;
            return true;
        }
        catch (TomlException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void ValidateServer(string serverName, JsonObject server)
    {
        string? type = server["type"]?.GetValue<string>();
        bool hasCommand = server["command"] is JsonValue;
        bool hasUrl = server["url"] is JsonValue;

        if (type is not null
            && !type.Equals("stdio", StringComparison.OrdinalIgnoreCase)
            && !type.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"MCP server '{serverName}' has unsupported type '{type}'.");
        }

        if ((type?.Equals("stdio", StringComparison.OrdinalIgnoreCase) ?? hasCommand) && !hasCommand)
        {
            throw new InvalidOperationException($"STDIO MCP server '{serverName}' requires 'command'.");
        }

        if ((type?.Equals("http", StringComparison.OrdinalIgnoreCase) ?? hasUrl) && !hasUrl)
        {
            throw new InvalidOperationException($"HTTP MCP server '{serverName}' requires 'url'.");
        }

        if (!hasCommand && !hasUrl)
        {
            throw new InvalidOperationException($"MCP server '{serverName}' requires either 'command' or 'url'.");
        }

        if (server["headers"] is not null && server["http_headers"] is not null)
        {
            throw new InvalidOperationException($"MCP server '{serverName}' cannot define both 'headers' and 'http_headers'.");
        }
    }

    private static string RenderValue(JsonNode? node, string serverName, string propertyName)
        => node switch
        {
            null => throw new InvalidOperationException($"MCP server '{serverName}' property '{propertyName}' cannot be null."),
            JsonObject value => $"{{ {string.Join(", ", value.Select(x => $"{FormatKey(x.Key)} = {RenderValue(x.Value, serverName, propertyName)}"))} }}",
            JsonArray value => $"[{string.Join(", ", value.Select(x => RenderValue(x, serverName, propertyName)))}]",
            JsonValue value => RenderScalar(value),
            _ => throw new InvalidOperationException($"MCP server '{serverName}' property '{propertyName}' has an unsupported value.")
        };

    private static string RenderScalar(JsonValue value)
    {
        if (value.TryGetValue<string>(out string? stringValue))
        {
            return QuoteString(stringValue);
        }

        if (value.TryGetValue<bool>(out bool booleanValue))
        {
            return booleanValue ? "true" : "false";
        }

        return value.ToJsonString().Replace("E+", "e+", StringComparison.Ordinal).Replace("E-", "e-", StringComparison.Ordinal);
    }

    private static string FormatKey(string key) => QuoteString(key);

    private static string QuoteString(string value) => JsonSerializer.Serialize(value);

    private static string GetStartMarker(string bundleId) => $"# ai-bundle:{bundleId}:codex-mcp:start";

    private static string GetEndMarker(string bundleId) => $"# ai-bundle:{bundleId}:codex-mcp:end";

    private static string NormalizeNewLines(string content)
        => content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string DetectNewLine(string content)
        => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static bool EndsWithNewLine(string content)
        => content.EndsWith('\n') || content.EndsWith('\r');

    private static bool EndsWithBlankLine(string content)
        => NormalizeNewLines(content).EndsWith("\n\n", StringComparison.Ordinal);

    private static string TrimExcessBlankLines(string content)
        => MultipleBlankLinesRegex().Replace(NormalizeNewLines(content).Trim(), "\n\n");

    [GeneratedRegex(@"^\s*\[\[?\s*mcp_servers\s*\.\s*(?:""(?<double>(?:\\.|[^""])*)""|'(?<single>[^']*)'|(?<bare>[A-Za-z0-9_-]+))", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TableHeaderRegex();

    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleBlankLinesRegex();
}
