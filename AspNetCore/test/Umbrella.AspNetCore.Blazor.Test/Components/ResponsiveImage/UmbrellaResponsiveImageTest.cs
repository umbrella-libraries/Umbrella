using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.ResponsiveImage;
using Umbrella.Internal.Mocks;

namespace Umbrella.AspNetCore.Blazor.Test.Components.ResponsiveImage;

public sealed class UmbrellaResponsiveImageTest
{
	[Fact]
	public async Task GeneratedAttributesOverrideAdditionalAttributes()
	{
		string html = await RenderAsync(
			maxPixelDensity: 2,
			additionalAttributes: new Dictionary<string, object>
			{
				["src"] = "/images/declared.png",
				["srcset"] = "declared-srcset",
				["loading"] = "eager",
				["decoding"] = "sync",
				["alt"] = "Description",
				["data-test"] = "preserved"
			});

		Assert.Contains("src=\"/images/test.png\"", html, StringComparison.Ordinal);
		Assert.Contains("srcset=\"/images/test.png 1x, /images/test@2x.png 2x\"", html, StringComparison.Ordinal);
		Assert.Contains("loading=\"lazy\"", html, StringComparison.Ordinal);
		Assert.Contains("decoding=\"async\"", html, StringComparison.Ordinal);
		Assert.Contains("alt=\"Description\"", html, StringComparison.Ordinal);
		Assert.Contains("data-test=\"preserved\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("declared-srcset", html, StringComparison.Ordinal);
		Assert.DoesNotContain("loading=\"eager\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("decoding=\"sync\"", html, StringComparison.Ordinal);
		Assert.Equal(1, html.Split(" src=\"").Length - 1);
		Assert.Equal(1, html.Split(" srcset=\"").Length - 1);
		Assert.Equal(1, html.Split(" loading=\"").Length - 1);
		Assert.Equal(1, html.Split(" decoding=\"").Length - 1);
	}

	[Fact]
	public async Task AdditionalAttributesRemainWhenNoReplacementIsGenerated()
	{
		string html = await RenderAsync(
			maxPixelDensity: 1,
			lazyLoadingEnabled: false,
			additionalAttributes: new Dictionary<string, object>
			{
				["src"] = "/images/declared.png",
				["srcset"] = "declared-srcset",
				["loading"] = "eager",
				["decoding"] = "sync",
				["class"] = "image-class"
			});

		Assert.Contains("src=\"/images/test.png\"", html, StringComparison.Ordinal);
		Assert.Contains("srcset=\"declared-srcset\"", html, StringComparison.Ordinal);
		Assert.Contains("loading=\"eager\"", html, StringComparison.Ordinal);
		Assert.Contains("decoding=\"sync\"", html, StringComparison.Ordinal);
		Assert.Contains("class=\"image-class\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("/images/declared.png", html, StringComparison.Ordinal);
		Assert.Equal(1, html.Split(" src=\"").Length - 1);
		Assert.Equal(1, html.Split(" srcset=\"").Length - 1);
		Assert.Equal(1, html.Split(" loading=\"").Length - 1);
		Assert.Equal(1, html.Split(" decoding=\"").Length - 1);
	}

	private static async Task<string> RenderAsync(
		int maxPixelDensity,
		bool lazyLoadingEnabled = true,
		IReadOnlyDictionary<string, object>? additionalAttributes = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(CoreUtilitiesMocks.CreateResponsiveImageHelper());

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		var parameters = new Dictionary<string, object?>
		{
			[nameof(UmbrellaResponsiveImage.Url)] = "/images/test.png",
			[nameof(UmbrellaResponsiveImage.MaxPixelDensity)] = maxPixelDensity,
			[nameof(UmbrellaResponsiveImage.LazyLoadingEnabled)] = lazyLoadingEnabled
		};

		if (additionalAttributes is not null)
			parameters[nameof(UmbrellaResponsiveImage.AdditionalAttributes)] = additionalAttributes;

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await renderer.RenderComponentAsync<UmbrellaResponsiveImage>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}
}
