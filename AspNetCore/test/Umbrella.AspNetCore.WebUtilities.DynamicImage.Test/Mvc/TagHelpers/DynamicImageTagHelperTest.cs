using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers;
using Umbrella.AspNetCore.WebUtilities.DynamicImage.Mvc.TagHelpers.Options;
using Umbrella.AspNetCore.WebUtilities.Test;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Mvc.TagHelpers;

public class DynamicImageTagHelperTest
{
	[Fact]
	public async Task ProcessAsync_GeneratesUnversionedUrl()
	{
		var tagHelper = CreateTagHelper();
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Equal("picture", output.TagName);
		Assert.Contains("type=\"image/webp\"", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.webp", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/test.jpg\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("_v_", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesVersionedUrl_WhenVersionTokenProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.VersionToken = "AbC123";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.webp", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.jpg\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesPixelDensitySrcSet_WhenSizeWidthsNotProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.ImageMaxPixelDensity = 3;
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.webp 1x, /dynamicimage/100/50/Crop/jpg/images/test@2x.webp 2x, /dynamicimage/100/50/Crop/jpg/images/test@3x.webp 3x", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.jpg 1x, /dynamicimage/100/50/Crop/jpg/images/test@2x.jpg 2x, /dynamicimage/100/50/Crop/jpg/images/test@3x.jpg 3x", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesUnversionedSrcSet()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.SizeWidths = "100,200";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.webp 100w, /dynamicimage/200/100/Crop/jpg/images/test.webp 200w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.jpg 100w, /dynamicimage/200/100/Crop/jpg/images/test.jpg 200w", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_GeneratesVersionedSrcSet_WhenVersionTokenProvided()
	{
		var tagHelper = CreateTagHelper();
		tagHelper.VersionToken = "abc123";
		tagHelper.SizeWidths = "100,200";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.webp 100w, /dynamicimage/200/100/Crop/jpg/_v_abc123/images/test.webp 200w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/_v_abc123/images/test.jpg 100w, /dynamicimage/200/100/Crop/jpg/_v_abc123/images/test.jpg 200w", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_UsesConfiguredSourceOrderAndDeduplicatesFallback()
	{
		var options = new DynamicImageTagHelperOptions
		{
			PictureSourceFormats = [DynamicImageFormat.Avif, DynamicImageFormat.WebP]
		};
		DynamicImageTagHelper tagHelper = CreateTagHelper(options);
		tagHelper.ImageFormat = DynamicImageFormat.WebP;
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("type=\"image/avif\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("type=\"image/webp\"", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/test.webp\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_ExternalUrlRendersPictureWithoutSources()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		var (ctx, output) = CreateContextAndOutput("https://cdn.example.com/images/test.jpg");

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.DoesNotContain("<source", html, StringComparison.Ordinal);
		Assert.Contains("src=\"https://cdn.example.com/images/test.jpg\"", html, StringComparison.Ordinal);
	}

	private static DynamicImageTagHelper CreateTagHelper(DynamicImageTagHelperOptions? options = null)
		=> new(
			CoreUtilitiesMocks.CreateLogger<DynamicImageTagHelper>(),
			Mocks.CreateUmbrellaWebHostingEnvironment(),
			Mocks.CreateMemoryCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			CoreUtilitiesMocks.CreateResponsiveImageHelper(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			options ?? new DynamicImageTagHelperOptions())
		{
			ImageMaxPixelDensity = 1,
			WidthRequest = 100,
			HeightRequest = 50
		};

	private static (TagHelperContext ctx, TagHelperOutput output) CreateContextAndOutput(string src = "/images/test.jpg")
	{
		var ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", src),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("width-request", 100),
			new TagHelperAttribute("height-request", 50)
		]);

		var output = Mocks.CreateImageTagHelperOutput(
		[
			new TagHelperAttribute("src", src),
			new TagHelperAttribute("alt", "hello")
		], "img");

		return (ctx, output);
	}

	private static string RenderOutput(TagHelperOutput output)
	{
		using var writer = new StringWriter();
		output.WriteTo(writer, HtmlEncoder.Default);
		return WebUtility.HtmlDecode(writer.ToString());
	}
}
