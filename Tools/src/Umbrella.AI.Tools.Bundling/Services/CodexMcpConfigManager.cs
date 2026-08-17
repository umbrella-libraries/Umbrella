using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;

namespace Umbrella.AI.Tools.Bundling.Services;

/// <summary>
/// Manages the shared, co-owned MCP region in <c>.codex/config.toml</c>.
/// </summary>
/// <remarks>
/// The region holds the deterministic union of the servers contributed by every installed bundle.
/// A per-bundle region cannot work here: TOML has no way to express the same <c>[mcp_servers.x]</c>
/// table twice, so two bundles declaring a shared server would produce an unparseable document.
/// Servers are rendered in ordinal name order so the result does not depend on install order.
/// </remarks>
internal static partial class CodexMcpConfigManager
{
    public const string RelativePath = ".codex\\config.toml";

    private const string StartMarker = "# ai-bundle:codex-mcp:start";
    private const string EndMarker = "# ai-bundle:codex-mcp:end";

    /// <summary>
    /// Renders servers as Codex <c>[mcp_servers.*]</c> tables in ordinal name order.
    /// </summary>
    public static string RenderManagedContent(JsonObject servers)
    {
        var builder = new StringBuilder();

        foreach ((string serverName, JsonNode? serverNode) in servers.OrderBy(x => x.Key, StringComparer.Ordinal))
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

    /// <summary>
    /// Rebuilds <c>.codex/config.toml</c> so the shared region contains <paramref name="unionServers"/>.
    /// </summary>
    /// <param name="existingContent">Current file content, or empty when the file does not exist.</param>
    /// <param name="unionServers">Every server owned by any installed bundle, including this one.</param>
    /// <param name="ownServers">The servers this bundle contributes, used for tracked drift detection.</param>
    /// <param name="expectedOwnHash">Manifest hash of this bundle's contribution, or null on first install.</param>
    /// <param name="force">Take ownership of drifted or unowned content.</param>
    /// <param name="allowUntrackedManagedBlockReplacement">Allow replacing a region this bundle does not yet track.</param>
    /// <param name="previouslyOwnedServers">
    /// Servers this bundle's manifest currently records. A server the region still declares because
    /// this bundle used to own it is being cleaned up by this very operation, so it must not be
    /// mistaken for user-authored content.
    /// </param>
    public static bool TryBuildUpdatedConfig(
        string existingContent,
        JsonObject unionServers,
        JsonObject ownServers,
        string? expectedOwnHash,
        bool force,
        bool allowUntrackedManagedBlockReplacement,
        IEnumerable<string> previouslyOwnedServers,
        out string updatedContent,
        out List<string> conflicts)
    {
        conflicts = [];
        string unionContent;

        try
        {
            unionContent = RenderManagedContent(unionServers);
        }
        catch (InvalidOperationException exception)
        {
            updatedContent = existingContent;
            conflicts.Add(exception.Message);
            return false;
        }

        // A legacy per-bundle region predates the shared region. Absorb any that remain: every server
        // they declared is already in .mcp.json, so the union re-render reproduces them.
        string workingContent = AbsorbLegacyRegions(existingContent, out bool absorbedLegacy);
        bool hasSharedRegion = TryGetManagedContent(workingContent, out string? regionContent);
        bool migrating = absorbedLegacy && !hasSharedRegion;

        var regionServerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasSharedRegion)
        {
            if (!TryReadMcpServerNames(regionContent!, out HashSet<string> parsedRegionNames, out string? regionParseError))
            {
                conflicts.Add($"Could not parse the managed Codex MCP region in {RelativePath}: {regionParseError}");
                updatedContent = existingContent;
                return false;
            }

            regionServerNames = parsedRegionNames;
        }

        if (!migrating && !force)
        {
            ValidateRegionOwnership(
                expectedOwnHash,
                hasSharedRegion,
                regionServerNames,
                unionServers,
                ownServers,
                allowUntrackedManagedBlockReplacement,
                previouslyOwnedServers,
                conflicts);
        }

        string unmanagedContent = RemoveManagedRegion(workingContent);

        if (!TryReadMcpServerNames(unmanagedContent, out HashSet<string> outsideServerNames, out string? parseError))
        {
            conflicts.Add($"Could not parse {RelativePath}: {parseError}");
            updatedContent = existingContent;
            return false;
        }

        string[] collisions = [.. unionServers
            .Select(x => x.Key)
            .Where(outsideServerNames.Contains)
            .Order(StringComparer.OrdinalIgnoreCase)];

        if (collisions.Length > 0 && !force)
        {
            conflicts.AddRange(collisions.Select(x => $"Codex MCP server already exists outside the managed region: {x}"));
        }

        if (conflicts.Count > 0)
        {
            updatedContent = existingContent;
            return false;
        }

        if (collisions.Length > 0)
        {
            unmanagedContent = RemoveServerTables(unmanagedContent, collisions);

            if (!TryReadMcpServerNames(unmanagedContent, out outsideServerNames, out parseError))
            {
                conflicts.Add($"Could not parse {RelativePath} after taking ownership: {parseError}");
                updatedContent = existingContent;
                return false;
            }

            string[] remainingCollisions = [.. collisions.Where(outsideServerNames.Contains)];

            if (remainingCollisions.Length > 0)
            {
                conflicts.AddRange(remainingCollisions.Select(x => $"Could not take ownership of inline Codex MCP server configuration: {x}"));
                updatedContent = existingContent;
                return false;
            }
        }

        updatedContent = hasSharedRegion && collisions.Length == 0
            ? ReplaceManagedRegion(workingContent, unionContent)
            : AppendManagedRegion(unmanagedContent, unionContent);

        if (!TryParseToml(updatedContent, out parseError))
        {
            conflicts.Add($"Generated {RelativePath} is invalid: {parseError}");
            updatedContent = existingContent;
            return false;
        }

        return true;
    }

    private static void ValidateRegionOwnership(
        string? expectedOwnHash,
        bool hasSharedRegion,
        HashSet<string> regionServerNames,
        JsonObject unionServers,
        JsonObject ownServers,
        bool allowUntrackedManagedBlockReplacement,
        IEnumerable<string> previouslyOwnedServers,
        List<string> conflicts)
    {
        if (expectedOwnHash is not null)
        {
            if (!hasSharedRegion)
            {
                conflicts.Add($"Managed Codex MCP region is missing: {RelativePath}");
                return;
            }

            string[] missing = [.. ownServers
                .Select(x => x.Key)
                .Where(x => !regionServerNames.Contains(x))
                .Order(StringComparer.OrdinalIgnoreCase)];

            if (missing.Length > 0)
            {
                conflicts.Add(
                    $"Managed Codex MCP region no longer declares owned servers ({string.Join(", ", missing)}): {RelativePath}");
            }
        }
        // Untracked: a region may legitimately already exist because another bundle owns it, and sync
        // regenerates it outright, so neither case is user-authored content.
        else if (!hasSharedRegion || allowUntrackedManagedBlockReplacement)
        {
            return;
        }

        // A server inside the markers that no installed bundle accounts for is user-authored content
        // whether or not this bundle already tracks the region, so the check runs on both paths. A
        // server this bundle is dropping in this same operation is still accounted for.
        var accountedFor = new HashSet<string>(previouslyOwnedServers, StringComparer.OrdinalIgnoreCase);
        accountedFor.UnionWith(unionServers.Select(x => x.Key));

        string[] unowned = [.. regionServerNames
            .Where(x => !accountedFor.Contains(x))
            .Order(StringComparer.OrdinalIgnoreCase)];

        if (unowned.Length > 0)
        {
            conflicts.Add(
                $"Managed Codex MCP region contains servers owned by no bundle ({string.Join(", ", unowned)}): {RelativePath}");
        }
    }

    public static bool TryGetManagedContent(string content, out string? managedContent)
    {
        int startIndex = content.IndexOf(StartMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(EndMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            managedContent = null;
            return false;
        }

        managedContent = content[(startIndex + StartMarker.Length)..endIndex].Trim('\r', '\n');
        return true;
    }

    /// <summary>
    /// Reads the server names declared inside the shared region, if present.
    /// </summary>
    public static bool TryGetManagedServerNames(string content, out HashSet<string> serverNames, out string? error)
    {
        if (!TryGetManagedContent(content, out string? managedContent))
        {
            serverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            error = null;
            return false;
        }

        return TryReadMcpServerNames(managedContent!, out serverNames, out error);
    }

    public static string RemoveManagedRegion(string content) => RemoveRegion(content, StartMarker, EndMarker);

    /// <summary>
    /// Removes every legacy per-bundle <c>ai-bundle:&lt;id&gt;:codex-mcp</c> region, regardless of bundle id.
    /// </summary>
    public static string AbsorbLegacyRegions(string content, out bool absorbedLegacy)
    {
        absorbedLegacy = false;
        string working = content;

        while (true)
        {
            Match match = LegacyStartMarkerRegex().Match(working);

            if (!match.Success)
            {
                return working;
            }

            string bundleId = match.Groups["bundleId"].Value;
            string legacyStart = $"# ai-bundle:{bundleId}:codex-mcp:start";
            string legacyEnd = $"# ai-bundle:{bundleId}:codex-mcp:end";
            string updated = RemoveRegion(working, legacyStart, legacyEnd);

            if (updated == working)
            {
                // Start marker with no matching end marker: leave it alone rather than loop forever.
                return working;
            }

            absorbedLegacy = true;
            working = updated;
        }
    }

    private static string RemoveRegion(string content, string startMarker, string endMarker)
    {
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

    private static string AppendManagedRegion(string unmanagedContent, string renderedContent)
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

        _ = builder.Append(StartMarker);
        _ = builder.Append(newLine);

        if (renderedContent.Length > 0)
        {
            _ = builder.Append(renderedContent.Replace("\n", newLine, StringComparison.Ordinal));
            _ = builder.Append(newLine);
        }

        _ = builder.Append(EndMarker);
        _ = builder.Append(newLine);
        return builder.ToString();
    }

    private static string ReplaceManagedRegion(string content, string renderedContent)
    {
        int startIndex = content.IndexOf(StartMarker, StringComparison.Ordinal);
        int endIndex = content.IndexOf(EndMarker, startIndex + StartMarker.Length, StringComparison.Ordinal);
        string newLine = DetectNewLine(content);
        string replacement = StartMarker + newLine;

        if (renderedContent.Length > 0)
        {
            replacement += renderedContent.Replace("\n", newLine, StringComparison.Ordinal) + newLine;
        }

        replacement += EndMarker;
        return content[..startIndex] + replacement + content[(endIndex + EndMarker.Length)..];
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
            else if (AnyTableHeaderRegex().IsMatch(line))
            {
                // Any other table header ends the skipped section. Without this, everything after a
                // removed server table, such as [model_providers.*] or [profiles.*], would be dropped.
                skipSection = false;
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

    [GeneratedRegex(@"^\s*\[\[?\s*(?:""(?:\\.|[^""])*""|'[^']*'|[A-Za-z0-9_-]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AnyTableHeaderRegex();

    // \r? matters: the marker line is CRLF in a Windows-authored file, and RegexOptions.Multiline
    // anchors $ before the \n, leaving the \r to match explicitly.
    [GeneratedRegex(@"^#[ \t]*ai-bundle:(?<bundleId>[^:\r\n]+):codex-mcp:start[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyStartMarkerRegex();

    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleBlankLinesRegex();
}
