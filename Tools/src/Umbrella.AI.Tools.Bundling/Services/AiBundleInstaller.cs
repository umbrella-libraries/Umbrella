using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using Umbrella.AI.Tools.Bundling.Models;

namespace Umbrella.AI.Tools.Bundling.Services;

/// <summary>
/// Installs, updates, inspects, and removes an AI bundle in a target repository. The engine holds no
/// knowledge of any particular bundle's content: everything specific to a bundle comes from
/// <see cref="BundleHostOptions"/> and the bundle's <c>bundle.json</c>.
/// </summary>
public sealed partial class AiBundleInstaller
{
    private readonly BundleHostOptions _options;
    private readonly Lazy<string> _assetRoot;
    private readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <summary>
    /// Creates an installer that resolves its asset root on first use, so commands that work purely
    /// from a repository checkout (such as <c>sync</c>) never require shipped assets to be present.
    /// </summary>
    public AiBundleInstaller(BundleHostOptions options, Lazy<string> assetRoot)
    {
        Guard.IsNotNull(options);
        Guard.IsNotNull(assetRoot);
        _options = options;
        _assetRoot = assetRoot;
    }

    /// <summary>
    /// Creates an installer against an already resolved asset root.
    /// </summary>
    public AiBundleInstaller(BundleHostOptions options, string assetRoot)
        : this(options, new Lazy<string>(() => assetRoot))
    {
    }

    private string BundleDefinitionRelativePath => _options.BundleDefinitionRelativePath;

    public OperationResult Install(CommandOptions options)
    {
        Guard.IsNotNull(options);
        return InstallOrUpdate(options, requireExistingManifest: false, operationName: "install");
    }

    public OperationResult Update(CommandOptions options)
    {
        Guard.IsNotNull(options);
        return InstallOrUpdate(options, requireExistingManifest: true, operationName: "update");
    }

    public OperationResult Sync(string startDirectory)
    {
        string? repoRoot = LocateRepoRoot(startDirectory);

        if (repoRoot is null)
        {
            return Failure($"Could not locate '{BundleDefinitionRelativePath}' in '{Path.GetFullPath(startDirectory)}' or any parent directory. Run sync from within an installed repository or pass --root-dir <repo-root>.");
        }

        string bundleDefPath = Path.Combine(repoRoot, BundleDefinitionRelativePath);
        AiBundleDefinition bundle = JsonSerializer.Deserialize<AiBundleDefinition>(File.ReadAllText(bundleDefPath), _serializerOptions)
            ?? throw new InvalidOperationException($"Failed to read bundle definition at {bundleDefPath}.");

        var result = new OperationResult { Success = true };
        result.Messages.Add($"Repository root: {repoRoot}");
        var syncedFiles = new List<(ManagedFileEntry Entry, string TargetPath, string DisplayPath)>();

        foreach (AdapterDirectoryDefinition adapter in bundle.AdapterDirectories)
        {
            string sourceDirectory = Path.Combine(repoRoot, NormalizePath(adapter.Source));

            if (!Directory.Exists(sourceDirectory))
            {
                return Failure(
                    $"Adapter source directory '{NormalizePath(adapter.Source)}' does not exist under '{repoRoot}'. "
                    + "'sync' regenerates adapters from canonical sources, so it must run in the repository that authors "
                    + "the bundle rather than a repository the bundle was installed into.");
            }

            foreach (AdapterTarget target in adapter.Targets)
            {
                string targetDirectory = Path.Combine(repoRoot, NormalizePath(target.Destination));

                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativeToSource = Path.GetRelativePath(sourceDirectory, file);
                    string targetPath = Path.Combine(targetDirectory, relativeToSource);
                    string displayPath = NormalizePath(Path.Combine(target.Destination, relativeToSource));
                    var entry = new ManagedFileEntry(file, target.Substitutions);
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    bool updated = WriteFileIfChanged(targetPath, entry);
                    syncedFiles.Add((entry, targetPath, displayPath));
                    result.Messages.Add(updated ? $"Synced: {displayPath}" : $"Unchanged: {displayPath}");
                }
            }
        }

        if (bundle.SkillListBlocks.Count > 0)
        {
            string firstBlockSkillsDir = bundle.SkillListBlocks[0].SkillsDirectory;
            string skillsSource = Path.Combine(repoRoot, NormalizePath(
                bundle.AdapterDirectories
                    .FirstOrDefault(a => a.Targets.Any(t => t.Destination.Equals(firstBlockSkillsDir, StringComparison.OrdinalIgnoreCase)))
                    ?.Source ?? Path.Combine(".ai-shared", "skills")));
            List<(string Name, string Description)> skills = ReadSkillMetadata(skillsSource);

            foreach (SkillListBlockDefinition blockConfig in bundle.SkillListBlocks)
            {
                List<(string Name, string Description, string FileName)> agents = string.IsNullOrWhiteSpace(blockConfig.AgentsDirectory)
                    ? []
                    : ReadAgentMetadata(Path.Combine(repoRoot, NormalizePath(blockConfig.AgentsDirectory)));
                string blockPath = Path.Combine(repoRoot, NormalizePath(blockConfig.TargetPath));
                _ = Directory.CreateDirectory(Path.GetDirectoryName(blockPath)!);
                File.WriteAllText(blockPath, GenerateSkillListBlock(bundle.ResolvedCatalogName, skills, blockConfig.SkillsDirectory, agents, blockConfig.AgentsDirectory));
                result.Messages.Add($"Generated: {NormalizePath(blockConfig.TargetPath)}");
            }
        }

        // Re-check every target against a fresh read of its source. This catches sources
        // that were modified while the sync was running, which would otherwise leave a
        // stale target behind a "Success" result.
        foreach ((ManagedFileEntry entry, string targetPath, string displayPath) in syncedFiles)
        {
            if (WriteFileIfChanged(targetPath, entry))
            {
                result.Messages.Add($"Re-synced (source changed during sync): {displayPath}");
            }
        }

        string manifestPath = GetManifestPath(repoRoot, bundle.BundleId);
        AiBundleManifest? manifest = File.Exists(manifestPath) ? LoadManifest(manifestPath) : null;

        if (!string.IsNullOrWhiteSpace(bundle.McpSourcePath))
        {
            string mcpPath = Path.Combine(repoRoot, NormalizePath(bundle.McpSourcePath));
            JsonObject? mcpRoot = LoadMcpRoot(mcpPath);

            if (mcpRoot is null)
            {
                return Failure($"Could not locate MCP source '{NormalizePath(bundle.McpSourcePath)}' at repository root '{repoRoot}'.");
            }

            if (mcpRoot["servers"] is not JsonObject sourceServers)
            {
                return Failure(
                    $"MCP source '{NormalizePath(bundle.McpSourcePath)}' must contain a root 'servers' object. "
                    + "Edit that object and run sync to regenerate compatibility outputs.");
            }

            JsonObject allServers = sourceServers.DeepClone().AsObject();
            List<AiBundleManifest> otherManifests = [.. LoadAllManifests(repoRoot).Where(x => !IsThisBundle(x, bundle))];

            // Sync regenerates derived outputs; it never transfers ownership. A server another installed
            // bundle already owns, and this manifest does not, stays recorded against that bundle alone.
            JsonObject ownServers = RestrictServers(allServers, allServers
                .Select(x => x.Key)
                .Where(x => manifest is null || OwnsServer(manifest, x) || !otherManifests.Any(y => OwnsServer(y, x))));

            JsonObject unionServers = BuildUnionServers(allServers, ownServers, otherManifests);

            string codexPath = Path.Combine(repoRoot, NormalizePath(CodexMcpConfigManager.RelativePath));
            string existingCodexConfig = File.Exists(codexPath) ? File.ReadAllText(codexPath) : string.Empty;

            if (!CodexMcpConfigManager.TryBuildUpdatedConfig(existingCodexConfig, unionServers, ownServers,
                expectedOwnHash: null, force: false, allowUntrackedManagedBlockReplacement: true,
                previouslyOwnedServers: [],
                out string updatedCodexConfig, out List<string> codexConflicts))
            {
                result.Success = false;
                result.Conflicts.AddRange(codexConflicts);
                return result;
            }

            JsonObject legacyServers = GetOrCreateMcpServers(mcpRoot);
            legacyServers.Clear();

            // The compatibility mirror is a complete copy of canonical 'servers', ownership aside.
            foreach ((string serverName, JsonNode? serverNode) in allServers)
            {
                legacyServers[serverName] = serverNode?.DeepClone();
            }

            SaveMcpJson(mcpPath, mcpRoot);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(codexPath)!);
            File.WriteAllText(codexPath, updatedCodexConfig);
            result.Messages.Add($"Generated: {CodexMcpConfigManager.RelativePath}");
            result.Messages.Add("Synchronized generated mcpServers compatibility entries in .mcp.json.");

            if (manifest is not null)
            {
                manifest.ManagedMcpServers =
                [
                    .. ownServers.Select(x => new NameHashRecord { Name = x.Key, Hash = HashUtility.ComputeJsonHash(x.Value!) })
                ];
                manifest.ManagedCodexMcp = new PathHashRecord
                {
                    Path = NormalizePath(CodexMcpConfigManager.RelativePath),
                    Hash = CodexMcpConfigManager.ComputeManagedHash(CodexMcpConfigManager.RenderManagedContent(ownServers))
                };
            }
        }

        if (manifest is not null)
        {
            foreach (PathHashRecord record in manifest.ManagedFiles)
            {
                string targetPath = Path.Combine(repoRoot, NormalizePath(record.Path));

                if (File.Exists(targetPath))
                {
                    record.Hash = HashUtility.ComputeFileHash(targetPath);
                }
            }

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, _serializerOptions));
            result.Messages.Add($"Refreshed manifest hashes: {manifestPath}");
        }

        return result;
    }

    private string? LocateRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, BundleDefinitionRelativePath)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static List<(string Name, string Description)> ReadSkillMetadata(string skillsDirectory)
    {
        if (!Directory.Exists(skillsDirectory))
        {
            return [];
        }

        var skills = new List<(string Name, string Description)>();

        foreach (string skillDir in Directory.GetDirectories(skillsDirectory))
        {
            string skillMdPath = Path.Combine(skillDir, "SKILL.md");

            if (!File.Exists(skillMdPath))
            {
                continue;
            }

            (string name, string description) = ReadFrontmatterMetadata(skillMdPath);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description))
            {
                skills.Add((name, description));
            }
        }

        return [.. skills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static List<(string Name, string Description, string FileName)> ReadAgentMetadata(string agentsDirectory)
    {
        if (!Directory.Exists(agentsDirectory))
        {
            return [];
        }

        var agents = new List<(string Name, string Description, string FileName)>();

        foreach (string agentPath in Directory.GetFiles(agentsDirectory, "*.md"))
        {
            (string name, string description) = ReadFrontmatterMetadata(agentPath);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description))
            {
                agents.Add((name, description, Path.GetFileName(agentPath)));
            }
        }

        return [.. agents.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static (string Name, string Description) ReadFrontmatterMetadata(string filePath)
    {
        string name = "", description = "";
        bool inFrontmatter = false;

        foreach (string line in File.ReadLines(filePath))
        {
            if (line.Trim() == "---")
            {
                if (!inFrontmatter)
                {
                    inFrontmatter = true;
                    continue;
                }

                break;
            }

            if (!inFrontmatter)
            {
                break;
            }

            Match match = FrontmatterPropertyRegex().Match(line);

            if (!match.Success)
            {
                continue;
            }

            string rawValue = match.Groups[2].Value;
            string value = rawValue.Length >= 2 && rawValue[0] == '\'' && rawValue[^1] == '\''
                ? rawValue[1..^1]
                : rawValue;

            switch (match.Groups[1].Value)
            {
                case "name": name = value; break;
                case "description": description = value; break;
            }
        }

        return (name, description);
    }

    private static string GenerateSkillListBlock(
        string catalogName,
        List<(string Name, string Description)> skills,
        string skillsDirectory,
        List<(string Name, string Description, string FileName)> agents,
        string? agentsDirectory)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"## {catalogName} Skills");
        _ = sb.AppendLine();
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"The following skills are available in `{skillsDirectory}`. Read a skill's `SKILL.md` for full instructions before using it.");
        _ = sb.AppendLine();

        foreach ((string name, string description) in skills)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"- `{name}` -- {description}");
        }

        if (agents.Count > 0 && !string.IsNullOrWhiteSpace(agentsDirectory))
        {
            _ = sb.AppendLine();
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"## {catalogName} Agents");
            _ = sb.AppendLine();
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"The following agent playbooks are available in `{agentsDirectory}`. For a matching task, read the relevant playbook before starting work.");
            _ = sb.AppendLine();

            foreach ((string name, string description, string fileName) in agents)
            {
                string agentPath = NormalizePath(Path.Combine(agentsDirectory, fileName));
                _ = sb.AppendLine(CultureInfo.InvariantCulture, $"- `{name}` -- {description} Playbook: `{agentPath}`.");
            }
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    public OperationResult GetStatus(CommandOptions options)
    {
        Guard.IsNotNull(options);
        string targetRoot = ResolveTargetRoot(options.TargetPath);
        AiBundleDefinition bundle = LoadBundleDefinition();
        string manifestPath = GetManifestPath(targetRoot, bundle.BundleId);
        var result = new OperationResult { Success = true };

        if (!File.Exists(manifestPath))
        {
            result.Messages.Add($"Bundle '{bundle.BundleId}' is not installed in {targetRoot}.");
            return result;
        }

        AiBundleManifest manifest = LoadManifest(manifestPath)!;
        int healthyFiles = 0;
        int driftedFiles = 0;

        foreach (PathHashRecord fileRecord in manifest.ManagedFiles)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(fileRecord.Path));

            if (File.Exists(targetPath) && HashUtility.ComputeFileHash(targetPath) == fileRecord.Hash)
            {
                healthyFiles++;
            }
            else
            {
                driftedFiles++;
                result.Conflicts.Add($"Managed file drifted: {fileRecord.Path}");
            }
        }

        int healthyBlocks = 0;
        int driftedBlocks = 0;

        foreach (PathHashRecord blockRecord in manifest.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(blockRecord.Path));

            if (TryGetManagedBlock(targetPath, bundle.BundleId, out string? content) && HashUtility.ComputeStringHash(content!) == blockRecord.Hash)
            {
                healthyBlocks++;
            }
            else
            {
                driftedBlocks++;
                result.Conflicts.Add($"Managed doc block drifted: {blockRecord.Path}");
            }
        }

        string mcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject? mcpRoot = LoadMcpRoot(mcpPath);
        JsonObject? servers = mcpRoot is null ? null : GetOrCreateServers(mcpRoot);
        int healthyServers = 0;
        int driftedServers = 0;
        int coOwnedServers = 0;
        List<AiBundleManifest> otherManifests = [.. LoadAllManifests(targetRoot).Where(x => !IsThisBundle(x, bundle))];

        foreach (NameHashRecord serverRecord in manifest.ManagedMcpServers)
        {
            JsonNode? serverNode = servers?[serverRecord.Name];

            if (serverNode is not null && HashUtility.ComputeJsonHash(serverNode) == serverRecord.Hash)
            {
                healthyServers++;

                if (otherManifests.Any(x => OwnsServer(x, serverRecord.Name)))
                {
                    coOwnedServers++;
                }
            }
            else
            {
                driftedServers++;
                result.Conflicts.Add($"Managed MCP server drifted: {serverRecord.Name}");
            }
        }

        int healthyCodexConfigs = 0;
        int driftedCodexConfigs = 0;

        if (manifest.ManagedCodexMcp is not null)
        {
            string codexPath = Path.Combine(targetRoot, NormalizePath(manifest.ManagedCodexMcp.Path));
            string codexContent = File.Exists(codexPath) ? File.ReadAllText(codexPath) : string.Empty;
            JsonObject ownServers = RestrictServers(servers, manifest.ManagedMcpServers.Select(x => x.Name));
            string expectedOwnHash = CodexMcpConfigManager.ComputeManagedHash(CodexMcpConfigManager.RenderManagedContent(ownServers));

            if (!CodexMcpConfigManager.TryGetManagedServerNames(codexContent, out HashSet<string> regionNames, out string? regionError))
            {
                driftedCodexConfigs++;
                result.Conflicts.Add(regionError is null
                    ? $"Managed Codex MCP region is missing: {manifest.ManagedCodexMcp.Path}"
                    : $"Managed Codex MCP region could not be parsed: {regionError}");
            }
            else if (manifest.ManagedMcpServers.Any(x => !regionNames.Contains(x.Name)))
            {
                driftedCodexConfigs++;
                result.Conflicts.Add($"Managed Codex MCP region no longer declares all owned servers: {manifest.ManagedCodexMcp.Path}");
            }
            else if (expectedOwnHash != manifest.ManagedCodexMcp.Hash)
            {
                driftedCodexConfigs++;
                result.Conflicts.Add($"Managed Codex MCP contribution drifted: {manifest.ManagedCodexMcp.Path}");
            }
            // The recorded hash covers this bundle's contribution rendered in isolation, so it cannot
            // see an edit made inside the shared region itself. Compare the region as it sits in the
            // file against a fresh render of the union every installed bundle accounts for.
            else if (!CodexMcpConfigManager.TryGetManagedContent(codexContent, out string? regionContent)
                || CodexMcpConfigManager.ComputeManagedHash(regionContent!)
                    != CodexMcpConfigManager.ComputeManagedHash(
                        CodexMcpConfigManager.RenderManagedContent(BuildUnionServers(servers, ownServers, otherManifests))))
            {
                driftedCodexConfigs++;
                result.Conflicts.Add($"Managed Codex MCP region content drifted: {manifest.ManagedCodexMcp.Path}");
            }
            else
            {
                healthyCodexConfigs++;
            }
        }

        result.Messages.Add($"Bundle: {bundle.BundleId}");
        result.Messages.Add($"Manifest: {manifestPath}");
        result.Messages.Add($"Files healthy: {healthyFiles}, drifted: {driftedFiles}");
        result.Messages.Add($"Managed blocks healthy: {healthyBlocks}, drifted: {driftedBlocks}");
        result.Messages.Add($"Owned MCP servers healthy: {healthyServers}, drifted: {driftedServers}, co-owned with another bundle: {coOwnedServers}");
        result.Messages.Add($"Codex MCP configs healthy: {healthyCodexConfigs}, drifted: {driftedCodexConfigs}");

        if (otherManifests.Count > 0)
        {
            result.Messages.Add($"Other bundles installed here: {string.Join(", ", otherManifests.Select(x => x.BundleId).Order(StringComparer.OrdinalIgnoreCase))}");
        }

        result.Success = result.Conflicts.Count == 0;
        return result;
    }

    public OperationResult Remove(CommandOptions options)
    {
        Guard.IsNotNull(options);
        string targetRoot = ResolveTargetRoot(options.TargetPath);
        AiBundleDefinition bundle = LoadBundleDefinition();
        string manifestPath = GetManifestPath(targetRoot, bundle.BundleId);
        var result = new OperationResult();

        if (!File.Exists(manifestPath))
        {
            result.Success = false;
            result.Conflicts.Add($"Bundle '{bundle.BundleId}' is not installed in {targetRoot}.");
            return result;
        }

        AiBundleManifest manifest = LoadManifest(manifestPath)!;
        List<AiBundleManifest> survivingManifests = [.. LoadAllManifests(targetRoot).Where(x => !IsThisBundle(x, bundle))];

        foreach (PathHashRecord record in manifest.ManagedFiles)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(record.Path));

            if (File.Exists(targetPath) && !options.Force && HashUtility.ComputeFileHash(targetPath) != record.Hash)
            {
                result.Conflicts.Add($"Managed file was modified: {record.Path}");
            }
        }

        foreach (PathHashRecord record in manifest.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(record.Path));

            if (TryGetManagedBlock(targetPath, bundle.BundleId, out string? content)
                && !options.Force
                && HashUtility.ComputeStringHash(content!) != record.Hash)
            {
                result.Conflicts.Add($"Managed doc block was modified: {record.Path}");
            }
        }

        string mcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject? mcpRoot = LoadMcpRoot(mcpPath);
        JsonObject? servers = mcpRoot is null ? null : GetOrCreateServers(mcpRoot);
        JsonObject? legacyServers = mcpRoot is null ? null : GetOrCreateMcpServers(mcpRoot);

        foreach (NameHashRecord record in manifest.ManagedMcpServers)
        {
            JsonNode? serverNode = servers?[record.Name];

            if (serverNode is not null && !options.Force && HashUtility.ComputeJsonHash(serverNode) != record.Hash)
            {
                result.Conflicts.Add($"Managed MCP server was modified: {record.Name}");
            }
        }

        string codexPath = Path.Combine(targetRoot, NormalizePath(manifest.ManagedCodexMcp?.Path ?? CodexMcpConfigManager.RelativePath));

        // A repository installed by an earlier tool version still carries a legacy per-bundle region.
        // Absorb it exactly as install and update do, so removal neither reads it as a missing shared
        // region nor leaves its server tables behind.
        string codexContent = CodexMcpConfigManager.AbsorbLegacyRegions(
            File.Exists(codexPath) ? File.ReadAllText(codexPath) : string.Empty,
            out bool absorbedLegacyCodex);
        bool migratingCodex = absorbedLegacyCodex && !CodexMcpConfigManager.TryGetManagedContent(codexContent, out _);

        if (manifest.ManagedCodexMcp is not null && !options.Force && !migratingCodex)
        {
            if (!CodexMcpConfigManager.TryGetManagedServerNames(codexContent, out HashSet<string> regionNames, out string? regionError))
            {
                result.Conflicts.Add(regionError is null
                    ? $"Managed Codex MCP region was removed: {manifest.ManagedCodexMcp.Path}"
                    : $"Managed Codex MCP region could not be parsed: {regionError}");
            }
            else if (manifest.ManagedMcpServers.Any(x => !regionNames.Contains(x.Name)))
            {
                result.Conflicts.Add($"Managed Codex MCP region no longer declares all owned servers: {manifest.ManagedCodexMcp.Path}");
            }
        }

        if (result.Conflicts.Count > 0)
        {
            result.Success = false;
            return result;
        }

        foreach (PathHashRecord record in manifest.ManagedFiles)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(record.Path));

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                CleanupEmptyDirectories(Path.GetDirectoryName(targetPath), targetRoot);
                result.Messages.Add($"Removed file: {record.Path}");
            }
        }

        foreach (PathHashRecord record in manifest.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(record.Path));

            if (File.Exists(targetPath))
            {
                RemoveManagedBlock(targetPath, bundle.BundleId);
                result.Messages.Add($"Removed managed block: {record.Path}");
            }
        }

        if (servers is not null)
        {
            foreach (NameHashRecord record in manifest.ManagedMcpServers)
            {
                if (survivingManifests.Any(x => OwnsServer(x, record.Name)))
                {
                    result.Messages.Add($"Retained co-owned MCP server: {record.Name}");
                    continue;
                }

                _ = servers.Remove(record.Name);
                _ = legacyServers?.Remove(record.Name);
                result.Messages.Add($"Removed MCP server: {record.Name}");
            }

            if (servers.Count == 0 && (legacyServers?.Count ?? 0) == 0 && options.CleanEmptyMcp)
            {
                File.Delete(mcpPath);
                result.Messages.Add("Removed .mcp.json because it became empty.");
            }
            else if (File.Exists(mcpPath))
            {
                SaveMcpJson(mcpPath, mcpRoot!);
            }
        }

        if (manifest.ManagedCodexMcp is not null && File.Exists(codexPath))
        {
            // Re-render the shared region from whatever the surviving bundles still own.
            JsonObject remainingUnion = RestrictServers(servers, survivingManifests.SelectMany(x => x.ManagedMcpServers).Select(x => x.Name));
            string updatedCodexContent;

            if (remainingUnion.Count == 0)
            {
                updatedCodexContent = CodexMcpConfigManager.RemoveManagedRegion(codexContent);
                result.Messages.Add($"Removed Codex MCP region: {manifest.ManagedCodexMcp.Path}");
            }
            else if (CodexMcpConfigManager.TryBuildUpdatedConfig(codexContent, remainingUnion, remainingUnion,
                expectedOwnHash: null, force: true, allowUntrackedManagedBlockReplacement: true,
                previouslyOwnedServers: manifest.ManagedMcpServers.Select(x => x.Name),
                out updatedCodexContent, out List<string> codexConflicts))
            {
                result.Messages.Add($"Rebuilt Codex MCP region for remaining bundles: {manifest.ManagedCodexMcp.Path}");
            }
            else
            {
                result.Success = false;
                result.Conflicts.AddRange(codexConflicts);
                return result;
            }

            if (string.IsNullOrWhiteSpace(updatedCodexContent) && options.CleanEmptyMcp)
            {
                File.Delete(codexPath);
                CleanupEmptyDirectories(Path.GetDirectoryName(codexPath), targetRoot);
            }
            else
            {
                File.WriteAllText(codexPath, updatedCodexContent);
            }
        }

        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
            CleanupEmptyDirectories(Path.GetDirectoryName(manifestPath), targetRoot);
        }

        result.Success = true;
        return result;
    }

    private OperationResult InstallOrUpdate(CommandOptions options, bool requireExistingManifest, string operationName)
    {
        string targetRoot = ResolveTargetRoot(options.TargetPath);

        if (!options.AllowNonRepo && !LooksLikeRepository(targetRoot))
        {
            return Failure($"Target path '{targetRoot}' does not look like a repository. Use --allow-non-repo to override.");
        }

        AiBundleDefinition bundle = LoadBundleDefinition();
        string manifestPath = GetManifestPath(targetRoot, bundle.BundleId);
        AiBundleManifest? currentManifest = File.Exists(manifestPath) ? LoadManifest(manifestPath) : null;

        if (requireExistingManifest && currentManifest is null)
        {
            return Failure($"Bundle '{bundle.BundleId}' is not installed in {targetRoot}. Run install first.");
        }

        List<AiBundleManifest> otherManifests = [.. LoadAllManifests(targetRoot).Where(x => !IsThisBundle(x, bundle))];

        Dictionary<string, ManagedFileEntry> sourceFiles = EnumerateAllManagedFiles(bundle);
        JsonObject sourceServers = LoadSourceServers(bundle.McpSourcePath);
        var result = new OperationResult();

        ValidateManagedFiles(targetRoot, sourceFiles, currentManifest, otherManifests, options, result);
        ValidateManagedBlocks(targetRoot, bundle, currentManifest, options, result);
        ValidateManagedMcp(targetRoot, sourceServers, currentManifest, otherManifests, options, result);

        string targetMcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject targetMcpRoot = LoadMcpRoot(targetMcpPath) ?? [];
        JsonObject targetServers = GetOrCreateServers(targetMcpRoot);
        JsonObject targetMcpServers = GetOrCreateMcpServers(targetMcpRoot);

        // Project the post-merge server set so the Codex region can be validated and rendered before
        // anything is written. Every conflict must be known before the first mutation.
        JsonObject projectedServers = targetServers.DeepClone().AsObject();
        List<string> staleServerNames = currentManifest is null
            ? []
            : [.. currentManifest.ManagedMcpServers.Select(x => x.Name).Where(x => !sourceServers.ContainsKey(x))];

        // A bundle that contributes no MCP servers has no MCP responsibilities: it must not create an
        // empty .mcp.json or an empty Codex region in the target repository. Servers it used to own
        // still need cleaning up, so a pending removal keeps it in the MCP path.
        bool managesMcp = sourceServers.Count > 0 || staleServerNames.Count > 0;

        foreach (string staleServerName in staleServerNames)
        {
            // A stale server another bundle still owns stays in .mcp.json, so it must stay in the
            // projection too: the shared Codex region has to keep declaring it for that bundle.
            if (!otherManifests.Any(x => OwnsServer(x, staleServerName)))
            {
                _ = projectedServers.Remove(staleServerName);
            }
        }

        foreach ((string serverName, JsonNode? serverNode) in sourceServers)
        {
            if (serverNode is not null)
            {
                projectedServers[serverName] = serverNode.DeepClone();
            }
        }

        JsonObject unionServers = BuildUnionServers(projectedServers, sourceServers, otherManifests);
        JsonObject ownServers = RestrictServers(projectedServers, sourceServers.Select(x => x.Key));

        string targetCodexPath = Path.Combine(targetRoot, NormalizePath(CodexMcpConfigManager.RelativePath));
        string existingCodexConfig = File.Exists(targetCodexPath) ? File.ReadAllText(targetCodexPath) : string.Empty;
        string updatedCodexConfig = existingCodexConfig;

        if (managesMcp
            && !CodexMcpConfigManager.TryBuildUpdatedConfig(existingCodexConfig, unionServers, ownServers,
                currentManifest?.ManagedCodexMcp?.Hash, options.Force, allowUntrackedManagedBlockReplacement: false,
                previouslyOwnedServers: currentManifest?.ManagedMcpServers.Select(x => x.Name) ?? [],
                out updatedCodexConfig, out List<string> codexConflicts))
        {
            result.Conflicts.AddRange(codexConflicts);
        }

        if (result.Conflicts.Count > 0)
        {
            result.Success = false;
            return result;
        }

        var newManifest = new AiBundleManifest
        {
            BundleId = bundle.BundleId,
            BundleVersion = _options.InstallerVersion,
            InstallerPackageId = _options.InstallerPackageId,
            InstallerVersion = _options.InstallerVersion,
            InstalledAt = DateTimeOffset.UtcNow
        };

        foreach ((string relativePath, ManagedFileEntry entry) in sourceFiles)
        {
            string targetPath = Path.Combine(targetRoot, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            WriteFile(targetPath, entry);
            newManifest.ManagedFiles.Add(new PathHashRecord { Path = relativePath, Hash = HashUtility.ComputeFileHash(targetPath) });
            result.Messages.Add($"Managed file {operationName}ed: {relativePath}");
        }

        foreach (StarterFileDefinition starter in bundle.StarterFiles)
        {
            if (string.IsNullOrWhiteSpace(starter.SourcePath) || string.IsNullOrWhiteSpace(starter.TargetPath))
            {
                result.Messages.Add("Skipped starter file with an empty sourcePath or targetPath.");
                continue;
            }

            string starterTargetPath = Path.Combine(targetRoot, NormalizePath(starter.TargetPath));

            if (!File.Exists(starterTargetPath))
            {
                CopyTextAsset(starter.SourcePath, starterTargetPath);
                result.Messages.Add($"Created starter file: {NormalizePath(starter.TargetPath)}");
            }
        }

        foreach (ManagedBlockDefinition block in bundle.ManagedBlocks)
        {
            string fragmentContent = ReadTextAsset(block.SourcePath);
            string targetPath = Path.Combine(targetRoot, NormalizePath(block.TargetPath));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            SetManagedBlock(targetPath, bundle.BundleId, fragmentContent);
            newManifest.ManagedBlocks.Add(new PathHashRecord { Path = NormalizePath(block.TargetPath), Hash = HashUtility.ComputeStringHash(fragmentContent.TrimEnd()) });
            result.Messages.Add($"Managed doc block {operationName}ed: {NormalizePath(block.TargetPath)}");
        }

        if (managesMcp)
        {
            foreach (string staleServerName in staleServerNames)
            {
                if (otherManifests.Any(x => OwnsServer(x, staleServerName)))
                {
                    result.Messages.Add($"Retained co-owned MCP server no longer in this bundle: {staleServerName}");
                    continue;
                }

                _ = targetServers.Remove(staleServerName);
                _ = targetMcpServers.Remove(staleServerName);
                result.Messages.Add($"Removed obsolete managed MCP server: {staleServerName}");
            }

            foreach ((string serverName, JsonNode? serverNode) in sourceServers)
            {
                if (serverNode is null)
                {
                    continue;
                }

                targetServers[serverName] = serverNode.DeepClone();
                newManifest.ManagedMcpServers.Add(new NameHashRecord { Name = serverName, Hash = HashUtility.ComputeJsonHash(serverNode) });
                targetMcpServers[serverName] = serverNode.DeepClone();

                string coOwnership = otherManifests.Any(x => OwnsServer(x, serverName)) ? " (co-owned)" : string.Empty;
                result.Messages.Add($"Managed MCP server {operationName}ed: {serverName}{coOwnership}");
            }

            SaveMcpJson(targetMcpPath, targetMcpRoot);

            _ = Directory.CreateDirectory(Path.GetDirectoryName(targetCodexPath)!);
            File.WriteAllText(targetCodexPath, updatedCodexConfig);
            newManifest.ManagedCodexMcp = new PathHashRecord
            {
                Path = NormalizePath(CodexMcpConfigManager.RelativePath),
                Hash = CodexMcpConfigManager.ComputeManagedHash(CodexMcpConfigManager.RenderManagedContent(ownServers))
            };
            result.Messages.Add($"Managed Codex MCP config {operationName}ed: {CodexMcpConfigManager.RelativePath}");
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(newManifest, _serializerOptions));
        result.Messages.Add($"Wrote manifest: {manifestPath}");
        result.Success = true;
        return result;
    }

    /// <summary>
    /// Builds the set of servers rendered into the shared Codex region: everything owned by this bundle
    /// plus everything still owned by any other installed bundle. Definitions come from the merged
    /// <c>.mcp.json</c>, which is the canonical source.
    /// </summary>
    private static JsonObject BuildUnionServers(JsonObject? mergedServers, JsonObject ownServers, List<AiBundleManifest> otherManifests)
    {
        var ownedNames = new HashSet<string>(ownServers.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);

        foreach (AiBundleManifest manifest in otherManifests)
        {
            ownedNames.UnionWith(manifest.ManagedMcpServers.Select(x => x.Name));
        }

        return RestrictServers(mergedServers, ownedNames);
    }

    private static JsonObject RestrictServers(JsonObject? servers, IEnumerable<string> serverNames)
    {
        var names = new HashSet<string>(serverNames, StringComparer.OrdinalIgnoreCase);
        var restricted = new JsonObject();

        if (servers is null)
        {
            return restricted;
        }

        foreach ((string serverName, JsonNode? serverNode) in servers)
        {
            if (names.Contains(serverName) && serverNode is not null)
            {
                restricted[serverName] = serverNode.DeepClone();
            }
        }

        return restricted;
    }

    private static bool OwnsServer(AiBundleManifest manifest, string serverName)
        => manifest.ManagedMcpServers.Any(x => x.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

    private static bool IsThisBundle(AiBundleManifest manifest, AiBundleDefinition bundle)
        => manifest.BundleId.Equals(bundle.BundleId, StringComparison.OrdinalIgnoreCase);

    private static void ValidateManagedFiles(string targetRoot, Dictionary<string, ManagedFileEntry> sourceFiles, AiBundleManifest? currentManifest, List<AiBundleManifest> otherManifests, CommandOptions options, OperationResult result)
    {
        foreach ((string relativePath, ManagedFileEntry entry) in sourceFiles)
        {
            string normalizedRelativePath = NormalizePath(relativePath);
            string targetPath = Path.Combine(targetRoot, normalizedRelativePath);
            AiBundleManifest? otherOwner = otherManifests.FirstOrDefault(x => x.ManagedFiles.Any(y => PathEquals(y.Path, normalizedRelativePath)));
            PathHashRecord? currentRecord = currentManifest?.ManagedFiles.FirstOrDefault(x => PathEquals(x.Path, normalizedRelativePath));

            if (otherOwner is not null && !options.Force)
            {
                result.Conflicts.Add($"Managed file is owned by bundle '{otherOwner.BundleId}': {normalizedRelativePath}");
                continue;
            }

            if (!File.Exists(targetPath))
            {
                continue;
            }

            string targetHash = HashUtility.ComputeFileHash(targetPath);

            if (currentRecord is not null)
            {
                if (!options.Force && targetHash != currentRecord.Hash)
                {
                    result.Conflicts.Add($"Managed file was modified: {normalizedRelativePath}");
                }

                continue;
            }

            if (!options.Force && targetHash != ComputeInstallHash(entry))
            {
                result.Conflicts.Add($"Unowned file already exists: {normalizedRelativePath}");
            }
        }
    }

    private static string ApplySubstitutions(ManagedFileEntry entry)
    {
        string content = File.ReadAllText(entry.SourcePath);

        foreach ((string token, string replacement) in entry.Substitutions!)
        {
            content = content.Replace(token, replacement, StringComparison.Ordinal);
        }

        return content;
    }

    private static string ComputeInstallHash(ManagedFileEntry entry)
        => entry.Substitutions?.Count > 0
            ? HashUtility.ComputeStringHash(ApplySubstitutions(entry))
            : HashUtility.ComputeFileHash(entry.SourcePath);

    private static void WriteFile(string targetPath, ManagedFileEntry entry)
    {
        if (entry.Substitutions?.Count > 0)
        {
            File.WriteAllText(targetPath, ApplySubstitutions(entry));
        }
        else
        {
            File.Copy(entry.SourcePath, targetPath, overwrite: true);
        }
    }

    private static byte[] ComputeTargetBytes(ManagedFileEntry entry)
        => entry.Substitutions?.Count > 0
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(ApplySubstitutions(entry))
            : File.ReadAllBytes(entry.SourcePath);

    private static bool WriteFileIfChanged(string targetPath, ManagedFileEntry entry)
    {
        byte[] expected = ComputeTargetBytes(entry);

        if (File.Exists(targetPath) && expected.AsSpan().SequenceEqual(File.ReadAllBytes(targetPath)))
        {
            return false;
        }

        // Write bytes rather than File.Copy so the target always gets a fresh
        // last-write time instead of inheriting the source's timestamp.
        File.WriteAllBytes(targetPath, expected);
        return true;
    }

    private static void ValidateManagedBlocks(string targetRoot, AiBundleDefinition bundle, AiBundleManifest? currentManifest, CommandOptions options, OperationResult result)
    {
        foreach (ManagedBlockDefinition block in bundle.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, NormalizePath(block.TargetPath));
            PathHashRecord? currentRecord = currentManifest?.ManagedBlocks.FirstOrDefault(x => PathEquals(x.Path, NormalizePath(block.TargetPath)));

            if (TryGetManagedBlock(targetPath, bundle.BundleId, out string? content)
                && currentRecord is not null
                && !options.Force
                && HashUtility.ComputeStringHash(content!) != currentRecord.Hash)
            {
                result.Conflicts.Add($"Managed doc block was modified: {NormalizePath(block.TargetPath)}");
            }
        }
    }

    private static void ValidateManagedMcp(string targetRoot, JsonObject sourceServers, AiBundleManifest? currentManifest, List<AiBundleManifest> otherManifests, CommandOptions options, OperationResult result)
    {
        string mcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject? mcpRoot = LoadMcpRoot(mcpPath);
        JsonObject? targetServers = mcpRoot is null ? null : GetOrCreateServers(mcpRoot);

        if (targetServers is null)
        {
            return;
        }

        foreach ((string serverName, JsonNode? templateNode) in sourceServers)
        {
            if (templateNode is null)
            {
                continue;
            }

            JsonNode? existingNode = targetServers[serverName];
            AiBundleManifest? otherOwner = otherManifests.FirstOrDefault(x => OwnsServer(x, serverName));
            NameHashRecord? currentRecord = currentManifest?.ManagedMcpServers.FirstOrDefault(x => x.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            string templateHash = HashUtility.ComputeJsonHash(templateNode);

            // A server owned by another bundle is co-ownable when both bundles describe it identically.
            // Only a genuine disagreement about the definition needs the user to intervene, so an entry
            // missing from .mcp.json is not one: there is nothing to disagree with, and it is recreated.
            if (otherOwner is not null
                && !options.Force
                && existingNode is not null
                && HashUtility.ComputeJsonHash(existingNode) != templateHash)
            {
                result.Conflicts.Add(
                    $"MCP server '{serverName}' is owned by bundle '{otherOwner.BundleId}' with a different definition. "
                    + "Align the definitions in both bundles, or use --force to take ownership.");
                continue;
            }

            if (existingNode is null)
            {
                continue;
            }

            string existingHash = HashUtility.ComputeJsonHash(existingNode);

            if (currentRecord is not null)
            {
                if (!options.Force && existingHash != currentRecord.Hash)
                {
                    result.Conflicts.Add($"Managed MCP server was modified: {serverName}");
                }

                continue;
            }

            if (!options.Force && existingHash != templateHash)
            {
                result.Conflicts.Add($"Unowned MCP server already exists: {serverName}");
            }
        }

        if (currentManifest is null)
        {
            return;
        }

        foreach (NameHashRecord staleRecord in currentManifest.ManagedMcpServers.Where(x => !sourceServers.ContainsKey(x.Name)))
        {
            JsonNode? existingNode = targetServers[staleRecord.Name];

            if (existingNode is not null
                && !options.Force
                && HashUtility.ComputeJsonHash(existingNode) != staleRecord.Hash)
            {
                result.Conflicts.Add($"Managed MCP server was modified: {staleRecord.Name}");
            }
        }
    }

    private AiBundleDefinition LoadBundleDefinition()
    {
        string definitionPath = ResolveAssetPath(BundleDefinitionRelativePath);
        return JsonSerializer.Deserialize<AiBundleDefinition>(File.ReadAllText(definitionPath), _serializerOptions)
            ?? throw new InvalidOperationException($"Failed to read bundle definition at {definitionPath}.");
    }

    private List<AiBundleManifest> LoadAllManifests(string targetRoot)
    {
        string bundlesRoot = Path.Combine(targetRoot, ".ai-shared", "bundles");

        if (!Directory.Exists(bundlesRoot))
        {
            return [];
        }

        return [.. Directory.GetFiles(bundlesRoot, "manifest.json", SearchOption.AllDirectories)
            .Select(LoadManifest)
            .Where(x => x is not null)
            .Cast<AiBundleManifest>()];
    }

    private AiBundleManifest? LoadManifest(string manifestPath)
        => JsonSerializer.Deserialize<AiBundleManifest>(File.ReadAllText(manifestPath), _serializerOptions);

    private Dictionary<string, ManagedFileEntry> EnumerateAllManagedFiles(AiBundleDefinition bundle)
    {
        var results = new Dictionary<string, ManagedFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in bundle.ManagedDirectories)
        {
            string sourceDirectory = RequireAssetDirectory(directory, "managedDirectories");

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = NormalizePath(Path.GetRelativePath(_assetRoot.Value, file));
                results[relativePath] = new ManagedFileEntry(file, null);
            }
        }

        foreach (AdapterDirectoryDefinition adapter in bundle.AdapterDirectories)
        {
            string sourceDirectory = RequireAssetDirectory(adapter.Source, "adapterDirectories");

            foreach (AdapterTarget target in adapter.Targets)
            {
                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativeToSource = Path.GetRelativePath(sourceDirectory, file);
                    string relativePath = NormalizePath(Path.Combine(target.Destination, relativeToSource));

                    if (results.ContainsKey(relativePath))
                    {
                        throw new InvalidOperationException($"Adapter path collision: multiple adapter targets produce '{relativePath}'. Review adapterDirectories in bundle.json.");
                    }

                    results[relativePath] = new ManagedFileEntry(file, target.Substitutions);
                }
            }
        }

        return results;
    }

    private string RequireAssetDirectory(string relativePath, string bundleProperty)
    {
        string resolved = ResolveAssetPath(relativePath);

        return Directory.Exists(resolved)
            ? resolved
            : throw new InvalidOperationException(
                $"Bundle source directory '{NormalizePath(relativePath)}' declared in {bundleProperty} was not found under asset root '{_assetRoot.Value}'.");
    }

    private sealed record ManagedFileEntry(string SourcePath, Dictionary<string, string>? Substitutions);

    private static string ResolveTargetRoot(string targetPath) => Path.GetFullPath(targetPath);

    private string ResolveAssetPath(string relativePath) => Path.Combine(_assetRoot.Value, NormalizePath(relativePath));

    private static bool LooksLikeRepository(string path)
        => Directory.Exists(Path.Combine(path, ".git"))
        || Directory.GetFiles(path, "*.sln", SearchOption.TopDirectoryOnly).Length > 0
        || File.Exists(Path.Combine(path, "Directory.Build.props"));

    private static string NormalizePath(string path)
        => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static bool PathEquals(string left, string right)
        => NormalizePath(left).Equals(NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static string GetManifestPath(string targetRoot, string bundleId)
        => Path.Combine(targetRoot, ".ai-shared", "bundles", bundleId, "manifest.json");

    private static OperationResult Failure(string message)
    {
        var result = new OperationResult { Success = false };
        result.Conflicts.Add(message);
        return result;
    }

    private string ReadTextAsset(string relativePath) => File.ReadAllText(ResolveAssetPath(relativePath));

    private void CopyTextAsset(string relativePath, string targetPath)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(ResolveAssetPath(relativePath), targetPath, overwrite: true);
    }

    private static string GetStartMarker(string bundleId) => $"<!-- ai-bundle:{bundleId}:start -->";

    private static string GetEndMarker(string bundleId) => $"<!-- ai-bundle:{bundleId}:end -->";

    private static bool TryGetManagedBlock(string targetPath, string bundleId, out string? content)
    {
        content = null;

        if (!File.Exists(targetPath))
        {
            return false;
        }

        string fileContent = File.ReadAllText(targetPath);
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        int startIndex = fileContent.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = fileContent.IndexOf(endMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            return false;
        }

        int contentStart = startIndex + startMarker.Length;
        content = fileContent[contentStart..endIndex].Trim('\r', '\n');
        return true;
    }

    private static void SetManagedBlock(string targetPath, string bundleId, string content)
    {
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        string block = string.Join(Environment.NewLine, [startMarker, content.TrimEnd(), endMarker]);
        string finalContent;

        if (!File.Exists(targetPath))
        {
            finalContent = block + Environment.NewLine;
        }
        else
        {
            string existing = File.ReadAllText(targetPath).TrimEnd();
            int startIndex = existing.IndexOf(startMarker, StringComparison.Ordinal);
            int endIndex = existing.IndexOf(endMarker, StringComparison.Ordinal);

            if (startIndex >= 0 && endIndex >= 0 && endIndex >= startIndex)
            {
                int replaceLength = (endIndex + endMarker.Length) - startIndex;
                finalContent = existing.Remove(startIndex, replaceLength).Insert(startIndex, block).TrimEnd() + Environment.NewLine;
            }
            else
            {
                string separator = existing.Length == 0 ? string.Empty : Environment.NewLine + Environment.NewLine;
                finalContent = existing + separator + block + Environment.NewLine;
            }
        }

        File.WriteAllText(targetPath, finalContent);
    }

    private static void RemoveManagedBlock(string targetPath, string bundleId)
    {
        string startMarker = GetStartMarker(bundleId);
        string endMarker = GetEndMarker(bundleId);
        string existing = File.ReadAllText(targetPath);
        int startIndex = existing.IndexOf(startMarker, StringComparison.Ordinal);
        int endIndex = existing.IndexOf(endMarker, StringComparison.Ordinal);

        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            return;
        }

        int removeLength = (endIndex + endMarker.Length) - startIndex;
        string updated = existing.Remove(startIndex, removeLength).Replace(Environment.NewLine + Environment.NewLine + Environment.NewLine, Environment.NewLine + Environment.NewLine, StringComparison.Ordinal).Trim();

        if (updated.Length == 0)
        {
            File.Delete(targetPath);
            return;
        }

        File.WriteAllText(targetPath, updated + Environment.NewLine);
    }

    private JsonObject LoadSourceServers(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return [];
        }

        JsonNode node = JsonNode.Parse(File.ReadAllText(ResolveAssetPath(relativePath)))
            ?? throw new InvalidOperationException($"Failed to parse MCP source: {relativePath}");

        JsonObject root = node.AsObject();

        return root["servers"] is JsonObject servers
            ? servers
            : throw new InvalidOperationException($"MCP source '{relativePath}' must define a 'servers' object.");
    }

    private static JsonObject? LoadMcpRoot(string mcpPath)
    {
        if (!File.Exists(mcpPath))
        {
            return null;
        }

        JsonNode rootNode = JsonNode.Parse(File.ReadAllText(mcpPath))
            ?? throw new InvalidOperationException($"Failed to parse {mcpPath}.");

        return rootNode.AsObject();
    }

    private static JsonObject GetOrCreateServers(JsonObject rootObject)
    {
        rootObject["servers"] ??= new JsonObject();
        return rootObject["servers"]!.AsObject();
    }

    private static JsonObject GetOrCreateMcpServers(JsonObject rootObject)
    {
        rootObject["mcpServers"] ??= new JsonObject();
        return rootObject["mcpServers"]!.AsObject();
    }

    private static void SaveMcpJson(string mcpPath, JsonObject root)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(mcpPath)!);
        File.WriteAllText(mcpPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CleanupEmptyDirectories(string? startingDirectory, string targetRoot)
    {
        if (string.IsNullOrWhiteSpace(startingDirectory))
        {
            return;
        }

        string normalizedRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar);
        var directory = new DirectoryInfo(startingDirectory);

        while (directory is not null && directory.Exists && directory.FullName.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (directory.EnumerateFileSystemInfos().Any())
            {
                return;
            }

            DirectoryInfo? parent = directory.Parent;
            directory.Delete();
            directory = parent;
        }
    }

    [GeneratedRegex(@"^(\w+):\s*(.+?)\s*$")]
    private static partial Regex FrontmatterPropertyRegex();
}
