using CommunityToolkit.Diagnostics;
using Umbrella.AI.Tools.Bundling.Models;

namespace Umbrella.AI.Tools.Bundling.Services;

/// <summary>
/// Locates the directory containing a bundle's shipped assets. Discovery is scoped to the hosting
/// tool's own bundle id, which is what stops one installed tool adopting another bundle's assets.
/// </summary>
public sealed class AiBundleAssetLocator(BundleHostOptions options)
{
    /// <summary>
    /// Resolves the asset root, preferring an explicit override, then the hosting tool's environment
    /// variable, then locations relative to the tool binary and current directory.
    /// </summary>
    /// <param name="explicitAssetRoot">An explicit root supplied on the command line, if any.</param>
    public string ResolveAssetRoot(string? explicitAssetRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitAssetRoot))
        {
            return IsAssetRoot(explicitAssetRoot)
                ? Path.GetFullPath(explicitAssetRoot)
                : throw new InvalidOperationException(
                    $"'{Path.GetFullPath(explicitAssetRoot)}' does not contain '{options.BundleDefinitionRelativePath}'.");
        }

        string? environmentPath = Environment.GetEnvironmentVariable(options.AssetRootEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(environmentPath) && IsAssetRoot(environmentPath))
        {
            return Path.GetFullPath(environmentPath);
        }

        string[] directCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "tool-assets"),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        ];

        foreach (string candidate in directCandidates)
        {
            if (IsAssetRoot(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        foreach (string candidate in EnumerateAncestors(AppContext.BaseDirectory).Concat(EnumerateAncestors(Directory.GetCurrentDirectory())))
        {
            if (IsAssetRoot(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new InvalidOperationException(
            $"Could not locate the '{options.BundleId}' bundle assets. Set {options.AssetRootEnvironmentVariable} "
            + $"or pass --asset-root pointing at a directory containing '{options.BundleDefinitionRelativePath}'.");
    }

    private bool IsAssetRoot(string path)
    {
        Guard.IsNotNullOrWhiteSpace(path);
        return File.Exists(Path.Combine(path, options.BundleDefinitionRelativePath));
    }

    private static IEnumerable<string> EnumerateAncestors(string path)
    {
        var directory = new DirectoryInfo(path);

        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }
}
