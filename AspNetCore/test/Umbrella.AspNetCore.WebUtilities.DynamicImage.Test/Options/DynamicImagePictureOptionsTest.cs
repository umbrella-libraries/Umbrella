using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;
using Umbrella.DynamicImage.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Options;

public class DynamicImagePictureOptionsTest
{
	[Fact]
	public void DefaultsToWebP()
	{
		Assert.Equal([DynamicImageFormat.WebP], new DynamicImageTagHelperOptions().PictureSourceFormats);
		Assert.Equal([DynamicImageFormat.WebP], new UmbrellaDynamicImageOptions().PictureSourceFormats);
	}

	[Theory]
	[InlineData(DynamicImageFormat.Jpeg)]
	[InlineData(DynamicImageFormat.Png)]
	public void Validate_RejectsUnsupportedPictureSourceFormat(DynamicImageFormat format)
	{
		var tagHelperOptions = new DynamicImageTagHelperOptions { PictureSourceFormats = [format] };
		var blazorOptions = new UmbrellaDynamicImageOptions { PictureSourceFormats = [format] };

		_ = Assert.Throws<ArgumentException>(tagHelperOptions.Validate);
		_ = Assert.Throws<ArgumentException>(blazorOptions.Validate);
	}

	[Fact]
	public void Validate_RejectsDuplicatePictureSourceFormats()
	{
		var tagHelperOptions = new DynamicImageTagHelperOptions { PictureSourceFormats = [DynamicImageFormat.WebP, DynamicImageFormat.WebP] };
		var blazorOptions = new UmbrellaDynamicImageOptions { PictureSourceFormats = [DynamicImageFormat.Avif, DynamicImageFormat.Avif] };

		_ = Assert.Throws<ArgumentException>(tagHelperOptions.Validate);
		_ = Assert.Throws<ArgumentException>(blazorOptions.Validate);
	}
}
