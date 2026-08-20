using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Imaging.Abstractions;

namespace Umbrella.AspNetCore.WebUtilities.DynamicImage.Test.Blazor;

public class UmbrellaDynamicImageTest
{
	[Fact]
	public async Task RendersPictureAsRootWithConfiguredSourcesAndFallback()
	{
		var options = new UmbrellaDynamicImageOptions
		{
			PictureSourceFormats = [DynamicImageFormat.Avif, DynamicImageFormat.WebP]
		};
		string html = await RenderAsync(
			options,
			new Dictionary<string, object?>
			{
				[nameof(UmbrellaDynamicImage.Url)] = "/images/test.jpg",
				[nameof(UmbrellaDynamicImage.WidthRequest)] = 100,
				[nameof(UmbrellaDynamicImage.HeightRequest)] = 50,
				[nameof(UmbrellaDynamicImage.MaxPixelDensity)] = 1,
				[nameof(UmbrellaDynamicImage.CssClass)] = "custom-picture",
				["alt"] = "Test image"
			});

		Assert.StartsWith("<picture class=\"u-dynamic-image custom-picture\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("<div", html, StringComparison.Ordinal);
		Assert.True(html.IndexOf("type=\"image/avif\"", StringComparison.Ordinal) < html.IndexOf("type=\"image/webp\"", StringComparison.Ordinal));
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.avif", html, StringComparison.Ordinal);
		Assert.Contains("/dynamicimage/100/50/Crop/jpg/images/test.webp", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/test.jpg\"", html, StringComparison.Ordinal);
		Assert.Contains("alt=\"Test image\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ExternalUrlRendersPictureWithoutSources()
	{
		string html = await RenderAsync(
			new UmbrellaDynamicImageOptions(),
			new Dictionary<string, object?>
			{
				[nameof(UmbrellaDynamicImage.Url)] = "https://cdn.example.com/test.jpg",
				[nameof(UmbrellaDynamicImage.WidthRequest)] = 100,
				[nameof(UmbrellaDynamicImage.HeightRequest)] = 50,
				[nameof(UmbrellaDynamicImage.MaxPixelDensity)] = 1
			});

		Assert.StartsWith("<picture class=\"u-dynamic-image \"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("<source", html, StringComparison.Ordinal);
		Assert.Contains("src=\"https://cdn.example.com/test.jpg\"", html, StringComparison.Ordinal);
	}

	private static async Task<string> RenderAsync(UmbrellaDynamicImageOptions options, IDictionary<string, object?> parameters)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(options);
		_ = services.AddSingleton<IResponsiveImageHelper>(CoreUtilitiesMocks.CreateResponsiveImageHelper());
		_ = services.AddSingleton<IDynamicImageUtility>(provider => new DynamicImageUtility(provider.GetRequiredService<ILogger<DynamicImageUtility>>()));

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await renderer.RenderComponentAsync<UmbrellaDynamicImage>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}
}
