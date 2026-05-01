using Umbrella.AI.Tools.Services;

namespace Umbrella.AI.Tools.Commands;

public sealed class StatusCommand(AiBundleInstaller installer)
{
    public int Execute(CommandOptions options) => CommandPrinter.Print(installer.GetStatus(options));
}