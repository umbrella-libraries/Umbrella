# Visual snapshot helper

Put this in the browser test project, for example `Visual\VisualSnapshot.cs`, with baselines in `Visual\Snapshots\`. It uses Magick.NET; port the same contract to another image library if the repo already uses one.

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageMagick;

/// <summary>
/// Compares a screenshot with its approved baseline, pixel for pixel.
/// </summary>
/// <remarks>
/// Fonts render differently on each operating system, so baselines are per platform: Snapshots/{name}.{platform}.png.
/// CI runs on Linux, so the linux baselines are the ones CI checks; approve them only from a pipeline run. A missing or
/// different baseline fails the test and writes .received.png (and .expected.png and .diff.png) to visual-artifacts.
/// Baselines are never updated automatically.
/// </remarks>
internal static class VisualSnapshot
{
	private static string Platform =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
		: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
		: "linux";

	public static async Task AssertMatchesAsync(byte[] screenshot, string snapshotName)
	{
		string fileName = $"{snapshotName}.{Platform}";
		string baselinePath = Path.Combine(GetSourceDirectory(), "Snapshots", $"{fileName}.png");
		string artifactDirectory = Path.Combine(AppContext.BaseDirectory, "visual-artifacts");
		string receivedPath = Path.Combine(artifactDirectory, $"{fileName}.received.png");
		string expectedPath = Path.Combine(artifactDirectory, $"{fileName}.expected.png");
		string diffPath = Path.Combine(artifactDirectory, $"{fileName}.diff.png");

		_ = Directory.CreateDirectory(artifactDirectory);
		File.Delete(receivedPath);
		File.Delete(expectedPath);
		File.Delete(diffPath);

		if (!File.Exists(baselinePath))
		{
			await File.WriteAllBytesAsync(receivedPath, screenshot, CancellationToken.None);
			Assert.Fail($"Missing visual baseline {baselinePath}. Review {receivedPath} and copy it to the baseline path to approve it.");
		}

		using var expected = new MagickImage(baselinePath);
		using var actual = new MagickImage(screenshot);

		if (expected.Width == actual.Width && expected.Height == actual.Height && expected.Compare(actual, ErrorMetric.Absolute) == 0)
			return;

		await File.WriteAllBytesAsync(receivedPath, screenshot, CancellationToken.None);
		File.Copy(baselinePath, expectedPath, overwrite: true);

		// Pad rather than resize, so a change in size stays visible in the diff.
		string dimensions = $"Expected {expected.Width}x{expected.Height}, received {actual.Width}x{actual.Height}.";
		uint width = Math.Max(expected.Width, actual.Width);
		uint height = Math.Max(expected.Height, actual.Height);
		expected.Extent(width, height, Gravity.Northwest, MagickColors.Transparent);
		actual.Extent(width, height, Gravity.Northwest, MagickColors.Transparent);

		using var difference = expected.Compare(actual, ErrorMetric.Absolute, out _);
		await difference.WriteAsync(diffPath, MagickFormat.Png);

		Assert.Fail($"Visual snapshot '{snapshotName}' differs. {dimensions} Expected: {expectedPath}; received: {receivedPath}; diff: {diffPath}. Review the received image before copying it to {baselinePath}.");
	}

	// Baselines live beside this file in the source tree, so approving one is a normal, reviewed source change.
	private static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetDirectoryName(sourceFilePath)!;
}
```

Notes:

- `[CallerFilePath]` is the path at compile time. Tests must run on the machine that built them (true for local runs and for a CI job that builds and tests in one job). If the repo uses deterministic source paths (`PathMap`, `ContinuousIntegrationBuild`) that rewrite it, resolve the source directory from the repo root instead.
- Magick.NET's `Width`/`Height` are `uint` in recent versions; older versions use `int`.
- The `visual-artifacts` folder sits under `bin`, which is what the CI failure-artifact step collects.
- Approving: review the received image (and the diff), copy it over the baseline, commit. Windows baselines come from local runs; Linux baselines from the CI `test-artifacts` of a failed run.
