using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Umbrella.AspNetCore.Blazor.Constants;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// A dialog component rendered by the <see cref="UmbrellaDialogHost"/> infrastructure.
/// </summary>
/// <seealso cref="ComponentBase" />
public partial class UmbrellaDialog
{
	[Inject]
	private NavigationManager Navigation { get; [RequiresUnreferencedCode(TrimConstants.DI)] set; } = null!;

	/// <summary>
	/// Gets or sets the dialog instance as a cascading parameter.
	/// </summary>
	[CascadingParameter]
	protected UmbrellaDialogInstance ModalInstance { get; set; } = null!;

	/// <summary>
	/// Gets or sets the size.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="UmbrellaDialogSize.Default"/>.
	/// </remarks>
	[Parameter]
	public UmbrellaDialogSize Size { get; set; } = UmbrellaDialogSize.Default;

	/// <summary>
	/// Gets or sets a value indicating whether to show the header.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="true"/>.
	/// </remarks>
	[Parameter]
	public bool ShowHeader { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to show the close button.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="false"/>.
	/// </remarks>
	[Parameter]
	public bool ShowCloseButton { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to render the close button icon. Set this to <see langword="false"/> if you want to use a custom close button icon instead of the default one.
	/// </summary>
	/// <remarks>
	/// Defaults to <see langword="true"/>.
	/// </remarks>
	[Parameter]
	public bool RenderCloseButtonIcon { get; set; } = true;

	/// <summary>
	/// An optional CSS class which will override the default close button icon CSS class.
	/// </summary>
	[Parameter]
	public string? CloseButtonIconCssClassOverride { get; set; }

	/// <summary>
	/// Gets or sets the sub title.
	/// </summary>
	[Parameter]
	public string? SubTitle { get; set; }

	/// <summary>
	/// Gets or sets the message.
	/// </summary>
	[Parameter]
	public string? Message { get; set; }

	/// <summary>
	/// Gets or sets the buttons.
	/// </summary>
	[Parameter]
	public IReadOnlyCollection<UmbrellaDialogButton>? Buttons { get; set; }

	/// <summary>
	/// Gets or sets the custom content of the dialog.
	/// </summary>
	/// <remarks>
	/// <c>
	/// Use <see cref="Header" />, <see cref="Body" /> and <see cref="Footer" /> parameters instead. The <see cref="Body" /> parameter directly replaces this property.
	/// </c>
	/// </remarks>
	[Parameter]
	[Obsolete("Use Header, Body and Footer parameters instead. The Body parameter directly replaces this property.")]
	public RenderFragment? ChildContent { get; set; }

	/// <summary>
	/// Gets or sets the custom header content of the dialog.
	/// </summary>
	[Parameter]
	public RenderFragment? Header { get; set; }

	/// <summary>
	/// Gets or sets the custom body content of the dialog.
	/// </summary>
	[Parameter]
	public RenderFragment? Body { get; set; }

	/// <summary>
	/// Gets or sets the custom footer content of the dialog.
	/// </summary>
	[Parameter]
	public RenderFragment? Footer { get; set; }

	/// <summary>
	/// Gets the dialog size CSS class based on the value of the <see cref="Size"/> property.
	/// </summary>
	protected string? DialogSizeCssClass => Size switch
	{
		UmbrellaDialogSize.Default => null,
		UmbrellaDialogSize.Small => "modal-sm",
		UmbrellaDialogSize.Large => "modal-lg",
		UmbrellaDialogSize.ExtraLarge => "modal-xl",
		UmbrellaDialogSize.FullScreen => "modal-fullscreen",
		_ => throw new SwitchExpressionException(Size)
	};

	/// <inheritdoc/>
	protected override void OnInitialized()
	{
		base.OnInitialized();

		ModalInstance.HideHeader = !ShowHeader;
		ModalInstance.HideCloseButton = !ShowCloseButton;
	}

	/// <summary>
	/// Handles clicks on the modal background.
	/// </summary>
	protected async Task BackgroundClickAsync()
	{
		if (ModalInstance.DisableBackgroundCancel is not true)
			await ModalInstance.CancelAsync();
	}

	/// <summary>
	/// Handles close button clicks and closes the current dialog.
	/// </summary>
	protected async Task CloseClickAsync() => await ModalInstance.CancelAsync();

	/// <summary>
	/// Handles a button click of one of the specified <see cref="Buttons"/>.
	/// </summary>
	/// <param name="button">The clicked button.</param>
	protected async Task ButtonClickAsync(UmbrellaDialogButton button)
	{
		ArgumentNullException.ThrowIfNull(button);

		if (!string.IsNullOrWhiteSpace(button.NavigateUrl))
		{
			Navigation.NavigateTo(button.NavigateUrl);
		}
		else if (button.IsCancel)
		{
			await ModalInstance.CancelAsync();
		}
		else
		{
			await ModalInstance.CloseAsync(ModalResult.Ok(button));
		}
	}
}
