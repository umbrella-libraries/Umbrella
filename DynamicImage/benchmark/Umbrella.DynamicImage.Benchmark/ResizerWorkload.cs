using Microsoft.Extensions.Logging.Abstractions;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;
using FreeImageResizer = Umbrella.DynamicImage.FreeImage.DynamicImageResizer;
using NetVipsResizer = Umbrella.DynamicImage.NetVips.DynamicImageResizer;
using SkiaSharpResizer = Umbrella.DynamicImage.SkiaSharp.DynamicImageResizer;

namespace Umbrella.DynamicImage.Benchmark;

internal sealed class ResizerWorkload
{
	private readonly IDynamicImageResizer _resizer;
	private readonly byte[] _source;
	private readonly ResizeScenario _scenario;

	public ResizerWorkload(string implementation, ResizeScenario scenario)
	{
		_scenario = scenario;
		_source = scenario.Load();
		_resizer = implementation switch
		{
			"FreeImage" => new FreeImageResizer(NullLogger<FreeImageResizer>.Instance, new DynamicImageNoCache()),
			"NetVips" => CreateNetVips(),
			"SkiaSharp" => new SkiaSharpResizer(NullLogger<SkiaSharpResizer>.Instance, new DynamicImageNoCache()),
			_ => throw new ArgumentException($"Unknown implementation: {implementation}")
		};
		if (!_resizer.SupportsFormat(scenario.Options.Format))
			throw new NotSupportedException($"{implementation} does not support {scenario.Options.Format}.");
	}

	public (byte[] resizedBytes, int resizedWidth, int resizedHeight) Resize() => _resizer.ResizeImage(_source, _scenario.Options);

	public int Validate()
	{
		var (bytes, width, height) = Resize();
		if (bytes.Length == 0 || width != _scenario.ExpectedWidth || height != _scenario.ExpectedHeight)
			throw new InvalidOperationException($"Invalid output dimensions for {_scenario.Id}: {width}x{height}.");
		if (!HasExpectedSignature(bytes, _scenario.Options.Format))
			throw new InvalidOperationException($"Incorrect encoded format for {_scenario.Id}.");
		var decoded = _resizer.GetImageDimensions(bytes);
		if (decoded != (_scenario.ExpectedWidth, _scenario.ExpectedHeight))
			throw new InvalidOperationException($"Incorrect decoded dimensions for {_scenario.Id}: {decoded}.");
		return bytes.Length;
	}

	private static NetVipsResizer CreateNetVips()
	{
		global::NetVips.Cache.Max = 0;
		Console.WriteLine($"NetVips operation cache: {(global::NetVips.Cache.Max)}; native concurrency: {(global::NetVips.NetVips.Concurrency)}");
		return new NetVipsResizer(NullLogger<NetVipsResizer>.Instance, new DynamicImageNoCache());
	}

	private static bool HasExpectedSignature(ReadOnlySpan<byte> bytes, DynamicImageFormat format) => format switch
	{
		DynamicImageFormat.Jpeg => bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
		DynamicImageFormat.Png => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
		DynamicImageFormat.WebP => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
		DynamicImageFormat.Avif => IsAvif(bytes),
		_ => false
	};

	private static bool IsAvif(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length < 16 || !bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
			return false;
		uint boxLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes);
		if (boxLength < 16 || boxLength > bytes.Length)
			return false;
		if (bytes.Slice(8, 4).SequenceEqual("avif"u8))
			return true;
		for (int offset = 16; offset + 4 <= boxLength; offset += 4)
		{
			if (bytes.Slice(offset, 4).SequenceEqual("avif"u8))
				return true;
		}

		return false;
	}
}