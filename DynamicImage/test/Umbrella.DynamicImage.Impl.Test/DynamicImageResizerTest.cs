
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.DynamicImage.Abstractions.Caching;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Helpers;
using Umbrella.Utilities.Runtime;
using FreeImageResizer = Umbrella.DynamicImage.FreeImage.DynamicImageResizer;
using NetVipsResizer = Umbrella.DynamicImage.NetVips.DynamicImageResizer;
using SkiaSharpResizer = Umbrella.DynamicImage.SkiaSharp.DynamicImageResizer;

namespace Umbrella.DynamicImage.Impl.Test;

public class DynamicImageResizerTest
{
	//This is a 3KB test png of the ASP.NET MVC Logo
	private const string TestPNG = "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";

	private static string? _baseDirectory;

	private static string BaseDirectory
	{
		get
		{
			if (string.IsNullOrEmpty(_baseDirectory))
			{
				string baseDirectory = AppContext.BaseDirectory.ToLowerInvariant();
				int indexToEndAt = baseDirectory.IndexOf(PathHelper.PlatformNormalize($@"\bin\{DebugUtility.BuildConfiguration}\net10.0"), StringComparison.Ordinal);
				_baseDirectory = baseDirectory.Remove(indexToEndAt, baseDirectory.Length - indexToEndAt);
			}

			return _baseDirectory;
		}
	}

	private static readonly List<(DynamicImageOptions Options, Size TargetSize)> _optionsList =
	[
		//These images are smaller than the original image dimensions of 300 x 193
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(77, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(150, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(77, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(150, 96)),

		// These images have a height larger than the original image size but the width is still under the original
		(new DynamicImageOptions("/dummypath.png", 100, 400, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(100, 64)),
		(new DynamicImageOptions("/dummypath.png", 100, 400, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(100, 400)),
		(new DynamicImageOptions("/dummypath.png", 100, 400, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 100, 400, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(100, 64)),

		// These images have a width larger than the original image size but the height is still under the original
		(new DynamicImageOptions("/dummypath.png", 400, 100, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(155, 100)),
		(new DynamicImageOptions("/dummypath.png", 400, 100, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(400, 100)),
		(new DynamicImageOptions("/dummypath.png", 400, 100, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(155, 100)),
		(new DynamicImageOptions("/dummypath.png", 400, 100, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(300, 193)),

		// These images are larger than the original image dimensions of 300 x 193
		(new DynamicImageOptions("/dummypath.png", 500, 1500, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 500, 1500, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 500, 1500, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 500, 1500, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 1500, 500, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 1500, 500, DynamicResizeMode.Crop, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 1500, 500, DynamicResizeMode.UseHeight, DynamicImageFormat.Jpeg), new Size(300, 193)),
		(new DynamicImageOptions("/dummypath.png", 1500, 500, DynamicResizeMode.UseWidth, DynamicImageFormat.Jpeg), new Size(300, 193)),

		// PNG Tests
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.Png), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.Png), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.Png), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.Png), new Size(50, 32)),

		// BMP Tests
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.Bmp), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.Bmp), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.Bmp), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.Bmp), new Size(50, 32)),

		// GIF Tests
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.Gif), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.Gif), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.Gif), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.Gif), new Size(50, 32)),

		// WebP Tests
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.WebP), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.WebP), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.WebP), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.WebP), new Size(50, 32)),

		// Avif Tests
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.ScaleDown, DynamicImageFormat.Avif), new Size(50, 32)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.Crop, DynamicImageFormat.Avif), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseHeight, DynamicImageFormat.Avif), new Size(233, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.UseWidth, DynamicImageFormat.Avif), new Size(50, 32)),

		// CropFocalPoint Tests — output dimensions match Crop; only crop position differs
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 0.5, focalPointY: 0.5), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 0.0, focalPointY: 0.0), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 50, 150, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 1.0, focalPointY: 1.0), new Size(50, 150)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 0.5, focalPointY: 0.5), new Size(150, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 0.0, focalPointY: 0.0), new Size(150, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 1.0, focalPointY: 1.0), new Size(150, 50)),
		(new DynamicImageOptions("/dummypath.png", 150, 50, DynamicResizeMode.CropFocalPoint, DynamicImageFormat.Jpeg, focalPointX: 0.25, focalPointY: 0.75), new Size(150, 50)),
	];

	public static Collection<object[]> OptionsList = [];

	public static IReadOnlyCollection<object[]> ResizersList =
	[
		[CreateDynamicImageResizer<FreeImageResizer>()],
		[CreateDynamicImageResizer<NetVipsResizer>()],
		[CreateDynamicImageResizer<SkiaSharpResizer>()],
	];

	static DynamicImageResizerTest()
	{
		foreach (var option in _optionsList)
		{
			// FreeImage does not support Avif yet
			if (option.Options.Format is not DynamicImageFormat.Avif)
				OptionsList.Add([CreateDynamicImageResizer<FreeImageResizer>(), option, TestPNG]);

			// NetVips supports all formats including AVIF, but not BMP (libvips cannot write BMP to a buffer)
			if (option.Options.Format is not DynamicImageFormat.Bmp)
				OptionsList.Add([CreateDynamicImageResizer<NetVipsResizer>(), option, TestPNG]);

			// TODO: SkiaSharp has issues converting PNG to BMP and GIF. AVIF encoding is not available in the current native build.
			if (option.Options.Format is not DynamicImageFormat.Bmp and not DynamicImageFormat.Gif and not DynamicImageFormat.Avif)
				OptionsList.Add([CreateDynamicImageResizer<SkiaSharpResizer>(), option, TestPNG]);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			var customAssemblyLoadContext = new UnmanagedAssemblyLoadContext();

			string freeImagePath = Path.Combine(AppContext.BaseDirectory, "runtimes/linux-x64/native/FreeImage.so");
			string skiaSharpPath = Path.Combine(AppContext.BaseDirectory, "runtimes/linux-x64/native/libSkiaSharp.so");

			Console.WriteLine($"FreeImage: {File.Exists(freeImagePath)}");
			Console.WriteLine($"SkiaSharp: {File.Exists(skiaSharpPath)}");
			//Assembly.Load()
			//customAssemblyLoadContext.LoadUnmanagedLibrary(freeImagePath);
			//customAssemblyLoadContext.LoadUnmanagedLibrary(skiaSharpPath);

			//byte[] bytes = File.ReadAllBytes(freeImagePath);
			//AppDomain.CurrentDomain.
		}
	}

	[Theory]
	[MemberData(nameof(OptionsList))]
	[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This is a unit test")]
	public async Task GenerateImageAsync_FromFuncAsync(DynamicImageResizerBase resizer, (DynamicImageOptions Options, Size TargetSize) item, string base64Image)
	{
		Guard.IsNotNull(resizer);

		byte[] bytes = Convert.FromBase64String(base64Image);

		var fileMock = new Mock<IUmbrellaFileInfo>();
		_ = fileMock.Setup(x => x.ReadAsByteArrayAsync(null, default)).Returns(Task.FromResult(bytes));
		_ = fileMock.Setup(x => x.LastModified).Returns(DateTimeOffset.UtcNow);
		_ = fileMock.Setup(x => x.ExistsAsync(default)).Returns(Task.FromResult(true));
		_ = fileMock.Setup(x => x.Length).Returns(bytes.LongLength);

		var fileProviderMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = fileProviderMock.Setup(x => x.GetAsync("/dummypath.png", default)).Returns(Task.FromResult<IUmbrellaFileInfo?>(fileMock.Object));

		var (options, targetSize) = item;

		DynamicImageItem? result = await resizer.GenerateImageAsync(fileProviderMock.Object, options, CancellationToken.None);

		ReadOnlyMemory<byte> resizedImageBytes = result is not null ? await result.GetContentAsync(TestContext.Current.CancellationToken) : default;

		Assert.True(resizedImageBytes.Length > 0);

		// Using the System.Drawing APIs from the full framework, i.e. not a library being used for resizing, to check the output image sizes are correct
		// We can only do this for older formats though.
		if (options.Format is not DynamicImageFormat.WebP and not DynamicImageFormat.Avif)
		{
			using var ms = new MemoryStream(resizedImageBytes.ToArray());
			using var image = Image.FromStream(ms);
			//Assert.Equal(targetSize, image.Size);

			ImageFormat? formatToCheck = null;

			switch (options.Format)
			{
				case DynamicImageFormat.Bmp:
					formatToCheck = ImageFormat.Bmp;
					break;
				case DynamicImageFormat.Gif:
					formatToCheck = ImageFormat.Gif;
					break;
				case DynamicImageFormat.Jpeg:
					formatToCheck = ImageFormat.Jpeg;
					break;
				case DynamicImageFormat.Png:
					formatToCheck = ImageFormat.Png;
					break;
			}

			Assert.Equal(formatToCheck, image.RawFormat);
		}

		// Only output the images to disk when building in debug mode. This is to ensure that when running in release mode on the build server this doesn't waste
		// unneccessary resources.
		if (DebugUtility.IsDebug && !resizedImageBytes.IsEmpty)
		{
			string outputDirectory = PathHelper.PlatformNormalize($@"{BaseDirectory}\Output\{resizer.GetType().Namespace}");
			string outputPath = PathHelper.PlatformNormalize($@"{outputDirectory}\{options.Width}w-{options.Height}h-{options.ResizeMode}.{options.Format.ToFileExtensionString()}");

			_ = Directory.CreateDirectory(outputDirectory);

			using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
			await fs.WriteAsync(resizedImageBytes, TestContext.Current.CancellationToken);
		}
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void ResizeImage_InvalidImage(IDynamicImageResizer imageResizer)
	{
		byte[] pdfBytes = File.ReadAllBytes(PathHelper.PlatformNormalize($@"{BaseDirectory}\IkeaManual.pdf"));

		_ = Assert.Throws<UmbrellaDynamicImageException>(() => imageResizer.ResizeImage(pdfBytes, 100, 100, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg));
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void ResizeImage_DodgyImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[] bytes = File.ReadAllBytes(PathHelper.PlatformNormalize($@"{BaseDirectory}\test-dodgy-image.jpg"));

		var (resizedBytes, _, _) = imageResizer.ResizeImage(bytes, 100, 100, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg);

		Assert.True(resizedBytes.Length > 0);
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void ResizeImage_EmptyImage(IDynamicImageResizer imageResizer)
	{
		byte[] bytes = [];

		_ = Assert.Throws<ArgumentException>(() => imageResizer.ResizeImage(bytes, 100, 100, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg));
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void ResizeImage_NullImage(IDynamicImageResizer imageResizer)
	{
		byte[]? bytes = null;

		_ = Assert.Throws<ArgumentNullException>(() => imageResizer.ResizeImage(bytes!, 100, 100, DynamicResizeMode.ScaleDown, DynamicImageFormat.Jpeg));
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void IsImage_InvalidImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[] bytes = File.ReadAllBytes(PathHelper.PlatformNormalize($@"{BaseDirectory}\IkeaManual.pdf"));

		bool isValid = imageResizer.IsImage(bytes);

		Assert.False(isValid);
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void IsImage_EmptyImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[] bytes = [];

		bool isValid = imageResizer.IsImage(bytes);

		Assert.False(isValid);
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void IsImage_NullImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[]? bytes = null;

		bool isValid = imageResizer.IsImage(bytes!);

		Assert.False(isValid);
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void IsImage_ValidImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[] bytes = Convert.FromBase64String(TestPNG);

		bool isValid = imageResizer.IsImage(bytes);

		Assert.True(isValid);
	}

	[Theory]
	[MemberData(nameof(ResizersList))]
	public void GetImageDimensions_ValidImage(IDynamicImageResizer imageResizer)
	{
		Guard.IsNotNull(imageResizer);

		byte[] bytes = Convert.FromBase64String(TestPNG);

		var (width, height) = imageResizer.GetImageDimensions(bytes);

		Assert.Equal(300, width);
		Assert.Equal(193, height);
	}

	private static DynamicImageResizerBase CreateDynamicImageResizer<T>()
	{
		var logger = new Mock<ILogger<T>>();

		return (DynamicImageResizerBase)Activator.CreateInstance(typeof(T), logger.Object, new DynamicImageNoCache())!;
	}
}