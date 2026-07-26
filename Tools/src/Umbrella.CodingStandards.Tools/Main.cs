using System.CommandLine;
using System.Xml.Linq;

namespace Umbrella.CodingStandards.Tools;

internal sealed class Main
{
	private const string CodingStandardsImportElement = "<Import Project=\"Umbrella.CodingStandards.props\" />";
	private static readonly IReadOnlyCollection<string> _filesToCopy = [".editorconfig", ".filenesting.json", "Umbrella.CodingStandards.props", "Umbrella.CodingStandards.cmd"];

#pragma warning disable CA1822 // Mark members as static
	public async Task<int> ExecuteAsync(string[] args)
#pragma warning restore CA1822 // Mark members as static
	{
		var outDirOption = new Option<string>("--root-dir", "-r")
		{
			Description = "The root directory where the files will be copied to."
		};

		var rootCommand = new RootCommand("The dotnet tool used to install the files used to enforce the Umbrella Coding Standards.")
		{
			outDirOption
		};

		rootCommand.SetAction(async parseResult =>
		{
			string outputDirectoryPath = parseResult.GetRequiredValue(outDirOption);

			foreach (string fileName in _filesToCopy)
			{
				string sourcePath = Path.Combine(AppContext.BaseDirectory, fileName);
				string targetPath = Path.Combine(outputDirectoryPath, fileName);

				File.Copy(sourcePath, targetPath, true);
			}

			string directoryBuildPropsPath = Path.Combine(outputDirectoryPath, "Directory.Build.props");

			FileInfo fiDirectoryBuildPropsPath = new(directoryBuildPropsPath);

			if (!fiDirectoryBuildPropsPath.Exists)
			{
				using var sw = fiDirectoryBuildPropsPath.CreateText();

				await sw.WriteAsync($"""
					<Project>
						{CodingStandardsImportElement}
					</Project>
					""");
			}
			else
			{
				string? text = null;

				using (var sr = fiDirectoryBuildPropsPath.OpenText())
				{
					text = await sr.ReadToEndAsync();
				}

				var document = XDocument.Parse(text);

				if (document.Root is null)
					throw new InvalidOperationException("The document root is null.");

				if (!document.Root.Elements().Any(x => x.Name == "Import" && x.Attribute("Project")?.Value is "Umbrella.CodingStandards.props"))
				{
					var element = new XElement("Import", new XAttribute("Project", "Umbrella.CodingStandards.props"));

					document.Root.AddFirst(element);

					using var sw = fiDirectoryBuildPropsPath.OpenWrite();
					await document.SaveAsync(sw, SaveOptions.None, CancellationToken.None);
				}
			}

			string commandsJsonPath = Path.Combine(outputDirectoryPath, "commands.json");

			FileInfo fiCommandsJsonPath = new(commandsJsonPath);

			if (!fiCommandsJsonPath.Exists)
			{
				using var sw = fiCommandsJsonPath.CreateText();
				await sw.WriteAsync("""
					{
						"commands": {
							"UmbrellaCodingStandards": {
								"fileName": "cmd.exe",
							"workingDirectory": ".",
							"arguments": "/c Umbrella.CodingStandards.cmd"
							}
						},
						"-vs-binding":{ "ProjectOpened":["UmbrellaCodingStandards"]}
					}
					""");
			}
			else
			{
				// TODO: Load the existing file and mutate.
			}
		});

		return await rootCommand.Parse(args).InvokeAsync();
	}
}

//internal class NamingConventionsTest
//{
//	public const string publicConst = "Val";
//	private const string privateConst = "Val";
//	private static string _myStaticString = "dd";
//	private readonly string _test = "test";

//	public NamingConventionsTest(string Param1, string Param2)
//	{
//	}

//	public (string Item1, string Item2) publicMethod(string Param1, string Param2)
//	{
//		string MyVariable = _myStaticString + Param1 + _test;

//		void processItem(string p1) => MyVariable += p1;

//		return (MyVariable, MyVariable);
//	}

//	public async Task dosomething() => await Task.Yield();
//}

// TODO: Tuple member names should be camelCase. Maybe?? Unless we keep them as PascalCase
// and then the rule for camelCase locals should kick in if the tuple is deconstructed.
// TODO: Record positional parameter names should be PascalCase
