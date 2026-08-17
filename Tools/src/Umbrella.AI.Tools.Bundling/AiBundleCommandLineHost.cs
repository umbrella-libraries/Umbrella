using System.CommandLine;
using CommunityToolkit.Diagnostics;
using Umbrella.AI.Tools.Bundling.Commands;
using Umbrella.AI.Tools.Bundling.Models;
using Umbrella.AI.Tools.Bundling.Services;

namespace Umbrella.AI.Tools.Bundling;

/// <summary>
/// The command line surface shared by every bundle installer tool. A consuming tool supplies its
/// <see cref="BundleHostOptions"/> and needs no command wiring of its own.
/// </summary>
public static class AiBundleCommandLineHost
{
    /// <summary>
    /// Parses and executes the install, update, status, remove, and sync commands.
    /// </summary>
    public static async Task<int> RunAsync(string[] args, BundleHostOptions options)
    {
        Guard.IsNotNull(args);
        Guard.IsNotNull(options);

        var rootDirOption = new Option<string>("--root-dir")
        {
            Description = "The root directory where the bundle will be installed, updated, inspected, or removed.",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        rootDirOption.Aliases.Add("-r");
        rootDirOption.Aliases.Add("--path");
        rootDirOption.Aliases.Add("-p");

        var assetRootOption = new Option<string?>("--asset-root")
        {
            Description = $"Directory containing the bundle assets, i.e. one holding '{options.BundleDefinitionRelativePath}'. Overrides the {options.AssetRootEnvironmentVariable} environment variable and automatic discovery."
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Take ownership or overwrite bundle-managed content that has drifted."
        };

        var allowNonRepoOption = new Option<bool>("--allow-non-repo")
        {
            Description = "Skip repository-shape validation for the target path."
        };

        var cleanEmptyMcpOption = new Option<bool>("--clean-empty-mcp")
        {
            Description = "When removing, delete empty .mcp.json and .codex/config.toml files."
        };

        var rootCommand = new RootCommand($"The dotnet tool used to install and manage {options.DisplayName} bundles.");

        var installCommand = new Command("install", $"Install the {options.DisplayName} bundle into a target repository.")
        {
            rootDirOption,
            assetRootOption,
            forceOption,
            allowNonRepoOption
        };
        installCommand.SetAction(parseResult => CommandPrinter.Print(
            CreateInstaller(parseResult).Install(CreateOptions(parseResult))));

        var updateCommand = new Command("update", "Update files, managed doc blocks, and MCP servers owned by the bundle.")
        {
            rootDirOption,
            assetRootOption,
            forceOption,
            allowNonRepoOption
        };
        updateCommand.SetAction(parseResult => CommandPrinter.Print(
            CreateInstaller(parseResult).Update(CreateOptions(parseResult))));

        var statusCommand = new Command("status", "Show installation status, drift, and owned MCP servers for the bundle.")
        {
            rootDirOption,
            assetRootOption,
            allowNonRepoOption
        };
        statusCommand.SetAction(parseResult => CommandPrinter.Print(
            CreateInstaller(parseResult).GetStatus(CreateOptions(parseResult))));

        var removeCommand = new Command("remove", "Remove files, managed doc blocks, and MCP servers owned by the bundle.")
        {
            rootDirOption,
            assetRootOption,
            forceOption,
            allowNonRepoOption,
            cleanEmptyMcpOption
        };
        removeCommand.SetAction(parseResult => CommandPrinter.Print(
            CreateInstaller(parseResult).Remove(CreateOptions(parseResult))));

        var syncCommand = new Command("sync", "Regenerate adapters, .mcp.json compatibility entries, and Codex MCP config from repository sources.")
        {
            rootDirOption
        };
        syncCommand.SetAction(parseResult =>
        {
            string syncRoot = parseResult.GetValue(rootDirOption) ?? Directory.GetCurrentDirectory();
            return CommandPrinter.Print(CreateInstaller(parseResult).Sync(syncRoot));
        });

        rootCommand.Subcommands.Add(installCommand);
        rootCommand.Subcommands.Add(updateCommand);
        rootCommand.Subcommands.Add(statusCommand);
        rootCommand.Subcommands.Add(removeCommand);
        rootCommand.Subcommands.Add(syncCommand);

        return await rootCommand.Parse(args).InvokeAsync();

        // Asset root discovery is deferred so commands that work purely from a repository checkout,
        // such as sync, never fail because shipped assets could not be located.
        AiBundleInstaller CreateInstaller(ParseResult parseResult)
        {
            string? explicitAssetRoot = parseResult.CommandResult.Command.Options.Contains(assetRootOption)
                ? parseResult.GetValue(assetRootOption)
                : null;
            var locator = new AiBundleAssetLocator(options);
            return new AiBundleInstaller(options, new Lazy<string>(() => locator.ResolveAssetRoot(explicitAssetRoot)));
        }

        CommandOptions CreateOptions(ParseResult parseResult)
            => new()
            {
                TargetPath = parseResult.GetValue(rootDirOption) ?? Directory.GetCurrentDirectory(),
                Force = parseResult.CommandResult.Command.Options.Contains(forceOption) && parseResult.GetValue(forceOption),
                AllowNonRepo = parseResult.CommandResult.Command.Options.Contains(allowNonRepoOption) && parseResult.GetValue(allowNonRepoOption),
                CleanEmptyMcp = parseResult.CommandResult.Command.Options.Contains(cleanEmptyMcpOption) && parseResult.GetValue(cleanEmptyMcpOption)
            };
    }
}
