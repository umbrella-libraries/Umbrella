# Accessibility baseline helper

Create this beside the browser test project's visual snapshot helper when a repo adopts baseline mode. Adjust the
namespace, the artifacts folder (use the one CI already publishes) and the counted impacts to match the repo.

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deque.AxeCore.Commons;

namespace IndyRecords.Web.Browser.Test.Accessibility;

/// <summary>
/// Compares a page's axe violations with its approved baseline of known violations, so a test fails only when a change
/// introduces a new one.
/// </summary>
/// <remarks>
/// <para>
/// The site has accessibility violations that aren't being fixed as part of the upgrade. Each page's known serious and
/// critical violations are recorded in <c>Baselines/{name}.json</c> (rule ID and element), which doubles as the
/// accessibility backlog. A violation that isn't in the baseline fails the test; a baselined violation that no longer
/// occurs is reported in the test output as fixed, and should be removed by approving the baseline again.
/// </para>
/// <para>
/// A missing baseline, a new violation or a fixed one writes the current violations to
/// <c>bin/.../visual-artifacts/accessibility/{name}.received.json</c>, which CI publishes in the <c>test-artifacts</c>
/// artifact. Review it and copy it over the baseline to approve it. Axe evaluates the DOM and styles rather than
/// rendered pixels, so one baseline serves every platform. Baselines are never updated automatically.
/// </para>
/// </remarks>
internal static partial class AccessibilityBaseline
{
	private static readonly string[] _impacts = ["serious", "critical"];

	private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	public static async Task AssertNoNewViolationsAsync(AxeResult result, string baselineName)
	{
		ArgumentNullException.ThrowIfNull(result);

		string baselinePath = Path.Combine(GetSourceDirectory(), "Baselines", $"{baselineName}.json");
		string artifactDirectory = Path.Combine(AppContext.BaseDirectory, "visual-artifacts", "accessibility");
		string receivedPath = Path.Combine(artifactDirectory, $"{baselineName}.received.json");

		_ = Directory.CreateDirectory(artifactDirectory);
		File.Delete(receivedPath);

		KnownViolation[] actual = [.. result.Violations
			.Where(x => _impacts.Contains(x.Impact))
			.SelectMany(x => x.Nodes.Select(n => new KnownViolation(x.Id, x.Impact, Normalise(n.Target.ToString()), x.Help)))
			.Distinct()
			.OrderBy(x => x.Rule, StringComparer.Ordinal)
			.ThenBy(x => x.Target, StringComparer.Ordinal)];

		if (!File.Exists(baselinePath))
		{
			await WriteAsync(receivedPath, actual);
			Assert.Fail($"Missing accessibility baseline {baselinePath}. Review the {actual.Length} known violation(s) in {receivedPath} and copy it to the baseline path to approve it.");
		}

		await using FileStream stream = File.OpenRead(baselinePath);
		KnownViolation[] baseline = await JsonSerializer.DeserializeAsync<KnownViolation[]>(stream, _jsonOptions, CancellationToken.None) ?? [];

		var baselineKeys = baseline.Select(x => (x.Rule, x.Target)).ToHashSet();
		var actualKeys = actual.Select(x => (x.Rule, x.Target)).ToHashSet();
		KnownViolation[] added = [.. actual.Where(x => !baselineKeys.Contains((x.Rule, x.Target)))];
		KnownViolation[] fixedViolations = [.. baseline.Where(x => !actualKeys.Contains((x.Rule, x.Target)))];

		if (added.Length is 0 && fixedViolations.Length is 0)
			return;

		await WriteAsync(receivedPath, actual);

		if (fixedViolations.Length > 0)
		{
			TestContext.Current.TestOutputHelper?.WriteLine(
				$"{fixedViolations.Length} baselined accessibility violation(s) no longer occur on '{baselineName}'. Approve {receivedPath} as the new baseline:{Environment.NewLine}{Describe(fixedViolations)}");
		}

		Assert.True(added.Length is 0,
			$"{added.Length} new accessibility violation(s) on '{baselineName}', not in {baselinePath}:{Environment.NewLine}{Describe(added)}{Environment.NewLine}" +
			$"Fix them, or, if they are existing behaviour, log them and approve {receivedPath} as the new baseline.");
	}

	private static async Task WriteAsync(string path, KnownViolation[] violations)
	{
		await using FileStream stream = File.Create(path);
		await JsonSerializer.SerializeAsync(stream, violations, _jsonOptions, CancellationToken.None);
	}

	private static string Describe(IEnumerable<KnownViolation> violations) =>
		string.Join(Environment.NewLine, violations.Select(x => $"    {x.Impact}: {x.Rule} - {x.Help}{Environment.NewLine}        {x.Target}"));

	// Selectors can carry build-specific scoped CSS attributes and generated IDs, which would make the baseline churn.
	// Axe writes a scoped CSS attribute as [b-xxxxxxxxxx=""], so the value is optional in the pattern.
	private static string Normalise(string target) =>
		GeneratedIdRegex().Replace(ScopedCssRegex().Replace(target, string.Empty), "*");

	[GeneratedRegex(@"\[b-[a-z0-9]{10}(="""")?\]")]
	private static partial Regex ScopedCssRegex();

	[GeneratedRegex(@"[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}", RegexOptions.IgnoreCase)]
	private static partial Regex GeneratedIdRegex();

	// Baselines live beside this file in the source tree, so approving one is a normal, reviewed source change.
	private static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetDirectoryName(sourceFilePath)!;

	internal sealed record KnownViolation(string Rule, string Impact, string Target, string Help);
}
```

Wire it up:

- Include the baselines in the project so they show in the IDE: `<None Include="Accessibility\Baselines\**" />`.
- Start existing pages with an approved baseline (an empty array `[]` when the page is clean).
- Before .NET 9, indented JSON uses the platform's line ending, so a received file is CRLF on Windows. Convert it to the
  repository's line ending (see `.gitattributes`) when approving it; on .NET 9 or later, set
  `JsonSerializerOptions.NewLine` instead.
- Document the mode, the baseline folder and the approval steps in the test README.
