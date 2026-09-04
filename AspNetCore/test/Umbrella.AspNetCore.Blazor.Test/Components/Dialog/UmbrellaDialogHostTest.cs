using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Dialog;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Components.Dialog;

public sealed class UmbrellaDialogHostTest
{
	[Fact]
	public async Task Removing_non_top_dialog_preserves_remaining_dialog_component_instance()
	{
		ProbeDialog.Reset();

		var entries = new List<UmbrellaDialogEntry>
		{
			CreateEntry("First"),
			CreateEntry("Second")
		};

		var dialogService = new Mock<IUmbrellaDialogService>();
		_ = dialogService.SetupGet(x => x.ActiveDialogs).Returns(entries);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(dialogService.Object);
		_ = services.AddSingleton(new Mock<IJSRuntime>().Object);

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		await renderer.Dispatcher.InvokeAsync(async () =>
		{
			_ = await renderer.RenderComponentAsync<UmbrellaDialogHost>();

			entries.RemoveAt(0);
			dialogService.Raise(x => x.OnChanged += null, EventArgs.Empty);
		});

		Assert.Equal(1, ProbeDialog.GetInitializationCount("First"));
		Assert.Equal(1, ProbeDialog.GetInitializationCount("Second"));
	}

	private static UmbrellaDialogEntry CreateEntry(string title)
		=> new(
			typeof(ProbeDialog),
			[],
			new UmbrellaDialogInstance { Title = title });

	private sealed class ProbeDialog : ComponentBase
	{
		private static readonly Dictionary<string, int> _initializationCounts = [];

		[CascadingParameter]
		public UmbrellaDialogInstance Instance { get; set; } = null!;

		public static int GetInitializationCount(string title) => _initializationCounts.GetValueOrDefault(title);

		public static void Reset() => _initializationCounts.Clear();

		protected override void OnInitialized()
		{
			base.OnInitialized();
			_initializationCounts[Instance.Title] = GetInitializationCount(Instance.Title) + 1;
		}

		protected override void BuildRenderTree(RenderTreeBuilder builder)
		{
		}
	}
}
