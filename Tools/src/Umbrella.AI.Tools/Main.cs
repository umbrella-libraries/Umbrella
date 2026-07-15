using CommunityToolkit.Diagnostics;
using System.CommandLine;
using Umbrella.AI.Tools.Commands;
using Umbrella.AI.Tools.Services;

namespace Umbrella.AI.Tools;

public sealed class Main(AiBundleInstaller installer)
{
#pragma warning disable CA1822
    public async Task<int> ExecuteAsync(string[] args)
#pragma warning restore CA1822
    {
        Guard.IsNotNull(args);

        var rootDirOption = new Option<string>("--root-dir")
        {
            Description = "The root directory where the bundle will be installed, updated, inspected, or removed.",
            DefaultValueFactory = _ => Directory.GetCurrentDirectory()
        };
        rootDirOption.Aliases.Add("-r");
        rootDirOption.Aliases.Add("--path");
        rootDirOption.Aliases.Add("-p");

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

        var rootCommand = new RootCommand("The dotnet tool used to install and manage Umbrella AI skills and agents bundles.");

        var installCommand = new Command("install", "Install the Umbrella AI skills and agents bundle into a target repository.")
        {
            rootDirOption,
            forceOption,
            allowNonRepoOption
        };
        installCommand.SetAction(parseResult =>
        {
            var options = CreateOptions(parseResult, rootDirOption, forceOption, allowNonRepoOption, cleanEmptyMcpOption);
            return CommandPrinter.Print(installer.Install(options));
        });

        var updateCommand = new Command("update", "Update files, managed doc blocks, and MCP servers owned by the bundle.")
        {
            rootDirOption,
            forceOption,
            allowNonRepoOption
        };
        updateCommand.SetAction(parseResult =>
        {
            var options = CreateOptions(parseResult, rootDirOption, forceOption, allowNonRepoOption, cleanEmptyMcpOption);
            return CommandPrinter.Print(installer.Update(options));
        });

        var statusCommand = new Command("status", "Show installation status, drift, and owned MCP servers for the bundle.")
        {
            rootDirOption,
            allowNonRepoOption
        };
        statusCommand.SetAction(parseResult =>
        {
            var options = CreateOptions(parseResult, rootDirOption, forceOption, allowNonRepoOption, cleanEmptyMcpOption);
            return CommandPrinter.Print(installer.GetStatus(options));
        });

        var removeCommand = new Command("remove", "Remove files, managed doc blocks, and MCP servers owned by the bundle.")
        {
            rootDirOption,
            forceOption,
            allowNonRepoOption,
            cleanEmptyMcpOption
        };
        removeCommand.SetAction(parseResult =>
        {
            var options = CreateOptions(parseResult, rootDirOption, forceOption, allowNonRepoOption, cleanEmptyMcpOption);
            return CommandPrinter.Print(installer.Remove(options));
        });

        var syncCommand = new Command("sync", "Regenerate adapters, .mcp.json compatibility entries, and Codex MCP config from repository sources.")
        {
            rootDirOption
        };
        syncCommand.SetAction(parseResult =>
        {
            string syncRoot = parseResult.GetValue(rootDirOption) ?? Directory.GetCurrentDirectory();
            return CommandPrinter.Print(installer.Sync(syncRoot));
        });

        rootCommand.Subcommands.Add(installCommand);
        rootCommand.Subcommands.Add(updateCommand);
        rootCommand.Subcommands.Add(statusCommand);
        rootCommand.Subcommands.Add(removeCommand);
        rootCommand.Subcommands.Add(syncCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static CommandOptions CreateOptions(
        ParseResult parseResult,
        Option<string> rootDirOption,
        Option<bool> forceOption,
        Option<bool> allowNonRepoOption,
        Option<bool> cleanEmptyMcpOption)
        => new()
        {
            TargetPath = parseResult.GetValue(rootDirOption) ?? Directory.GetCurrentDirectory(),
            Force = parseResult.GetValue(forceOption),
            AllowNonRepo = parseResult.GetValue(allowNonRepoOption),
            CleanEmptyMcp = parseResult.GetValue(cleanEmptyMcpOption)
        };
}
