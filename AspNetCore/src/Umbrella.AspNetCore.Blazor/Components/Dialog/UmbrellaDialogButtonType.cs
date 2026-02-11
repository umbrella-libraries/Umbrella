


namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// The type of the dialog button. This is primarily used to determine how the button is styled.
/// </summary>
public enum UmbrellaDialogButtonType
{
	/// <summary>
	/// The default button. Applies btn-secondary.
	/// </summary>
	Default,

	/// <summary>
	/// The success button. Applies btn-success.
	/// </summary>
	Success,

	/// <summary>
	/// The info button. Applies btn-info.
	/// </summary>
	Info,

	/// <summary>
	/// The warning button. Applies btn-warning.
	/// </summary>
	Warning,

	/// <summary>
	/// The danger button. Applies btn-danger.
	/// </summary>
	Danger,

	/// <summary>
	/// The primary button. Applies btn-primary.
	/// </summary>
	Primary
}