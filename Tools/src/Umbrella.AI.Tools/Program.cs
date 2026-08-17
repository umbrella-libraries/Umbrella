using System.Reflection;
using Umbrella.AI.Tools.Bundling;
using Umbrella.AI.Tools.Bundling.Models;

return await AiBundleCommandLineHost.RunAsync(args, new BundleHostOptions
{
    BundleId = "umbrella",
    DisplayName = "Umbrella AI skills and agents",
    InstallerPackageId = "Umbrella.AI.Tools",
    InstallerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
    AssetRootEnvironmentVariable = "UMBRELLA_AI_ASSET_ROOT"
});
