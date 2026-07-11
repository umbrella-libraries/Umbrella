using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Umbrella.AI.Tools.Models;

namespace Umbrella.AI.Tools.Services;

public sealed class AiBundleInstaller(string assetRoot, string installerPackageId, string installerVersion)
{
    private const string BundleDefinitionRelativePath = ".ai-shared\\bundles\\umbrella\\bundle.json";
    private readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public OperationResult Install(CommandOptions options) => InstallOrUpdate(options, requireExistingManifest: false, operationName: "install");

    public OperationResult Update(CommandOptions options) => InstallOrUpdate(options, requireExistingManifest: true, operationName: "update");

    public OperationResult Sync(string startDirectory)
    {
        string? repoRoot = LocateRepoRoot(startDirectory);

        if (repoRoot is null)
        {
            return Failure($"Could not locate '{NormalizePath(BundleDefinitionRelativePath)}' in '{Path.GetFullPath(startDirectory)}' or any parent directory. Run sync from within an installed repository or pass --root-dir <repo-root>.");
        }

        string bundleDefPath = Path.Combine(repoRoot, NormalizePath(BundleDefinitionRelativePath));
        AiBundleDefinition bundle = JsonSerializer.Deserialize<AiBundleDefinition>(File.ReadAllText(bundleDefPath), _serializerOptions)
            ?? throw new InvalidOperationException($"Failed to read bundle definition at {bundleDefPath}.");

        var result = new OperationResult { Success = true };
        result.Messages.Add($"Repository root: {repoRoot}");
        var syncedFiles = new List<(ManagedFileEntry Entry, string TargetPath, string DisplayPath)>();

        foreach (AdapterDirectoryDefinition adapter in bundle.AdapterDirectories)
        {
            string sourceDirectory = Path.Combine(repoRoot, NormalizePath(adapter.Source));

            foreach (AdapterTarget target in adapter.Targets)
            {
                string targetDirectory = Path.Combine(repoRoot, NormalizePath(target.Destination));

                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativeToSource = Path.GetRelativePath(sourceDirectory, file);
                    string targetPath = Path.Combine(targetDirectory, relativeToSource);
                    string displayPath = NormalizePath(Path.Combine(target.Destination, relativeToSource));
                    var entry = new ManagedFileEntry(file, target.Substitutions);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
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
                    ?.Source ?? ".ai-shared\\skills"));
            List<(string Name, string Description)> skills = ReadSkillMetadata(skillsSource);

            foreach (SkillListBlockDefinition blockConfig in bundle.SkillListBlocks)
            {
                string blockPath = Path.Combine(repoRoot, NormalizePath(blockConfig.TargetPath));
                Directory.CreateDirectory(Path.GetDirectoryName(blockPath)!);
                File.WriteAllText(blockPath, GenerateSkillListBlock(skills, blockConfig.SkillsDirectory));
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
        if (File.Exists(manifestPath))
        {
            AiBundleManifest manifest = LoadManifest(manifestPath)!;
            foreach (PathHashRecord record in manifest.ManagedFiles)
            {
                string targetPath = Path.Combine(repoRoot, NormalizePath(record.Path));
                if (File.Exists(targetPath))
                    record.Hash = HashUtility.ComputeFileHash(targetPath);
            }

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, _serializerOptions));
            result.Messages.Add($"Refreshed manifest hashes: {manifestPath}");
        }

        return result;
    }

    private static string? LocateRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, NormalizePath(BundleDefinitionRelativePath))))
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

            string name = "", description = "";
            bool inFrontmatter = false;

            foreach (string line in File.ReadLines(skillMdPath))
            {
                if (line.Trim() == "---")
                {
                    if (!inFrontmatter)
                    {
                        inFrontmatter = true;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!inFrontmatter)
                {
                    break;
                }

                Match match = Regex.Match(line, @"^(\w+):\s*(.+?)\s*$");
                if (match.Success)
                {
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
            }

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description))
            {
                skills.Add((name, description));
            }
        }

        return [.. skills.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static string GenerateSkillListBlock(List<(string Name, string Description)> skills, string skillsDirectory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Umbrella Skills");
        sb.AppendLine();
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"The following skills are available in `{skillsDirectory}`. Read a skill's `SKILL.md` for full instructions before using it.");
        sb.AppendLine();

        foreach ((string name, string description) in skills)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- `{name}` -- {description}");
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    public OperationResult GetStatus(CommandOptions options)
    {
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
            string targetPath = Path.Combine(targetRoot, fileRecord.Path);

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
            string targetPath = Path.Combine(targetRoot, blockRecord.Path);

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

        foreach (NameHashRecord serverRecord in manifest.ManagedMcpServers)
        {
            JsonNode? serverNode = servers?[serverRecord.Name];

            if (serverNode is not null && HashUtility.ComputeJsonHash(serverNode) == serverRecord.Hash)
            {
                healthyServers++;
            }
            else
            {
                driftedServers++;
                result.Conflicts.Add($"Managed MCP server drifted: {serverRecord.Name}");
            }
        }

        result.Messages.Add($"Bundle: {bundle.BundleId}");
        result.Messages.Add($"Manifest: {manifestPath}");
        result.Messages.Add($"Files healthy: {healthyFiles}, drifted: {driftedFiles}");
        result.Messages.Add($"Managed blocks healthy: {healthyBlocks}, drifted: {driftedBlocks}");
        result.Messages.Add($"Owned MCP servers healthy: {healthyServers}, drifted: {driftedServers}");
        result.Success = result.Conflicts.Count == 0;
        return result;
    }

    public OperationResult Remove(CommandOptions options)
    {
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

        foreach (PathHashRecord record in manifest.ManagedFiles)
        {
            string targetPath = Path.Combine(targetRoot, record.Path);

            if (File.Exists(targetPath) && !options.Force && HashUtility.ComputeFileHash(targetPath) != record.Hash)
            {
                result.Conflicts.Add($"Managed file was modified: {record.Path}");
            }
        }

        foreach (PathHashRecord record in manifest.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, record.Path);

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

        foreach (NameHashRecord record in manifest.ManagedMcpServers)
        {
            JsonNode? serverNode = servers?[record.Name];

            if (serverNode is not null && !options.Force && HashUtility.ComputeJsonHash(serverNode) != record.Hash)
            {
                result.Conflicts.Add($"Managed MCP server was modified: {record.Name}");
            }
        }

        if (result.Conflicts.Count > 0)
        {
            result.Success = false;
            return result;
        }

        foreach (PathHashRecord record in manifest.ManagedFiles)
        {
            string targetPath = Path.Combine(targetRoot, record.Path);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
                CleanupEmptyDirectories(Path.GetDirectoryName(targetPath), targetRoot);
                result.Messages.Add($"Removed file: {record.Path}");
            }
        }

        foreach (PathHashRecord record in manifest.ManagedBlocks)
        {
            string targetPath = Path.Combine(targetRoot, record.Path);

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
                _ = servers.Remove(record.Name);
                result.Messages.Add($"Removed MCP server: {record.Name}");
            }

            if (servers.Count == 0 && options.CleanEmptyMcp)
            {
                File.Delete(mcpPath);
                result.Messages.Add("Removed .mcp.json because it became empty.");
            }
            else if (File.Exists(mcpPath))
            {
                SaveMcpJson(mcpPath, mcpRoot!);
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

        var otherManifests = LoadAllManifests(targetRoot)
            .Where(x => !x.BundleId.Equals(bundle.BundleId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Dictionary<string, ManagedFileEntry> sourceFiles = EnumerateAllManagedFiles(bundle);
        var result = new OperationResult();

        ValidateManagedFiles(targetRoot, sourceFiles, currentManifest, otherManifests, options, result);
        ValidateManagedBlocks(targetRoot, bundle, currentManifest, options, result);
        ValidateManagedMcp(targetRoot, bundle, currentManifest, otherManifests, options, result);

        if (result.Conflicts.Count > 0)
        {
            result.Success = false;
            return result;
        }

        var newManifest = new AiBundleManifest
        {
            BundleId = bundle.BundleId,
            BundleVersion = installerVersion,
            InstallerPackageId = installerPackageId,
            InstallerVersion = installerVersion,
            InstalledAt = DateTimeOffset.UtcNow
        };

        foreach ((string relativePath, ManagedFileEntry entry) in sourceFiles)
        {
            string targetPath = Path.Combine(targetRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            WriteFile(targetPath, entry);
            newManifest.ManagedFiles.Add(new PathHashRecord { Path = relativePath, Hash = HashUtility.ComputeFileHash(targetPath) });
            result.Messages.Add($"Managed file {operationName}ed: {relativePath}");
        }

        string exclusionsPath = Path.Combine(targetRoot, "nuget-upgrade-exclusions.json");

        if (!File.Exists(exclusionsPath))
        {
            CopyTextAsset(bundle.ExclusionsStarterPath, exclusionsPath);
            result.Messages.Add("Created nuget-upgrade-exclusions.json starter file.");
        }

        foreach (ManagedBlockDefinition block in bundle.ManagedBlocks)
        {
            string fragmentContent = ReadTextAsset(block.SourcePath);
            string targetPath = Path.Combine(targetRoot, NormalizePath(block.TargetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            SetManagedBlock(targetPath, bundle.BundleId, fragmentContent);
            newManifest.ManagedBlocks.Add(new PathHashRecord { Path = NormalizePath(block.TargetPath), Hash = HashUtility.ComputeStringHash(fragmentContent.TrimEnd()) });
            result.Messages.Add($"Managed doc block {operationName}ed: {NormalizePath(block.TargetPath)}");
        }

        JsonObject templateServers = LoadTemplateServers(bundle.McpTemplatePath);
        string targetMcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject targetMcpRoot = LoadMcpRoot(targetMcpPath) ?? [];
        JsonObject targetServers = GetOrCreateServers(targetMcpRoot);
        JsonObject targetMcpServers = GetOrCreateMcpServers(targetMcpRoot);

        foreach ((string serverName, JsonNode? serverNode) in templateServers)
        {
            if (serverNode is null)
            {
                continue;
            }

            targetServers[serverName] = serverNode.DeepClone();
            newManifest.ManagedMcpServers.Add(new NameHashRecord { Name = serverName, Hash = HashUtility.ComputeJsonHash(serverNode) });
            targetMcpServers[serverName] = serverNode.DeepClone();
            result.Messages.Add($"Managed MCP server {operationName}ed: {serverName}");
        }

        SaveMcpJson(targetMcpPath, targetMcpRoot);

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(newManifest, _serializerOptions));
        result.Messages.Add($"Wrote manifest: {manifestPath}");
        result.Success = true;
        return result;
    }

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
                result.Conflicts.Add($"Managed file is owned by another bundle: {normalizedRelativePath}");
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
            content = content.Replace(token, replacement, StringComparison.Ordinal);
        return content;
    }

    private static string ComputeInstallHash(ManagedFileEntry entry)
    {
        if (entry.Substitutions?.Count > 0)
            return HashUtility.ComputeStringHash(ApplySubstitutions(entry));
        return HashUtility.ComputeFileHash(entry.SourcePath);
    }

    private static void WriteFile(string targetPath, ManagedFileEntry entry)
    {
        if (entry.Substitutions?.Count > 0)
            File.WriteAllText(targetPath, ApplySubstitutions(entry));
        else
            File.Copy(entry.SourcePath, targetPath, overwrite: true);
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

    private void ValidateManagedMcp(string targetRoot, AiBundleDefinition bundle, AiBundleManifest? currentManifest, List<AiBundleManifest> otherManifests, CommandOptions options, OperationResult result)
    {
        string mcpPath = Path.Combine(targetRoot, ".mcp.json");
        JsonObject templateServers = LoadTemplateServers(bundle.McpTemplatePath);
        JsonObject? mcpRoot = LoadMcpRoot(mcpPath);
        JsonObject? targetServers = mcpRoot is null ? null : GetOrCreateServers(mcpRoot);

        if (targetServers is null)
        {
            return;
        }

        foreach ((string serverName, JsonNode? templateNode) in templateServers)
        {
            if (templateNode is null)
            {
                continue;
            }

            JsonNode? existingNode = targetServers[serverName];
            AiBundleManifest? otherOwner = otherManifests.FirstOrDefault(x => x.ManagedMcpServers.Any(y => y.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase)));
            NameHashRecord? currentRecord = currentManifest?.ManagedMcpServers.FirstOrDefault(x => x.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (otherOwner is not null && !options.Force)
            {
                result.Conflicts.Add($"MCP server is owned by another bundle: {serverName}");
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

            if (!options.Force && existingHash != HashUtility.ComputeJsonHash(templateNode))
            {
                result.Conflicts.Add($"Unowned MCP server already exists: {serverName}");
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

        return Directory.GetFiles(bundlesRoot, "manifest.json", SearchOption.AllDirectories)
            .Select(LoadManifest)
            .Where(x => x is not null)
            .Cast<AiBundleManifest>()
            .ToList();
    }

    private AiBundleManifest? LoadManifest(string manifestPath)
        => JsonSerializer.Deserialize<AiBundleManifest>(File.ReadAllText(manifestPath), _serializerOptions);

    private Dictionary<string, ManagedFileEntry> EnumerateAllManagedFiles(AiBundleDefinition bundle)
    {
        var results = new Dictionary<string, ManagedFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in bundle.ManagedDirectories)
        {
            string sourceDirectory = ResolveAssetPath(directory);

            foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = NormalizePath(Path.GetRelativePath(assetRoot, file));
                results[relativePath] = new ManagedFileEntry(file, null);
            }
        }

        foreach (AdapterDirectoryDefinition adapter in bundle.AdapterDirectories)
        {
            string sourceDirectory = ResolveAssetPath(adapter.Source);

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

    private sealed record ManagedFileEntry(string SourcePath, Dictionary<string, string>? Substitutions);

    private static string ResolveTargetRoot(string targetPath) => Path.GetFullPath(targetPath);

    private string ResolveAssetPath(string relativePath) => Path.Combine(assetRoot, NormalizePath(relativePath));

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
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
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
        string extracted = fileContent[contentStart..endIndex].Trim('\r', '\n');
        content = extracted;
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

    private JsonObject LoadTemplateServers(string relativePath)
    {
        JsonNode node = JsonNode.Parse(File.ReadAllText(ResolveAssetPath(relativePath)))
            ?? throw new InvalidOperationException($"Failed to parse MCP template: {relativePath}");

        return GetOrCreateServers(node.AsObject());
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
        if (rootObject["servers"] is null)
        {
            rootObject["servers"] = new JsonObject();
        }

        return rootObject["servers"]!.AsObject();
    }

    private static JsonObject GetOrCreateMcpServers(JsonObject rootObject)
    {
        if (rootObject["mcpServers"] is null)
        {
            rootObject["mcpServers"] = new JsonObject();
        }

        return rootObject["mcpServers"]!.AsObject();
    }

    private static void SaveMcpJson(string mcpPath, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(mcpPath)!);
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
}
