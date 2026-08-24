using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.Utilities.Mime;

namespace Umbrella.Utilities.Test.Mime;

public class MimeTypeUtilityTest
{
	[Fact]
	public void GetMimeType_Empty() => Assert.Throws<ArgumentException>(() => CreateMimeTypeUtility().GetMimeType(""));

	[Fact]
	public void GetMimeType_Whitespace() => Assert.Throws<ArgumentException>(() => CreateMimeTypeUtility().GetMimeType("    "));

	[Fact]
	public void GetMimeType_Dot()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType(".");
		Assert.Equal("application/octet-stream", mimeType);
	}

	[Fact]
	public void GetMimeType_NamePlusDot()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("name.");
		Assert.Equal("application/octet-stream", mimeType);
	}

	[Fact]
	public void GetMimeType_Extension()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType(".png");
		Assert.Equal("image/png", mimeType);
	}

	[Fact]
	public void GetMimeType_Extension_NoLeadingPeriod()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("png");
		Assert.Equal("image/png", mimeType);
	}

	[Fact]
	public void GetMimeType_Filename()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("test.png");
		Assert.Equal("image/png", mimeType);
	}

	[Fact]
	public void GetMimeType_Extension_Uppercase()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("PNG");
		Assert.Equal("image/png", mimeType);
	}

	[Fact]
	public void GetMimeType_Filepath()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("/path/test.png");
		Assert.Equal("image/png", mimeType);
	}

	[Fact]
	public void GetMimeType_FilenameWithPeriods()
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType("test.image.with.periods.png");
		Assert.Equal("image/png", mimeType);
	}

	[Theory]
	[InlineData("bmp", "image/bmp")]
	[InlineData("gif", "image/gif")]
	[InlineData("jpg", "image/jpeg")]
	[InlineData("png", "image/png")]
	[InlineData("webp", "image/webp")]
	[InlineData("avif", "image/avif")]
	public void GetMimeType_DynamicImageFormat(string extension, string expectedMimeType)
	{
		string mimeType = CreateMimeTypeUtility().GetMimeType(extension);
		Assert.Equal(expectedMimeType, mimeType);
	}

	[Fact]
	public void GetFileExtension_Null()
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension(null);
		Assert.Null(fileExtension);
	}

	[Fact]
	public void GetFileExtension_Empty()
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension("");
		Assert.Null(fileExtension);
	}

	[Fact]
	public void GetFileExtension_Whitespace()
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension("    ");
		Assert.Null(fileExtension);
	}

	[Fact]
	public void GetFileExtension_Invalid()
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension("test");
		Assert.Null(fileExtension);
	}

	[Fact]
	public void GetFileExtension_Valid()
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension("image/png");
		Assert.Equal(".png", fileExtension);
	}

	[Theory]
	[InlineData("image/bmp", ".bmp")]
	[InlineData("image/gif", ".gif")]
	[InlineData("image/jpeg", ".jpg")]
	[InlineData("image/png", ".png")]
	[InlineData("image/webp", ".webp")]
	[InlineData("image/avif", ".avif")]
	public void GetFileExtension_DynamicImageContentType(string mimeType, string expectedExtension)
	{
		string? fileExtension = CreateMimeTypeUtility().GetFileExtension(mimeType);
		Assert.Equal(expectedExtension, fileExtension);
	}

	private static MimeTypeUtility CreateMimeTypeUtility() => new(new Mock<ILogger<MimeTypeUtility>>().Object);
}
