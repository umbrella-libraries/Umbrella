using System.Text.Json;
using System.Text.RegularExpressions;

namespace Umbrella.AI.Tools.Test;

public partial class SkillContractTest
{
	private static readonly string[] _allowedModes = ["read-only", "mutating"];
	private static readonly string[] _allowedValidations =
	[
		"analyzers",
		"build",
		"generated-catalog",
		"http-cache",
		"migration-review",
		"no-source-change",
		"report",
		"resolved-packages",
		"restore",
		"source-scope",
		"tests"
	];
	private static readonly string[] _metadataKeys = ["description", "name"];
	private static readonly string[] _scenarioKeys = ["fixture", "mode", "name", "validations"];
	private static string RepoRoot => GetRepoRoot();
	private static string SkillsRoot => Path.Combine(RepoRoot, ".ai-shared", "skills");

	[Fact]
	public void CanonicalSkillsHaveValidMetadataAndResources()
	{
		var names = new HashSet<string>(StringComparer.Ordinal);
		var descriptions = new HashSet<string>(StringComparer.Ordinal);

		foreach (string skillDirectory in Directory.EnumerateDirectories(SkillsRoot).Order(StringComparer.Ordinal))
		{
			string folderName = Path.GetFileName(skillDirectory);
			string skillPath = Path.Combine(skillDirectory, "SKILL.md");
			Assert.True(File.Exists(skillPath), $"{folderName} does not contain SKILL.md.");

			SkillMetadata metadata = ReadSkillMetadata(skillPath);
			Assert.Equal(folderName, metadata.Name);
			Assert.Matches("^[a-z0-9-]+$", metadata.Name);
			Assert.InRange(metadata.Name.Length, 1, 64);
			Assert.True(metadata.Description.Length >= 40, $"{folderName} needs a more descriptive trigger description.");
			Assert.True(names.Add(metadata.Name), $"Duplicate skill name: {metadata.Name}");
			Assert.True(descriptions.Add(metadata.Description), $"Duplicate skill description: {metadata.Description}");

			string content = File.ReadAllText(skillPath);
			Assert.DoesNotMatch("<(?:TODO|PLACEHOLDER)>|\\[(?:TODO|PLACEHOLDER)\\]", content);
			Assert.True(File.ReadLines(skillPath).Count() <= 500, $"{folderName}/SKILL.md exceeds the 500-line progressive-disclosure limit.");

			AssertDeclaredAssetsExist(skillDirectory, content);
			AssertCrossSkillResourcesExist(content);
			AssertOpenAiMetadataIsValid(skillDirectory, metadata.Name);
		}
	}

	[Fact]
	public void BehaviouralValidationManifestCoversEveryCanonicalSkill()
	{
		string manifestPath = Path.Combine(RepoRoot, ".ai-shared", "bundles", "umbrella", "skill-validation.json");
		using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
		JsonElement root = document.RootElement;
		Assert.Equal(1, root.GetProperty("version").GetInt32());

		var manifestNames = new HashSet<string>(StringComparer.Ordinal);

		foreach (JsonElement skill in root.GetProperty("skills").EnumerateArray())
		{
			Assert.Equal(_scenarioKeys, skill.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal));
			string name = skill.GetProperty("name").GetString()!;
			string mode = skill.GetProperty("mode").GetString()!;
			string fixture = skill.GetProperty("fixture").GetString()!;
			string[] validations = skill.GetProperty("validations").EnumerateArray().Select(x => x.GetString()!).ToArray();

			Assert.True(manifestNames.Add(name), $"Duplicate validation scenario: {name}");
			Assert.Contains(mode, _allowedModes);
			Assert.False(string.IsNullOrWhiteSpace(fixture));
			Assert.NotEmpty(validations);
			Assert.Equal(validations.Length, validations.Distinct(StringComparer.Ordinal).Count());
			Assert.All(validations, validation => Assert.Contains(validation, _allowedValidations));

			if (mode == "read-only")
			{
				Assert.Contains("no-source-change", validations);
				Assert.Contains("report", validations);
			}
			else
			{
				Assert.Contains("source-scope", validations);
				Assert.Contains(validations, x => x is "build" or "tests" or "restore" or "migration-review");
			}
		}

		string[] canonicalNames = Directory.EnumerateDirectories(SkillsRoot)
			.Select(Path.GetFileName)
			.Order(StringComparer.Ordinal)
			.ToArray()!;
		Assert.Equal(canonicalNames, manifestNames.Order(StringComparer.Ordinal));
	}

	[Fact]
	public void CanonicalAgentPlaybooksOnlyReferenceExistingSkills()
	{
		var skillNames = Directory.EnumerateDirectories(SkillsRoot)
			.Select(Path.GetFileName)
			.ToHashSet(StringComparer.Ordinal)!;

		foreach (string agentRoot in new[]
		{
			Path.Combine(RepoRoot, ".ai-shared", "agents", "claude"),
			Path.Combine(RepoRoot, ".ai-shared", "agents", "github")
		})
		{
			foreach (string agentPath in Directory.EnumerateFiles(agentRoot, "*.md"))
			{
				string content = File.ReadAllText(agentPath);

				foreach (Match match in AgentSkillReferenceRegex().Matches(content))
				{
					string skillName = match.Groups["name"].Value;
					Assert.Contains(skillName, skillNames);
				}
			}
		}
	}

	private static SkillMetadata ReadSkillMetadata(string skillPath)
	{
		string content = File.ReadAllText(skillPath).Replace("\r\n", "\n", StringComparison.Ordinal);
		Assert.StartsWith("---\n", content, StringComparison.Ordinal);
		int end = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
		Assert.True(end > 4, $"{skillPath} has invalid YAML frontmatter.");

		var values = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (string line in content[4..end].Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			int separator = line.IndexOf(':', StringComparison.Ordinal);
			Assert.True(separator > 0, $"Invalid frontmatter line in {skillPath}: {line}");
			string key = line[..separator].Trim();
			string value = Unquote(line[(separator + 1)..].Trim());
			Assert.True(values.TryAdd(key, value), $"Duplicate frontmatter key in {skillPath}: {key}");
		}

		Assert.Equal(_metadataKeys, values.Keys.Order(StringComparer.Ordinal));
		return new(values["name"], values["description"]);
	}

	private static void AssertDeclaredAssetsExist(string skillDirectory, string content)
	{
		foreach (Match match in DeclaredAssetRegex().Matches(content))
		{
			string relativePath = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
			Assert.True(File.Exists(Path.Combine(skillDirectory, relativePath)), $"Missing declared skill asset: {relativePath}");
		}
	}

	private static void AssertCrossSkillResourcesExist(string content)
	{
		foreach (Match match in CrossSkillResourceRegex().Matches(content))
		{
			string skillName = match.Groups["skill"].Value;
			string relativePath = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
			Assert.True(File.Exists(Path.Combine(SkillsRoot, skillName, relativePath)), $"Missing cross-skill resource: {skillName}/{relativePath}");
		}
	}

	private static void AssertOpenAiMetadataIsValid(string skillDirectory, string skillName)
	{
		string metadataPath = Path.Combine(skillDirectory, "agents", "openai.yaml");
		Assert.True(File.Exists(metadataPath), $"{skillName} does not contain agents/openai.yaml.");
		string content = File.ReadAllText(metadataPath);

		string displayName = ReadQuotedYamlValue(content, "display_name");
		string shortDescription = ReadQuotedYamlValue(content, "short_description");
		string defaultPrompt = ReadQuotedYamlValue(content, "default_prompt");

		Assert.False(string.IsNullOrWhiteSpace(displayName));
		Assert.InRange(shortDescription.Length, 25, 64);
		Assert.StartsWith($"Use ${skillName}", defaultPrompt, StringComparison.Ordinal);
	}

	private static string ReadQuotedYamlValue(string content, string key)
	{
		Match match = Regex.Match(content, $"^\\s+{Regex.Escape(key)}:\\s+\"(?<value>[^\"]+)\"\\s*$", RegexOptions.Multiline);
		Assert.True(match.Success, $"Missing or invalid quoted openai.yaml value: {key}");
		return match.Groups["value"].Value;
	}

	private static string Unquote(string value)
	{
		if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
			return value[1..^1].Replace("''", "'", StringComparison.Ordinal);

		if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
			return value[1..^1];

		return value;
	}

	private static string GetRepoRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);

		while (directory is not null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, ".ai-shared"))
				&& Directory.Exists(Path.Combine(directory.FullName, "Tools")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Failed to locate the repository root for tests.");
	}

	private sealed record SkillMetadata(string Name, string Description);

	[GeneratedRegex(@"^\s*-\s*`(?<path>(?:scripts|references|assets)[\\/][^`]+)`\s*$", RegexOptions.Multiline)]
	private static partial Regex DeclaredAssetRegex();

	[GeneratedRegex(@"\{\{skill_dir\}\}[\\/](?<skill>[a-z0-9-]+)[\\/](?<path>(?:scripts|references|assets)[\\/][A-Za-z0-9._\\/-]+)")]
	private static partial Regex CrossSkillResourceRegex();

	[GeneratedRegex(@"skills[\\/](?<name>umbrella-[a-z0-9-]+)[\\/]SKILL\.md", RegexOptions.IgnoreCase)]
	private static partial Regex AgentSkillReferenceRegex();
}
