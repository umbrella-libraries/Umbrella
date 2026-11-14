# Visual snapshot helper

Put this in the browser test project, for example `Visual\VisualSnapshot.cs`, with baselines in `Visual\Snapshots\`. It uses Magick.NET; port the same contract to another image library if the repo already uses one.

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImageMagick;
using Microsoft.Playwright;

/// <summary>
/// Compares a screenshot with its approved baseline, pixel for pixel.
/// </summary>
/// <remarks>
/// <para>
/// A pixel matches when each of its channels is within <see cref="ChannelTolerance"/> levels of the baseline's. Chromium's
/// anti-aliasing of rounded corners and scaled images varies by a level or so between runs of the same page, while a real
/// change to text, layout or colour moves channels much further.
/// </para>
/// <para>
/// Fonts render differently on each operating system, so baselines are per platform: Snapshots/{name}.{platform}.png.
/// CI runs on Linux, so the linux baselines are the ones CI checks; approve them only from a pipeline run. A missing or
/// different baseline fails the test and writes .received.png (and .expected.png and .diff.png) to visual-artifacts.
/// Baselines are never updated automatically.
/// </para>
/// </remarks>
internal static class VisualSnapshot
{
	private const int ChannelTolerance = 2;

	private static string Platform =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
		: RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
		: "linux";

	/// <summary>
	/// Takes a screenshot of the whole page, as wide as the window, with animations stopped and the caret hidden.
	/// </summary>
	/// <remarks>
	/// A full-page screenshot is as wide as the page's content. A carousel's off-screen slides make that wider than the
	/// window, although the site clips them so visitors can neither see nor scroll to them. The screenshot is cropped to
	/// the window's width: what visitors see.
	/// </remarks>
	public static async Task<byte[]> CaptureFullPageAsync(IPage page, params ILocator[] mask)
	{
		ArgumentNullException.ThrowIfNull(page);

		byte[] screenshot = await page.ScreenshotAsync(new()
		{
			FullPage = true,
			Animations = ScreenshotAnimations.Disabled,
			Caret = ScreenshotCaret.Hide,
			Mask = mask,
		});

		uint width = (uint)(page.ViewportSize?.Width ?? throw new InvalidOperationException("The page has no viewport size."));
		using var image = new MagickImage(screenshot);

		if (image.Width <= width)
			return screenshot;

		image.Crop(new MagickGeometry(0, 0, width, image.Height));
		image.ResetPage();

		return image.ToByteArray(MagickFormat.Png);
	}

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

		bool sameSize = expected.Width == actual.Width && expected.Height == actual.Height;
		int differingPixels = sameSize ? CountDifferingPixels(expected, actual) : -1;

		if (differingPixels is 0)
			return;

		await File.WriteAllBytesAsync(receivedPath, screenshot, CancellationToken.None);
		File.Copy(baselinePath, expectedPath, overwrite: true);

		// Pad rather than resize, so a change in size stays visible in the diff.
		string dimensions = $"Expected {expected.Width}x{expected.Height}, received {actual.Width}x{actual.Height}." +
			(sameSize ? $" {differingPixels} pixel(s) differ by more than {ChannelTolerance} levels." : string.Empty);
		uint width = Math.Max(expected.Width, actual.Width);
		uint height = Math.Max(expected.Height, actual.Height);
		expected.Extent(width, height, Gravity.Northwest, MagickColors.Transparent);
		actual.Extent(width, height, Gravity.Northwest, MagickColors.Transparent);

		using var difference = expected.Compare(actual, ErrorMetric.Absolute, out _);
		await difference.WriteAsync(diffPath, MagickFormat.Png);

		Assert.Fail($"Visual snapshot '{snapshotName}' differs. {dimensions} Expected: {expectedPath}; received: {receivedPath}; diff: {diffPath}. Review the received image before copying it to {baselinePath}.");
	}

	private static int CountDifferingPixels(MagickImage expected, MagickImage actual)
	{
		byte[] expectedBytes = ToRgba(expected);
		byte[] actualBytes = ToRgba(actual);
		int count = 0;

		for (int i = 0; i < expectedBytes.Length; i += 4)
		{
			for (int channel = i; channel < i + 4; channel++)
			{
				if (Math.Abs(expectedBytes[channel] - actualBytes[channel]) > ChannelTolerance)
				{
					count++;
					break;
				}
			}
		}

		return count;
	}

	private static byte[] ToRgba(MagickImage image)
	{
		using IUnsafePixelCollection<byte> pixels = image.GetPixelsUnsafe();
		return pixels.ToByteArray(PixelMapping.RGBA) ?? [];
	}

	// Baselines live beside this file in the source tree, so approving one is a normal, reviewed source change.
	private static string GetSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetDirectoryName(sourceFilePath)!;
}
```

Notes:

- `[CallerFilePath]` is the path at compile time. Tests must run on the machine that built them (true for local runs and for a CI job that builds and tests in one job). If the repo uses deterministic source paths (`PathMap`, `ContinuousIntegrationBuild`) that rewrite it, resolve the source directory from the repo root instead.
- Magick.NET's `Width`/`Height` are `uint` in recent versions; older versions use `int`.
- Keep the tolerance at 2 levels. If a page still differs between runs, find what varies (an animation, a lazy image, a font loading late) and wait for it or mask it, rather than raising the tolerance.
- Chromium can't capture a full-page screenshot much taller than about 16,000px; split a page that grows past it (several seeded pages, or a screenshot per section) rather than letting it be cut off.
- The `visual-artifacts` folder sits under `bin`, which is what the CI failure-artifact step collects.
- Approving: review the received image (and the diff), copy it over the baseline, commit. Windows baselines come from local runs; Linux baselines from the CI `test-artifacts` of a failed run.
