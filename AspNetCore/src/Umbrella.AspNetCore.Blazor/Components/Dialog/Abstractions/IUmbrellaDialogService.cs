
using Blazored.Modal;
using Blazored.Modal.Services;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Constants;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;

/// <summary>
/// Used to show dialogs in Blazor applications.
/// </summary>
/// <seealso cref="IDialogService" />
public interface IUmbrellaDialogService : IDialogService
{
	/// <summary>
	/// Shows a confirmation success dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="acceptButtonText">The accept button text.</param>
	/// <param name="cancelButtonText">The cancel button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned with a value indicating whether the message was confirmed or not by the user.</returns>
	ValueTask<bool> ShowConfirmSuccessMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText);

	/// <summary>
	/// Shows a confirmation danger dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="acceptButtonText">The accept button text.</param>
	/// <param name="cancelButtonText">The cancel button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned with a value indicating whether the message was confirmed or not by the user.</returns>
	ValueTask<bool> ShowConfirmDangerMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText);

	/// <summary>
	/// Shows a confirmation info dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="acceptButtonText">The accept button text.</param>
	/// <param name="cancelButtonText">The cancel button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned with a value indicating whether the message was confirmed or not by the user.</returns>
	ValueTask<bool> ShowConfirmInfoMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText);

	/// <summary>
	/// Shows a confirmation warning dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="acceptButtonText">The accept button text.</param>
	/// <param name="cancelButtonText">The cancel button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned with a value indicating whether the message was confirmed or not by the user.</returns>
	ValueTask<bool> ShowConfirmWarningMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText);

	/// <summary>
	/// Shows a custom dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="cssClass">The custom css class applied to the dialog container.</param>
	/// <param name="buttons">The dialog buttons displayed at the bottom of the dialog.</param>
	/// <param name="subTitle">The sub title.</param>
	/// <param name="showCloseIcon">If set to <see langword="true"/> the close icon is shown in the top right of the dialog (if supported on the target platform).</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned</returns>
	ValueTask<ModalResult> ShowDialogAsync(string message, string title, string cssClass, IReadOnlyCollection<UmbrellaDialogButton> buttons, string? subTitle = null, bool showCloseIcon = false);

	/// <summary>
	/// Shows a custom dialog.
	/// </summary>
	/// <typeparam name="T">The type of the component rendered inside the dialog.</typeparam>
	/// <param name="title">The title.</param>
	/// <param name="cssClass">The custom css class applied to the dialog container.</param>
	/// <param name="modalParameters">The modal parameters used to pass data to the dialog instance.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask<ModalResult> ShowDialogAsync<T>(string title, string cssClass, ModalParameters? modalParameters = null)
		where T : ComponentBase;

	/// <summary>
	/// Shows a custom dialog.
	/// </summary>
	/// <typeparam name="T">The type of the component rendered inside the dialog.</typeparam>
	/// <typeparam name="TResult">The type of the result.</typeparam>
	/// <param name="title">The title.</param>
	/// <param name="cssClass">The custom css class applied to the dialog container.</param>
	/// <param name="modalParameters">The modal parameters used to pass data to the dialog instance.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask<TResult> ShowDialogAsync<T, TResult>(string title, string cssClass, ModalParameters? modalParameters = null)
		where T : ComponentBase
		where TResult : ModalResult;
}