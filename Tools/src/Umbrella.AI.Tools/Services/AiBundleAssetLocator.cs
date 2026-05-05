namespace Umbrella.AI.Tools.Services;

public static class AiBundleAssetLocator
{
    private const string BundleDefinitionRelativePath = ".ai-shared\\bundles\\umbrella\\bundle.json";

    public static string ResolveAssetRoot()
    {
        string? environmentPath = Environment.GetEnvironmentVariable("UMBRELLA_AI_ASSET_ROOT");

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

        throw new InvalidOperationException("Could not locate the Umbrella AI tool assets.");
    }

    private static bool IsAssetRoot(string path)
        => File.Exists(Path.Combine(path, BundleDefinitionRelativePath));

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