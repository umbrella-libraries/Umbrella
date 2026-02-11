
namespace Umbrella.TypeScript.Tools;

#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
public class Program
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
{
	public static int Main(string[] args)
	{
		var app = new UmbrellaTypeScriptApp();

		if (args is null ||
			args.Length == 0 ||
			args[0].Equals("-?", StringComparison.OrdinalIgnoreCase) ||
			args[0].Equals("-h", StringComparison.OrdinalIgnoreCase) ||
			args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
		{
			app.ShowHelp();
			return 1;
		}

		try
		{
			return app.Execute(args);
		}
		catch (Exception ex)
		{
			app.WriteConsoleErrorMessage("Failed: " + ex.Message);
			return 1;
		}
	}
}