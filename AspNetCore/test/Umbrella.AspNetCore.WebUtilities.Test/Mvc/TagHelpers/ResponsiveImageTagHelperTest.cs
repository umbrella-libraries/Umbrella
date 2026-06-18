using Microsoft.AspNetCore.Razor.TagHelpers;
using Umbrella.AspNetCore.WebUtilities.Razor.TagHelpers;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Exceptions;

namespace Umbrella.AspNetCore.WebUtilities.Test.Mvc.TagHelpers;

public class ResponsiveImageTagHelperTest
{
	[Theory]
	[InlineData("", 1, null)]
	[InlineData(null, 1, null)]
	[InlineData(null, 2, null)]
	[InlineData(null, 3, null)]
	[InlineData("/path/to/image.png", 1, null)]
	[InlineData("/path/to/image.png", 2, "/path/to/image.png 1x, /path/to/image@2x.png 2x")]
	[InlineData("/path/to/image.png", 3, "/path/to/image.png 1x, /path/to/image@2x.png 2x, /path/to/image@3x.png 3x")]
	[InlineData("http://www.google.com/path/to/image.png", 2, "http://www.google.com/path/to/image.png 1x, http://www.google.com/path/to/image@2x.png 2x")]
	[InlineData("https://www.google.com/path/to/image.png", 2, "https://www.google.com/path/to/image.png 1x, https://www.google.com/path/to/image@2x.png 2x")]
	public async Task GenerateSuccessAsync(string? path, int maxPixelDensity, string? expectedOutput)
	{
		var tagHelper = CreateTagHelper();
		tagHelper.ImageMaxPixelDensity = maxPixelDensity;

		var ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", path),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("image-density", maxPixelDensity)
		]);

		var output = Mocks.CreateImageTagHelperOutput(
		[
			new TagHelperAttribute("alt", "hello")
		], "img");

		await tagHelper.ProcessAsync(ctx, output);

		Assert.Equal(0, output.Content.GetContent().Length);
		Assert.Equal("img", output.TagName);

		bool srcSetShouldExist = !string.IsNullOrWhiteSpace(path) && maxPixelDensity > 1;

		var srcSetAttribute = output.Attributes.SingleOrDefault(x => x.Name is "srcset");

		if (srcSetShouldExist)
		{
			Assert.NotNull(srcSetAttribute);
			Assert.Equal(expectedOutput, srcSetAttribute!.Value);
		}
		else
		{
			Assert.Null(srcSetAttribute);
		}
	}

	[Fact]
	public async Task GenerateFailedAsync() => await Assert.ThrowsAsync<UmbrellaException>(async () =>
	{
		var tagHelper = CreateTagHelper();
		var ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", "/image/test"),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("image-density", 12)
		]);

		var output = Mocks.CreateImageTagHelperOutput(
		[
			new TagHelperAttribute("alt", "hello")
		], "img");

		await tagHelper.ProcessAsync(ctx, output);
	});

	private static ResponsiveImageTagHelper CreateTagHelper()
		=> new(
			CoreUtilitiesMocks.CreateLogger<ResponsiveImageTagHelper>(),
			Mocks.CreateUmbrellaWebHostingEnvironment(),
			Mocks.CreateMemoryCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			CoreUtilitiesMocks.CreateResponsiveImageHelper());
}