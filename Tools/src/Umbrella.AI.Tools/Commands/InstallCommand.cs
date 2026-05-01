using Umbrella.AI.Tools.Services;

namespace Umbrella.AI.Tools.Commands;

public sealed class InstallCommand(AiBundleInstaller installer)
{
    public int Execute(CommandOptions options) => CommandPrinter.Print(installer.Install(options));
}