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

	[Fact]
	public async Task ProcessAsync_GeneratesFocalPointUrlsForSourcesAndResponsiveFallback()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		tagHelper.ResizeMode = DynamicResizeMode.CropFocalPoint;
		tagHelper.FocalPointX = 0.25;
		tagHelper.FocalPointY = 0.75;
		tagHelper.SizeWidths = "100,200";
		var (ctx, output) = CreateContextAndOutput();

		await tagHelper.ProcessAsync(ctx, output);

		string html = RenderOutput(output);
		Assert.Contains("/dynamicimage/100/50/CropFocalPoint/jpg/images/test.webp?fpx=0.25&fpy=0.75 100w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/200/100/CropFocalPoint/jpg/images/test.webp?fpx=0.25&fpy=0.75 200w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/CropFocalPoint/jpg/images/test.jpg?fpx=0.25&fpy=0.75 100w", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/200/100/CropFocalPoint/jpg/images/test.jpg?fpx=0.25&fpy=0.75 200w", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_RejectsIncompleteFocalPoint()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		tagHelper.ResizeMode = DynamicResizeMode.CropFocalPoint;
		tagHelper.FocalPointX = 0.25;
		var (ctx, output) = CreateContextAndOutput();

		_ = await Assert.ThrowsAsync<ArgumentException>(() => tagHelper.ProcessAsync(ctx, output));
	}

	[Fact]
	public async Task ProcessAsync_RejectsOutOfRangeFocalPoint()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		tagHelper.ResizeMode = DynamicResizeMode.CropFocalPoint;
		tagHelper.FocalPointX = -0.01;
		tagHelper.FocalPointY = 0.75;
		var (ctx, output) = CreateContextAndOutput();

		_ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tagHelper.ProcessAsync(ctx, output));
	}

	[Fact]
	public async Task ProcessAsync_RejectsFocalPointForNonFocalResizeMode()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		tagHelper.FocalPointX = 0.25;
		tagHelper.FocalPointY = 0.75;
		var (ctx, output) = CreateContextAndOutput();

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => tagHelper.ProcessAsync(ctx, output));
	}

	[Fact]
	public async Task ProcessAsync_RendersArtDirectionSourcesBeforeFormatSourcesAndImage()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);

		string html = await RenderWithChildrenAsync(tagHelper, children: child);

		int artDirectedIndex = html.IndexOf("media=\"(max-width: 599px)\"", StringComparison.Ordinal);
		int formatSourceIndex = html.LastIndexOf("<source", StringComparison.Ordinal);
		int imageIndex = html.IndexOf("<img", StringComparison.Ordinal);

		Assert.True(artDirectedIndex >= 0);
		Assert.True(artDirectedIndex < formatSourceIndex);
		Assert.True(formatSourceIndex < imageIndex);
	}

	[Fact]
	public async Task ProcessAsync_ChildRendersSourcePerFormatIncludingOwnFallbackFormat()
	{
		var options = new DynamicImageTagHelperOptions
		{
			PictureSourceFormats = [DynamicImageFormat.Avif, DynamicImageFormat.WebP]
		};
		DynamicImageTagHelper tagHelper = CreateTagHelper(options);
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800, options: options);

		string html = await RenderWithChildrenAsync(tagHelper, children: child);

		// A browser that has matched the media condition will not fall back to the img element, so the fallback format must be offered
		// alongside the modern formats for that same condition.
		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.avif\" type=\"image/avif\"", html, StringComparison.Ordinal);
		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.webp\" type=\"image/webp\"", html, StringComparison.Ordinal);
		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.jpg\" type=\"image/jpeg\"", html, StringComparison.Ordinal);
		Assert.Equal(3, html.Split("media=\"(max-width: 599px)\"").Length - 1);
	}

	[Fact]
	public async Task ProcessAsync_ChildInheritsUndeclaredValuesFromParent()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		tagHelper.VersionToken = "abc123";
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);

		string html = await RenderWithChildrenAsync(tagHelper, src: "/images/test.jpg", children: child);

		// The child declares neither src nor version-token, so both are inherited from the parent.
		Assert.Contains("media=\"(max-width: 599px)\" srcset=\"/dynamicimage/600/800/Crop/jpg/_v_abc123/images/test.webp\" type=\"image/webp\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_ChildOverridesInheritedValuesWhenDeclared()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);
		child.TagHelper.ResizeMode = DynamicResizeMode.UseWidth;
		child.DeclaredAttributes.Add(new TagHelperAttribute("resize-mode", "UseWidth"));
		child.DeclaredAttributes.Add(new TagHelperAttribute("src", "/images/other.jpg"));

		string html = await RenderWithChildrenAsync(tagHelper, children: child);

		Assert.Contains("/dynamicimage/600/800/UseWidth/jpg/images/other.webp", html, StringComparison.Ordinal);
		Assert.DoesNotContain("Crop/jpg/images/other", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_ChildCopiesPassthroughAttributesOntoEverySource()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);
		child.OutputAttributes.Add(new TagHelperAttribute("sizes", "50vw"));

		string html = await RenderWithChildrenAsync(tagHelper, children: child);

		Assert.Equal(2, html.Split("sizes=\"50vw\"").Length - 1);
	}

	[Fact]
	public async Task ProcessAsync_ChildThrowsWhenUsedOutsideDynamicImage()
	{
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);
		TagHelperContext ctx = Mocks.CreateTagHelperContext(child.DeclaredAttributes);
		TagHelperOutput output = Mocks.CreateImageTagHelperOutput(child.OutputAttributes, "dynamic-source");

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => child.TagHelper.ProcessAsync(ctx, output));
	}

	[Fact]
	public async Task ProcessAsync_ChildThrowsWhenMediaNotProvided()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource(media: null, widthRequest: 600, heightRequest: 800);

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderWithChildrenAsync(tagHelper, children: child));
	}

	[Fact]
	public async Task ProcessAsync_ThrowsWhenChildContentIsNotEmpty()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		TagHelperContext ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", "/images/test.jpg"),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("width-request", 100),
			new TagHelperAttribute("height-request", 50)
		]);

		var output = new TagHelperOutput(
			"img",
			[new TagHelperAttribute("src", "/images/test.jpg"), new TagHelperAttribute("alt", "hello")],
			(useCachedResult, encoder) =>
			{
				var content = new DefaultTagHelperContent();
				_ = content.SetHtmlContent("<span>nope</span>");
				return Task.FromResult<TagHelperContent>(content);
			});

		tagHelper.Init(ctx);

		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => tagHelper.ProcessAsync(ctx, output));
	}

	[Fact]
	public async Task ProcessAsync_ThrowsWhenChildNestedInsideExternalUrlImage()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);

		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => RenderWithChildrenAsync(tagHelper, "https://cdn.example.com/images/test.jpg", child));

		// The source itself has to reject the parent. If this were left to the parent, which cannot report it until its child content has
		// already run, the source would first generate and cache meaningless URLs from the absolute URL it inherited.
		Assert.Contains("<dynamic-source> element cannot be used", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessAsync_ChildDoesNotRepeatIdAcrossGeneratedSources()
	{
		DynamicImageTagHelper tagHelper = CreateTagHelper();
		ChildSource child = CreateChildSource("(max-width: 599px)", widthRequest: 600, heightRequest: 800);
		child.OutputAttributes.Add(new TagHelperAttribute("ID", "hero-small"));
		child.OutputAttributes.Add(new TagHelperAttribute("class", "promo"));

		string html = await RenderWithChildrenAsync(tagHelper, children: child);

		// One element renders several source tags, so repeating the id would produce invalid HTML. The uppercase name also covers the
		// exclusion list being matched case-insensitively.
		Assert.DoesNotContain("hero-small", html, StringComparison.Ordinal);
		Assert.Equal(2, html.Split("class=\"promo\"").Length - 1);
	}

	private sealed record ChildSource(
		DynamicImagePictureSourceTagHelper TagHelper,
		TagHelperAttributeList DeclaredAttributes,
		TagHelperAttributeList OutputAttributes);

	private static ChildSource CreateChildSource(string? media, int widthRequest, int heightRequest, DynamicImageTagHelperOptions? options = null)
	{
		var tagHelper = new DynamicImagePictureSourceTagHelper(
			CoreUtilitiesMocks.CreateLogger<DynamicImagePictureSourceTagHelper>(),
			Mocks.CreateUmbrellaWebHostingEnvironment(),
			Mocks.CreateMemoryCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			CoreUtilitiesMocks.CreateResponsiveImageHelper(),
			new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()),
			options ?? new DynamicImageTagHelperOptions())
		{
			Media = media,
			WidthRequest = widthRequest,
			HeightRequest = heightRequest
		};

		// The Razor infrastructure records every authored attribute on the context and removes the ones bound to tag helper properties
		// from the output, so the two lists are populated separately here.
		var declaredAttributes = new TagHelperAttributeList
		{
			new("width-request", widthRequest),
			new("height-request", heightRequest)
		};

		if (media is not null)
			declaredAttributes.Add(new TagHelperAttribute("media", media));

		return new ChildSource(tagHelper, declaredAttributes, []);
	}

	private static async Task<string> RenderWithChildrenAsync(
		DynamicImageTagHelper tagHelper,
		string src = "/images/test.jpg",
		params ChildSource[] children)
	{
		TagHelperContext ctx = Mocks.CreateTagHelperContext(
		[
			new TagHelperAttribute("src", src),
			new TagHelperAttribute("alt", "hello"),
			new TagHelperAttribute("width-request", 100),
			new TagHelperAttribute("height-request", 50)
		]);

		TagHelperOutput output = Mocks.CreateImageTagHelperOutput(
			[new TagHelperAttribute("src", src), new TagHelperAttribute("alt", "hello")],
			"img",
			async () =>
			{
				foreach (ChildSource child in children)
				{
					// Razor copies the items of the enclosing scope into the scope of each child, which is what shares the picture context.
					TagHelperContext childContext = Mocks.CreateTagHelperContext(child.DeclaredAttributes, new Dictionary<object, object>(ctx.Items));
					TagHelperOutput childOutput = Mocks.CreateImageTagHelperOutput(child.OutputAttributes, "dynamic-source");

					await child.TagHelper.ProcessAsync(childContext, childOutput);
				}
			});

		tagHelper.Init(ctx);
		await tagHelper.ProcessAsync(ctx, output);

		return RenderOutput(output);
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
