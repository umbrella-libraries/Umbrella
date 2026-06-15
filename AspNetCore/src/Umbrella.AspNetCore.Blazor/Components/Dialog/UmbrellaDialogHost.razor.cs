using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// The root host component for the Umbrella dialog system.
/// Place this component once in the application layout to enable dialog rendering.
/// </summary>
public sealed partial class UmbrellaDialogHost : ComponentBase, IDisposable
{
	/// <summary>
	/// Gets the dialog service used to manage dialogs.
	/// </summary>
	[Inject]
	private IUmbrellaDialogService DialogService { get; set; } = null!;

	/// <inheritdoc/>
	protected override void OnInitialized() => DialogService.OnChanged += OnDialogChanged;

	private void OnDialogChanged(object? sender, EventArgs e) => StateHasChanged();

	/// <inheritdoc/>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (disposing)
			DialogService.OnChanged -= OnDialogChanged;
	}
}