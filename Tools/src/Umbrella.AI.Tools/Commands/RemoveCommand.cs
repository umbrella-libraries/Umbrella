using Umbrella.AI.Tools.Services;

namespace Umbrella.AI.Tools.Commands;

public sealed class RemoveCommand(AiBundleInstaller installer)
{
    public int Execute(CommandOptions options) => CommandPrinter.Print(installer.Remove(options));
}