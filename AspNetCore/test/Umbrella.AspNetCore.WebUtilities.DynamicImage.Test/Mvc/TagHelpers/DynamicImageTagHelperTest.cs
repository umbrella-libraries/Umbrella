using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;
using Umbrella.AspNetCore.WebUtilities.Test;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;
using Xunit;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Mvc.TagHelpers;

public class DynamicImageTagHelperTest
{
	[Fact]
	public async Task ProcessAsync_GeneratesUnversionedUrl()
	{
		var tagHelper = CreateTagHelper();
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal("/dynamicimage/100/50/Crop/jpg/images/test.jpg", output.Attributes["src"].Value);
		Assert.DoesNotContain("_v_", output.Attributes["src"].Value?.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesVersionedUrl_WhenVersionTokenProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.VersionToken = "AbC123";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.jpg", output.Attributes["src"].Value);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesPixelDensitySrcSet_WhenSizeWidthsNotProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.ImageMaxPixelDensity = 3;
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal("/dynamicimage/100/50/Crop/jpg/images/test.jpg", output.Attributes["src"].Value);
		Assert.Equal("/dynamicimage/100/50/Crop/jpg/images/test.jpg 1x, /dynamicimage/100/50/Crop/jpg/images/test@2x.jpg 2x, /dynamicimage/100/50/Crop/jpg/images/test@3x.jpg 3x", output.Attributes["srcset"].Value);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesUnversionedSrcSet()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.SizeWidths = "100,200";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal("/dynamicimage/100/50/Crop/jpg/images/test.jpg", output.Attributes["src"].Value);
		Assert.Equal("/dynamicimage/100/50/Crop/jpg/images/test.jpg 100w, /dynamicimage/200/100/Crop/jpg/images/test.jpg 200w", output.Attributes["srcset"].Value);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesVersionedSrcSet_WhenVersionTokenProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.VersionToken = "abc123";
		tagHelper.SizeWidths = "100,200";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.jpg", output.Attributes["src"].Value);
		Assert.Equal("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.jpg 100w, /dynamicimage/200/100/Crop/jpg/_v_abc123/images/test.jpg 200w", output.Attributes["srcset"].Value);
	}

	private static DynamicImageTagHelper CreateTagHelper()
		=> new(
			CoreUtilitiesMocks.CreateLogger<DynamicImageTagHelper>(),
			Mocks.CreateUmbrellaWebHostingEnvironment(),
			Mocks.CreateMemoryCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			CoreUtilitiesMocks.CreateResponsiveImageHelper(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			new DynamicImageTagHelperOptions())
		{
			ImageMaxPixelDensity = 1,
			WidthRequest = 100,
			HeightRequest = 50
		};

	private static (TagHelperContext ctx, TagHelperOutput output) CreateContextAndOutput()
	{
		var ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", "/images/test.jpg"),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("width-request", 100),
			new TagHelperAttribute("height-request", 50)
		]);

		var output = Mocks.CreateImageTagHelperOutput(
		[
			new TagHelperAttribute("src", "/images/test.jpg"),
			new TagHelperAttribute("alt", "hello")
		], "img");

		return (ctx, output);
	}
}
