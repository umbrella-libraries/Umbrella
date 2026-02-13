using Umbrella.AppFramework.Services.Abstractions;

namespace Umbrella.AppFramework.Options;

/// <summary>
/// Options for the <see cref="IDialogService"/> .
/// </summary>
public class DialogServiceOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the close icon is displayed.
	/// </summary>
	public bool ShowCloseIcon { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the close button icon is rendered in the dialog.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="true"/> . Set this to <see langword="false"/> if you want to use a custom close button
	/// icon instead of the default one.
	/// </remarks>
	public bool RenderCloseButtonIcon { get; set; } = true;
}