using SkiaSharp;

namespace Umbrella.DynamicImage.Benchmark;

// Offline-only fixture generation; never called by benchmark setup or memory workers.
internal static class FixtureGenerator
{
	public static void Generate(string directory)
	{
		_ = Directory.CreateDirectory(directory);
		foreach (int width in new[] { 1920, 3840 })
		{
			int height = width * 9 / 16;
			using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
			uint state = 0x12345678;
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					state ^= state << 13;
					state ^= state >> 17;
					state ^= state << 5;
					int texture = (int)(state & 31);
					int edge = ((x / 80 + y / 80) & 1) * 70;
					bitmap.SetPixel(x, y, new SKColor((byte)((x * 180 / width + texture) % 256),
						(byte)((y * 180 / height + edge) % 256), (byte)((x + y + texture) % 256)));
				}
			}

			foreach (var (format, extension) in new[] { (SKEncodedImageFormat.Jpeg, "jpg"), (SKEncodedImageFormat.Png, "png") })
			{
				using var data = bitmap.Encode(format, 90);
				using var stream = File.Create(Path.Combine(directory, $"pattern-{width}x{height}.{extension}"));
				data.SaveTo(stream);
			}
		}
	}
}