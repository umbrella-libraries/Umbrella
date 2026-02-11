


namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// The dialog size. This is used to control the maximum width of the dialog.
/// </summary>
public enum UmbrellaDialogSize
{
	/// <summary>
	/// The default size which is 500px when using Bootstrap.
	/// </summary>
	Default,

	/// <summary>
	/// The small size which has a maximum width of 300px when using Bootstrap.
	/// </summary>
	Small,

	/// <summary>
	/// The large size which has a maximum width of 800px when using Bootstrap.
	/// </summary>
	Large,

	/// <summary>
	/// The extra large size which has a maximum width of 1140px when using Bootstrap.
	/// </summary>
	ExtraLarge,

	/// <summary>
	/// Makes the dialog cover the entire browser viewport.
	/// </summary>
	FullScreen
}