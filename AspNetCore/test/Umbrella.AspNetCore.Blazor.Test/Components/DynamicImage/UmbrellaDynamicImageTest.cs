using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage;
using Umbrella.AspNetCore.Blazor.Components.DynamicImage.Options;
using Umbrella.DynamicImage.Abstractions;
using Umbrella.Internal.Mocks;

namespace Umbrella.AspNetCore.Blazor.Test.Components.DynamicImage;

public sealed class UmbrellaDynamicImageTest
{
	[Theory]
	[InlineData(null, null)]
	[InlineData(null, 800)]
	[InlineData(600, null)]
	public async Task Art_directed_source_requires_explicit_dimensions(int? width, int? height)
	{
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			RenderAsync(childSources: [new ArtDirectedSource("(max-width: 599px)", width, height)]));

		Assert.Contains("must be declared explicitly", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0, 800)]
	[InlineData(600, 0)]
	[InlineData(-1, 800)]
	[InlineData(600, -1)]
	public async Task Art_directed_crop_requires_positive_dimensions(int width, int height)
	{
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			RenderAsync(childSources: [new ArtDirectedSource("(max-width: 599px)", width, height)]));
	}

	[Theory]
	[InlineData(DynamicResizeMode.UseWidth, 600, 1)]
	[InlineData(DynamicResizeMode.UseHeight, 1, 800)]
	public async Task Art_directed_source_supports_single_axis_resize_modes(DynamicResizeMode resizeMode, int width, int height)
	{
		string html = await RenderAsync(childSources:
		[
			new ArtDirectedSource("(max-width: 599px)", width, height) { ResizeMode = resizeMode }
		]);

		Assert.Contains($"/dynamicimage/{width}/{height}/{resizeMode}/", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Picture_renders_format_sources_before_the_fallback_image()
	{
		string html = await RenderAsync();

		int sourceIndex = html.IndexOf("<source", StringComparison.Ordinal);
		int imageIndex = html.IndexOf("<img", StringComparison.Ordinal);

		Assert.Contains("type=\"image/webp\"", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/test.jpg\"", html, StringComparison.Ordinal);
		Assert.True(sourceIndex >= 0);
		Assert.True(sourceIndex < imageIndex);
	}

	[Fact]
	public async Task Art_directed_sources_render_before_the_format_sources_and_the_image()
	{
		string html = await RenderAsync(childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800)]);

		int artDirectedIndex = html.IndexOf("media=\"(max-width: 599px)\"", StringComparison.Ordinal);
		int formatSourceIndex = html.LastIndexOf("<source", StringComparison.Ordinal);
		int imageIndex = html.IndexOf("<img", StringComparison.Ordinal);

		Assert.True(artDirectedIndex >= 0);
		Assert.True(artDirectedIndex < formatSourceIndex);
		Assert.True(formatSourceIndex < imageIndex);
	}

	[Fact]
	public async Task Art_directed_source_renders_one_source_per_format_including_its_own_fallback()
	{
		var options = new UmbrellaDynamicImageOptions
		{
			PictureSourceFormats = [DynamicImageFormat.Avif, DynamicImageFormat.WebP]
		};

		string html = await RenderAsync(options, childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800)]);

		// A browser that has matched the media condition will not fall back to the img element, so the fallback format has to be offered
		// alongside the modern formats for that same condition.
		Assert.Contains("type=\"image/avif\" srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.avif\"", html, StringComparison.Ordinal);
		Assert.Contains("type=\"image/webp\" srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.webp\"", html, StringComparison.Ordinal);
		Assert.Contains("type=\"image/jpeg\" srcset=\"/dynamicimage/600/800/Crop/jpg/images/test.jpg\"", html, StringComparison.Ordinal);
		Assert.Equal(3, html.Split("media=\"(max-width: 599px)\"").Length - 1);
	}

	[Fact]
	public async Task Art_directed_source_inherits_undeclared_values_from_the_parent()
	{
		string html = await RenderAsync(
			versionToken: "abc123",
			childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800)]);

		// The source declares neither a url nor a version token, so both are inherited from the parent.
		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/_v_abc123/images/test.webp\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Art_directed_source_never_repeats_the_id_across_its_generated_sources()
	{
		string html = await RenderAsync(childSources:
		[
			new ArtDirectedSource("(max-width: 599px)", 600, 800)
			{
				AdditionalAttributes = new Dictionary<string, object> { ["id"] = "hero-small", ["sizes"] = "50vw" }
			}
		]);

		// One component renders several source tags, so repeating the id would produce invalid HTML.
		Assert.DoesNotContain("hero-small", html, StringComparison.Ordinal);
		Assert.Equal(2, html.Split("sizes=\"50vw\"").Length - 1);
	}

	[Fact]
	public async Task External_url_renders_the_image_without_any_sources()
	{
		string html = await RenderAsync(url: "https://cdn.example.com/images/test.jpg");

		Assert.DoesNotContain("<source", html, StringComparison.Ordinal);
		Assert.Contains("src=\"https://cdn.example.com/images/test.jpg\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Art_directed_source_inherits_a_source_path_resolved_asynchronously()
	{
		// The url is a placeholder that only the override can turn into a real path, so a source rendering the resolved path proves it saw
		// the value produced during OnParametersSetAsync rather than the one supplied as a parameter.
		string html = await RenderAsync<AssetDynamicImage>(
			url: "asset://catalogue-hero",
			childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800)]);

		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/catalogue-hero.webp\"", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/catalogue-hero.jpg\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Source_that_supplies_its_own_url_resolves_it_rather_than_inheriting()
	{
		string html = await RenderAsync(childSources:
		[
			new ArtDirectedSource("(max-width: 599px)", 600, 800) { Url = "/images/other.jpg" }
		]);

		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/other.webp\"", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/test.jpg\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Unresolved_source_is_reported_after_resolution_rather_than_when_parameters_are_set()
	{
		// Url is no longer validated in OnParametersSet, because an override may identify the image by something other than a URL. The
		// absence of any source has to be reported after resolution instead.
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(url: "   "));

		Assert.Contains("A source could not be resolved", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Source_with_an_external_url_of_its_own_is_rejected()
	{
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(childSources:
		[
			new ArtDirectedSource("(max-width: 599px)", 600, 800) { Url = "https://cdn.example.com/images/other.jpg" }
		]));

		Assert.Contains("cannot be used with an external URL", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Sources_nested_inside_an_external_url_image_are_rejected()
	{
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(
			url: "https://cdn.example.com/images/test.jpg",
			childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800)]));

		Assert.Contains("cannot be nested inside", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Source_overriding_HasOwnSource_is_asked_for_its_own_image()
	{
		string html = await RenderAsync<AssetDynamicImage>(
			url: "asset://catalogue-hero",
			childSources: [new ArtDirectedSource("(max-width: 599px)", 600, 800) { AssetId = "breakpoint" }]);

		// The component carries no Url, so without HasOwnSource it would silently inherit the parent image at its own crop.
		Assert.Contains("srcset=\"/dynamicimage/600/800/Crop/jpg/images/breakpoint.webp\"", html, StringComparison.Ordinal);
		Assert.Contains("src=\"/dynamicimage/100/50/Crop/jpg/images/catalogue-hero.jpg\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Source_overriding_HasOwnSource_without_a_resolver_fails_rather_than_inheriting()
	{
		// The default resolver only considers the url of the source that is asking, so a component claiming its own image but supplying
		// nothing the resolver understands fails instead of quietly rendering the parent image.
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => RenderAsync(childSources:
		[
			new ArtDirectedSource("(max-width: 599px)", 600, 800) { AssetId = "breakpoint" }
		]));

		Assert.Contains("A source could not be resolved", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// A source that identifies its image by an asset reference rather than a URL.
	/// </summary>
	private sealed class AssetDynamicImageSource : UmbrellaDynamicImageSource
	{
		[Parameter]
		public string? AssetId { get; set; }

		protected override bool HasOwnSource => !string.IsNullOrWhiteSpace(AssetId);
	}

	/// <summary>
	/// An image that resolves an asset reference from whichever element is asking.
	/// </summary>
	private sealed class AssetDynamicImage : UmbrellaDynamicImage
	{
		protected override async Task<string> ResolveSourcePathAsync(UmbrellaDynamicImageSource? source)
		{
			await Task.Yield();

			if (source is AssetDynamicImageSource { AssetId: { Length: > 0 } assetId })
				return $"/images/{assetId}.jpg";

			string? url = source is not null ? source.Url : Url;

			return url is not null && url.StartsWith("asset://", StringComparison.Ordinal)
				? $"/images/{url["asset://".Length..]}.jpg"
				: string.Empty;
		}
	}

	private sealed record ArtDirectedSource(string Media, int? WidthRequest, int? HeightRequest)
	{
		public DynamicResizeMode? ResizeMode { get; init; }

		public string? Url { get; init; }

		public string? AssetId { get; init; }

		public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; init; }
	}

	private static Task<string> RenderAsync(
		UmbrellaDynamicImageOptions? options = null,
		string url = "/images/test.jpg",
		string? versionToken = null,
		IReadOnlyCollection<ArtDirectedSource>? childSources = null)
		=> RenderAsync<UmbrellaDynamicImage>(options, url, versionToken, childSources);

	private static Task<string> RenderAsync<TImage>(
		string url,
		IReadOnlyCollection<ArtDirectedSource>? childSources = null)
		where TImage : UmbrellaDynamicImage
		=> RenderAsync<TImage>(null, url, null, childSources);

	private static async Task<string> RenderAsync<TImage>(
		UmbrellaDynamicImageOptions? options,
		string url,
		string? versionToken,
		IReadOnlyCollection<ArtDirectedSource>? childSources)
		where TImage : UmbrellaDynamicImage
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(options ?? new UmbrellaDynamicImageOptions());
		_ = services.AddSingleton<IDynamicImageUtility>(new DynamicImageUtility(CoreUtilitiesMocks.CreateLogger<DynamicImageUtility>()));
		_ = services.AddSingleton(CoreUtilitiesMocks.CreateResponsiveImageHelper());

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		var parameters = new Dictionary<string, object?>
		{
			[nameof(UmbrellaDynamicImage.Url)] = url,
			[nameof(UmbrellaDynamicImage.WidthRequest)] = 100,
			[nameof(UmbrellaDynamicImage.HeightRequest)] = 50,
			[nameof(UmbrellaDynamicImage.MaxPixelDensity)] = 1
		};

		if (versionToken is not null)
			parameters[nameof(UmbrellaDynamicImage.VersionToken)] = versionToken;

		if (childSources is { Count: > 0 })
			parameters[nameof(UmbrellaDynamicImage.ChildContent)] = BuildChildContent(childSources);

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await renderer.RenderComponentAsync<TImage>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}

	private static RenderFragment BuildChildContent(IReadOnlyCollection<ArtDirectedSource> childSources) => builder =>
	{
		foreach (ArtDirectedSource source in childSources)
		{
			if (source.AssetId is not null)
				builder.OpenComponent<AssetDynamicImageSource>(0);
			else
				builder.OpenComponent<UmbrellaDynamicImageSource>(1);

			if (source.AssetId is not null)
				builder.AddComponentParameter(2, nameof(AssetDynamicImageSource.AssetId), source.AssetId);

			builder.AddComponentParameter(3, nameof(UmbrellaDynamicImageSource.Media), source.Media);
			if (source.WidthRequest.HasValue)
				builder.AddComponentParameter(4, nameof(UmbrellaDynamicImageSource.WidthRequest), source.WidthRequest.Value);
			if (source.HeightRequest.HasValue)
				builder.AddComponentParameter(5, nameof(UmbrellaDynamicImageSource.HeightRequest), source.HeightRequest.Value);
			if (source.ResizeMode.HasValue)
				builder.AddComponentParameter(6, nameof(UmbrellaDynamicImageSource.ResizeMode), source.ResizeMode.Value);

			if (source.Url is not null)
				builder.AddComponentParameter(7, nameof(UmbrellaDynamicImageSource.Url), source.Url);

			if (source.AdditionalAttributes is not null)
				builder.AddMultipleAttributes(8, source.AdditionalAttributes!);

			builder.CloseComponent();
		}
	};
}
