using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// The root host component for the Umbrella dialog system.
/// Place this component once in the application layout to enable dialog rendering.
/// Each active dialog is rendered with its <see cref="UmbrellaDialogInstance"/> cascaded to the dialog component.
/// </summary>
public sealed partial class UmbrellaDialogHost : ComponentBase, IDisposable, IAsyncDisposable
{
	private bool _isInteropInitialized;
	private bool _isDisposed;

	/// <summary>
	/// Gets the dialog service used to manage dialogs.
	/// </summary>
	[Inject]
	private IUmbrellaDialogService DialogService { get; set; } = null!;

	[Inject]
	private IJSRuntime JSRuntime { get; set; } = null!;

	/// <inheritdoc/>
	protected override void OnInitialized() => DialogService.OnChanged += OnDialogChanged;

	/// <inheritdoc/>
	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (_isInteropInitialized || _isDisposed)
			return;

		try
		{
			await JSRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.initializeDialogHost");
			_isInteropInitialized = true;
		}
		catch (InvalidOperationException)
		{
			// JavaScript interop is unavailable during static rendering / prerendering.
		}
		catch (JSDisconnectedException)
		{
			// The browser connection ended while the host was being initialized.
		}
		catch (JSException)
		{
			// Keep rendering when the package script has not loaded yet and retry after a later render.
		}
	}

	private void OnDialogChanged(object? sender, EventArgs e) => StateHasChanged();

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		Dispose(true);

		try
		{
			if (_isInteropInitialized)
				await JSRuntime.InvokeVoidAsync("UmbrellaBlazorInterop.disposeDialogHost");
		}
		catch (InvalidOperationException)
		{
			// JavaScript interop is unavailable during static rendering / prerendering.
		}
		catch (JSDisconnectedException)
		{
			// The browser connection has already ended, so there is nothing left to clean up.
		}
		catch (JSException)
		{
			// The package script may already have been unloaded during navigation.
		}

		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (disposing && !_isDisposed)
		{
			_isDisposed = true;
			DialogService.OnChanged -= OnDialogChanged;
		}
	}
}
