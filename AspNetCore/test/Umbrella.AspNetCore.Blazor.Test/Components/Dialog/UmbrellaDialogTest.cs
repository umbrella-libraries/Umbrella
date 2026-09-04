using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Dialog;

namespace Umbrella.AspNetCore.Blazor.Test.Components.Dialog;

public sealed class UmbrellaDialogTest
{
	[Fact]
	public async Task Default_header_names_dialog_with_visible_title()
	{
		string html = await RenderAsync(new UmbrellaDialogInstance { Title = "Account details" });
		int titleIdStart = html.IndexOf("id=\"u-dialog-title-", StringComparison.Ordinal) + 4;
		int titleIdEnd = html.IndexOf('"', titleIdStart);

		Assert.True(titleIdStart >= 4);
		Assert.True(titleIdEnd > titleIdStart);

		string titleId = html[titleIdStart..titleIdEnd];

		Assert.Contains("role=\"dialog\"", html, StringComparison.Ordinal);
		Assert.Contains("aria-modal=\"true\"", html, StringComparison.Ordinal);
		Assert.Contains($"aria-labelledby=\"{titleId}\"", html, StringComparison.Ordinal);
		Assert.Contains(">Account details</h5>", html, StringComparison.Ordinal);
		Assert.DoesNotContain("aria-label=", html, StringComparison.Ordinal);
		Assert.DoesNotContain("role=\"document\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Hidden_header_names_dialog_directly()
	{
		string html = await RenderAsync(
			new UmbrellaDialogInstance { Title = "Delete account" },
			new Dictionary<string, object> { [nameof(UmbrellaDialog.ShowHeader)] = false });

		Assert.Contains("role=\"dialog\"", html, StringComparison.Ordinal);
		Assert.Contains("aria-modal=\"true\"", html, StringComparison.Ordinal);
		Assert.Contains("aria-label=\"Delete account\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("aria-labelledby=", html, StringComparison.Ordinal);
		Assert.DoesNotContain("class=\"modal-title\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Empty_title_has_dialog_fallback_name()
	{
		string html = await RenderAsync(new UmbrellaDialogInstance());

		Assert.Contains("aria-label=\"Dialog\"", html, StringComparison.Ordinal);
		Assert.DoesNotContain("aria-labelledby=", html, StringComparison.Ordinal);
	}

	private static async Task<string> RenderAsync(UmbrellaDialogInstance instance, IReadOnlyDictionary<string, object>? dialogParameters = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<NavigationManager, TestNavigationManager>();
		_ = services.AddSingleton(new Mock<IJSRuntime>().Object);

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			RenderFragment content = builder =>
			{
				builder.OpenComponent<UmbrellaDialog>(0);

				if (dialogParameters is not null)
					builder.AddMultipleAttributes(1, dialogParameters);

				builder.CloseComponent();
			};

			var parameters = new Dictionary<string, object?>
			{
				[nameof(CascadingValue<UmbrellaDialogInstance>.Value)] = instance,
				[nameof(CascadingValue<UmbrellaDialogInstance>.IsFixed)] = true,
				[nameof(CascadingValue<UmbrellaDialogInstance>.ChildContent)] = content
			};

			HtmlRootComponent output = await renderer.RenderComponentAsync<CascadingValue<UmbrellaDialogInstance>>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}

	private sealed class TestNavigationManager : NavigationManager
	{
		public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/dialog");

		protected override void NavigateToCore(string uri, bool forceLoad)
		{
		}
	}
}
