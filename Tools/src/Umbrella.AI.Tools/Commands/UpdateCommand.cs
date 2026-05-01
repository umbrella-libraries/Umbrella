using Umbrella.AI.Tools.Services;

namespace Umbrella.AI.Tools.Commands;

public sealed class UpdateCommand(AiBundleInstaller installer)
{
    public int Execute(CommandOptions options) => CommandPrinter.Print(installer.Update(options));
}