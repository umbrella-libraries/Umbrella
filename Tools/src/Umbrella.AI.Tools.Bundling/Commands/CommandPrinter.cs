using Umbrella.AI.Tools.Bundling.Models;

namespace Umbrella.AI.Tools.Bundling.Commands;

internal static class CommandPrinter
{
    public static int Print(OperationResult result)
    {
        Console.WriteLine(result.Success ? "Success" : "Failed");

        foreach (string message in result.Messages)
        {
            Console.WriteLine($"- {message}");
        }

        if (result.Conflicts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Conflicts:");

            foreach (string conflict in result.Conflicts)
            {
                Console.WriteLine($"- {conflict}");
            }
        }

        return result.Success ? 0 : 1;
    }
}