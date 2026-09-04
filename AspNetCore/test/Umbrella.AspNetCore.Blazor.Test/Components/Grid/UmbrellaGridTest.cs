using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.Grid;
using Umbrella.AspNetCore.Blazor.Components.Grid.Options;
using Umbrella.AspNetCore.Blazor.Services.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Components.Grid;

public sealed class UmbrellaGridTest
{
	[Fact]
	public async Task Caption_item_names_default_to_item_and_items()
	{
		string singularHtml = await RenderAsync(new UmbrellaGridDataResponse<TestItem>([new TestItem()], 1));
		string pluralHtml = await RenderAsync(new UmbrellaGridDataResponse<TestItem>([new TestItem(), new TestItem()], 2));

		Assert.Contains(">Showing 1 of 1 item</caption>", singularHtml, StringComparison.Ordinal);
		Assert.Contains(">Showing items 1 to 2 of 2</caption>", pluralHtml, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Caption_uses_custom_singular_item_name_for_one_total_result()
	{
		string html = await RenderAsync(
			new UmbrellaGridDataResponse<TestItem>([new TestItem()], 1),
			captionItemName: "conversation");

		Assert.Contains(">Showing 1 of 1 conversation</caption>", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Caption_uses_custom_plural_item_name_for_multiple_total_results()
	{
		string html = await RenderAsync(
			new UmbrellaGridDataResponse<TestItem>([new TestItem(), new TestItem()], 2),
			captionItemNamePlural: "conversations");

		Assert.Contains(">Showing conversations 1 to 2 of 2</caption>", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Zero_results_retain_the_empty_state_without_a_caption()
	{
		string html = await RenderAsync(new UmbrellaGridDataResponse<TestItem>([], 0));

		Assert.DoesNotContain("u-grid__caption", html, StringComparison.Ordinal);
		Assert.Contains("There is either no data to display or your search options have no results.", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Caption_uses_plural_item_name_and_existing_range_semantics_for_paged_results()
	{
		string html = await RenderAsync(
			new UmbrellaGridDataResponse<TestItem>([new TestItem()], 21, 3, 10),
			captionItemName: "contact",
			captionItemNamePlural: "contacts");

		Assert.Contains(">Showing contacts 21 to 21 of 21</caption>", html, StringComparison.Ordinal);
	}

	private static async Task<string> RenderAsync(
		UmbrellaGridDataResponse<TestItem> response,
		string? captionItemName = null,
		string? captionItemNamePlural = null)
	{
		var browserEventAggregator = new Mock<IBrowserEventAggregator>();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new UmbrellaGridOptions());
		_ = services.AddSingleton<NavigationManager, TestNavigationManager>();
		_ = services.AddSingleton(new Mock<IUmbrellaBlazorInteropService>().Object);
		_ = services.AddSingleton(new Mock<IUmbrellaDialogService>().Object);
		_ = services.AddSingleton(new Mock<IAppSessionStorageService>().Object);
		_ = services.AddSingleton(browserEventAggregator.Object);
		_ = services.AddSingleton(new Lazy<IBrowserEventAggregator>(() => browserEventAggregator.Object));

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		var parameters = new Dictionary<string, object?>
		{
			[nameof(UmbrellaGrid<TestItem>.ShowFilters)] = false,
			[nameof(UmbrellaGrid<TestItem>.ShowPagination)] = false,
			[nameof(UmbrellaGrid<TestItem>.AutoScrollTop)] = false,
			[nameof(UmbrellaGrid<TestItem>.OnDataRequestedAsync)] = new Func<UmbrellaGridDataRequest, CancellationToken, Task<UmbrellaGridDataResponse<TestItem>?>>((_, _) => Task.FromResult<UmbrellaGridDataResponse<TestItem>?>(response))
		};

		if (captionItemName is not null)
			parameters[nameof(UmbrellaGrid<TestItem>.CaptionItemName)] = captionItemName;

		if (captionItemNamePlural is not null)
			parameters[nameof(UmbrellaGrid<TestItem>.CaptionItemNamePlural)] = captionItemNamePlural;

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			HtmlRootComponent output = await renderer.RenderComponentAsync<UmbrellaGrid<TestItem>>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}

	private sealed record TestItem;

	private sealed class TestNavigationManager : NavigationManager
	{
		public TestNavigationManager()
		{
			Initialize("https://localhost/", "https://localhost/grid");
		}

		protected override void NavigateToCore(string uri, bool forceLoad)
		{
		}
	}
}
