using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Blazor.Components.FileUpload;
using Umbrella.Utilities.Primitives;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Test.Components.FileUpload;

public sealed class UmbrellaManagedFileUploadTest
{
	[Fact]
	public async Task Existing_file_renders_view_replace_and_remove_actions()
	{
		string html = await RenderAsync(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt",
			[nameof(UmbrellaManagedFileUpload.OnRemoveFile)] = EventCallback.Factory.Create(new object(), () => { })
		});

		Assert.Contains("class=\"u-managed-file-upload__buttons\"", html, StringComparison.Ordinal);
		Assert.Contains("role=\"group\"", html, StringComparison.Ordinal);
		Assert.Contains("aria-label=\"Existing file actions\"", html, StringComparison.Ordinal);
		Assert.Contains("href=\"/files/transcript.vtt\"", html, StringComparison.Ordinal);
		Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
		Assert.Contains("rel=\"noopener noreferrer\"", html, StringComparison.Ordinal);
		Assert.Contains(">View File</a>", html, StringComparison.Ordinal);
		Assert.Contains(">Replace</button>", html, StringComparison.Ordinal);
		Assert.Contains(">Remove</button>", html, StringComparison.Ordinal);
		Assert.DoesNotContain("type=\"file\"", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Missing_existing_file_renders_file_upload_control()
	{
		string html = await RenderAsync();

		Assert.Contains("class=\"u-file-upload\"", html, StringComparison.Ordinal);
		Assert.Contains("type=\"file\"", html, StringComparison.Ordinal);
		Assert.Contains(">Choose file</label>", html, StringComparison.Ordinal);
		Assert.DoesNotContain("u-managed-file-upload__buttons", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Remove_button_is_not_rendered_without_callback()
	{
		string html = await RenderAsync(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt"
		});

		Assert.Contains(">View File</a>", html, StringComparison.Ordinal);
		Assert.Contains(">Replace</button>", html, StringComparison.Ordinal);
		Assert.DoesNotContain(">Remove</button>", html, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Replace_switches_to_upload_mode_and_invokes_callback()
	{
		bool callbackInvoked = false;
		UmbrellaManagedFileUpload component = CreateComponent(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt",
			[nameof(UmbrellaManagedFileUpload.OnReplaceFile)] = EventCallback.Factory.Create(new object(), () => callbackInvoked = true)
		});

		component.SynchronizeExistingFile();
		await component.ReplaceFileClickAsync();

		Assert.True(callbackInvoked);
		Assert.False(component.IsShowingExistingFile);
	}

	[Fact]
	public async Task Remove_without_warning_switches_to_upload_mode_and_invokes_callback()
	{
		bool callbackInvoked = false;
		UmbrellaManagedFileUpload component = CreateComponent(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt",
			[nameof(UmbrellaManagedFileUpload.ShowRemoveWarning)] = false,
			[nameof(UmbrellaManagedFileUpload.OnRemoveFile)] = EventCallback.Factory.Create(new object(), () => callbackInvoked = true)
		});

		component.SynchronizeExistingFile();
		await component.RemoveFileClickAsync();

		Assert.True(callbackInvoked);
		Assert.False(component.IsShowingExistingFile);
	}

	[Fact]
	public async Task Replacement_mode_survives_parent_rerender_until_existing_url_changes()
	{
		var initialParameters = new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt"
		};
		UmbrellaManagedFileUpload component = CreateComponent(initialParameters);

		component.SynchronizeExistingFile();
		await component.ReplaceFileClickAsync();

		ParameterView.FromDictionary(initialParameters).SetParameterProperties(component);
		component.SynchronizeExistingFile();
		Assert.False(component.IsShowingExistingFile);

		ParameterView.FromDictionary(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/replacement.vtt"
		}).SetParameterProperties(component);
		component.SynchronizeExistingFile();
		Assert.True(component.IsShowingExistingFile);
	}

	[Fact]
	public async Task Successful_same_url_replacement_restores_existing_file_actions()
	{
		UmbrellaManagedFileUpload component = CreateComponent(new Dictionary<string, object?>
		{
			[nameof(UmbrellaManagedFileUpload.ExistingFileUrl)] = "/files/transcript.vtt",
			[nameof(UmbrellaManagedFileUpload.OnRequestUpload)] = new Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>(_ => Task.FromResult<IOperationResult?>(OperationResult.Success()))
		});

		component.SynchronizeExistingFile();
		await component.ReplaceFileClickAsync();
		var result = OperationResult.Success();
		bool stateChanged = component.RestoreExistingFileAfterSuccessfulUpload(result);

		Assert.True(result.IsSuccess);
		Assert.True(stateChanged);
		Assert.True(component.IsShowingExistingFile);
	}

	private static UmbrellaManagedFileUpload CreateComponent(IDictionary<string, object?> parameters)
	{
		var component = new UmbrellaManagedFileUpload();
		ParameterView.FromDictionary(parameters).SetParameterProperties(component);
		return component;
	}

	private static async Task<string> RenderAsync(IReadOnlyDictionary<string, object?>? componentParameters = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(new Mock<IUmbrellaDialogService>().Object);
		_ = services.AddSingleton(new Mock<IJSRuntime>().Object);

		await using ServiceProvider serviceProvider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			var parameters = new Dictionary<string, object?>
			{
				[nameof(UmbrellaManagedFileUpload.OnRequestUpload)] = new Func<UmbrellaFileUploadRequestEventArgs, Task<IOperationResult?>>(_ => Task.FromResult<IOperationResult?>(null))
			};

			if (componentParameters is not null)
			{
				foreach ((string key, object? value) in componentParameters)
					parameters[key] = value;
			}

			HtmlRootComponent output = await renderer.RenderComponentAsync<UmbrellaManagedFileUpload>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}
}
