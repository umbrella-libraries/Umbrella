using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.DynamicImage.Benchmark;

public sealed record ResizeScenario(string Id, string Fixture, DynamicImageOptions Options, int ExpectedWidth, int ExpectedHeight)
{
	public static IReadOnlyList<ResizeScenario> All { get; } = Create();
	public static IEnumerable<string> CommonIds => All.Where(x => x.Options.Format != DynamicImageFormat.Avif).Select(x => x.Id);
	public static IEnumerable<string> AvifIds => All.Where(x => x.Options.Format == DynamicImageFormat.Avif).Select(x => x.Id);
	public static ResizeScenario Find(string id) => All.Single(x => x.Id == id);
	public byte[] Load() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", Fixture));

	private static List<ResizeScenario> Create()
	{
		List<ResizeScenario> scenarios = [];
		foreach (int width in new[] { 1920, 3840 })
		{
			foreach (string source in new[] { "jpg", "png" })
			{
				foreach (DynamicImageFormat format in new[] { DynamicImageFormat.Jpeg, DynamicImageFormat.Png, DynamicImageFormat.WebP, DynamicImageFormat.Avif })
				{
					foreach (string operation in new[] { "Fit320", "Fit1280", "Crop320", "Focal320" })
					{
						bool crop = operation is "Crop320" or "Focal320";
						int targetWidth = operation == "Fit1280" ? 1280 : 320;
						int targetHeight = operation == "Fit1280" ? 720 : 320;
						string fixture = $"pattern-{width}x{width * 9 / 16}.{source}";
						DynamicImageOptions options = new(fixture, targetWidth, targetHeight,
							crop ? DynamicResizeMode.Crop : DynamicResizeMode.ScaleDown, format,
							DynamicImageFilterQuality.Medium, 75,
							focalPointX: operation == "Focal320" ? 0.25 : null,
							focalPointY: operation == "Focal320" ? 0.75 : null);
						scenarios.Add(new($"{width}-{source}-{operation}-{format}", fixture, options,
							targetWidth, crop ? targetHeight : targetWidth * 9 / 16));
					}
				}
			}
		}

		return scenarios;
	}
}