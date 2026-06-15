namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// Represents the context of a dialog being shown. Provided as a cascading parameter to dialog components.
/// </summary>
public sealed class UmbrellaDialogInstance
{
	private readonly TaskCompletionSource<ModalResult> _tcs = new();

	/// <summary>
	/// Gets the dialog title.
	/// </summary>
	public string Title { get; init; } = "";

	/// <summary>
	/// Gets the CSS class applied to the dialog container.
	/// </summary>
	public string? CssClass { get; init; }

	/// <summary>
	/// Gets a value indicating whether clicking the dialog background cancels the dialog.
	/// </summary>
	public bool DisableBackgroundCancel { get; init; }

	/// <summary>
	/// Gets or sets a value indicating whether the dialog header is hidden.
	/// Mutated by <see cref="UmbrellaDialog"/> during initialisation.
	/// </summary>
	public bool? HideHeader { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the close button is hidden.
	/// Mutated by <see cref="UmbrellaDialog"/> during initialisation.
	/// </summary>
	public bool? HideCloseButton { get; set; }

	internal Task<ModalResult> Result => _tcs.Task;

	/// <summary>
	/// Cancels the dialog.
	/// </summary>
	public Task CancelAsync()
	{
		_ = _tcs.TrySetResult(ModalResult.Cancel());
		return Task.CompletedTask;
	}

	/// <summary>
	/// Closes the dialog with the specified result.
	/// </summary>
	public Task CloseAsync(ModalResult result)
	{
		_ = _tcs.TrySetResult(result);
		return Task.CompletedTask;
	}
}
