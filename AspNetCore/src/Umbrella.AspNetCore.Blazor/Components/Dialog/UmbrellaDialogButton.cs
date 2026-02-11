


namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// A button that is rendered as part of the <see cref="UmbrellaDialog"/>.
/// </summary>
public class UmbrellaDialogButton
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDialogButton"/> class.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="buttonType">Type of the button.</param>
	/// <param name="isCancel">if set to <c>true</c> specifies that this button is the cancel button.</param>
	/// <param name="navigateUrl">The navigate URL.</param>
	/// <param name="flexibleWidth">if set to <c>true</c> applies the btn--flexible css class to the button.</param>
	public UmbrellaDialogButton(string text, UmbrellaDialogButtonType buttonType, bool isCancel = false, string? navigateUrl = null, bool flexibleWidth = false)
	{
		Text = text;
		ButtonType = buttonType;
		IsCancel = isCancel;
		NavigateUrl = navigateUrl;
		FlexibleWidth = flexibleWidth;
	}

	/// <summary>
	/// Gets or sets the text.
	/// </summary>
	public string Text { get; set; }

	/// <summary>
	/// Gets or sets the type of the button.
	/// </summary>
	public UmbrellaDialogButtonType ButtonType { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is the cancel button.
	/// </summary>
	public bool IsCancel { get; set; }

	/// <summary>
	/// Gets or sets the URL to navigate to when the button is clicked.
	/// </summary>
	public string? NavigateUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the button has a flexible width. If set to <see langword="true"/>, applies the btn--flexible css class to the button.
	/// </summary>
	public bool FlexibleWidth { get; set; }

	/// <summary>
	/// Gets the CSS class.
	/// </summary>
	public string CssClass => ButtonType switch
	{
		UmbrellaDialogButtonType.Danger => "danger",
		UmbrellaDialogButtonType.Default => "secondary",
		UmbrellaDialogButtonType.Info => "info",
		UmbrellaDialogButtonType.Primary => "primary",
		UmbrellaDialogButtonType.Success => "success",
		UmbrellaDialogButtonType.Warning => "warning",
		_ => "secondary"
	};
}