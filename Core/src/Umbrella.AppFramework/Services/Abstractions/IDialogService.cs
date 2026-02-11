


using System.ComponentModel.DataAnnotations;
using Umbrella.AppFramework.Services.Constants;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AppFramework.Services.Abstractions;

/// <summary>
/// A service used to show application dialogs.
/// </summary>
public interface IDialogService
{
	/// <summary>
	/// Shows a dialog with a message.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <param name="showCloseIcon">If set to <see langword="true"/> the close icon is shown in the top right of the dialog (if supported on the target platform).</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowMessageAsync(string message, string title, string closeButtonText = DialogDefaults.DefaultCloseButtonText, bool showCloseIcon = false);

	/// <summary>
	/// Shows a dialog with a message indicating danger.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowDangerMessageAsync(string message = DialogDefaults.UnknownErrorMessage, string title = "Error", string closeButtonText = DialogDefaults.DefaultCloseButtonText);

	/// <summary>
	/// Shows a dialog with a formatted message for the specified <paramref name="validationResults"/>.
	/// </summary>
	/// <param name="validationResults">The validation results.</param>
	/// <param name="introMessage">The intro message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowValidationResultsMessageAsync(IEnumerable<ValidationResult> validationResults, string introMessage = DialogDefaults.ValidationResultsIntroMessage, string title = "Error", string closeButtonText = DialogDefaults.DefaultCloseButtonText);

	/// <summary>
	/// Shows a friendly error message for the specified <paramref name="problemDetails"/>.
	/// </summary>
	/// <param name="problemDetails">The problem details.</param>
	/// <param name="title">The title.</param>
	/// <returns>A task that completes when the dialog has been actioned.</returns>
	ValueTask ShowProblemDetailsErrorMessageAsync(HttpProblemDetails? problemDetails, string title = "Error");

	/// <summary>
	/// Shows a friendly error message for the specified <paramref name="operationResult"/>.
	/// </summary>
	/// <param name="operationResult">The erroneous operation result.</param>
	/// <param name="title">The title.</param>
	/// <returns>A task that completes when the dialog has been actioned.</returns>
	ValueTask ShowOperationResultErrorMessageAsync(IOperationResult? operationResult, string title = "Error");

	/// <summary>
	/// Shows a dialog with a message indicating a concurrency error.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowConcurrencyDangerMessageAsync(string message = DialogDefaults.ConcurrencyErrorMessage);

	/// <summary>
	/// Shows a dialog with a message indicating success.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowSuccessMessageAsync(string message, string title = "Success", string closeButtonText = DialogDefaults.DefaultCloseButtonText);

	/// <summary>
	/// Shows a dialog with a message indicating information.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowInfoMessageAsync(string message, string title = "Information", string closeButtonText = DialogDefaults.DefaultCloseButtonText);

	/// <summary>
	/// Shows a dialog with a message indicating warning.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="closeButtonText">The close button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned.</returns>
	ValueTask ShowWarningMessageAsync(string message, string title = "Warning", string closeButtonText = DialogDefaults.DefaultCloseButtonText);

	/// <summary>
	/// Shows a confirmation dialog.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="title">The title.</param>
	/// <param name="acceptButtonText">The accept button text.</param>
	/// <param name="cancelButtonText">The cancel button text.</param>
	/// <returns>An awaitable task that completes when the dialog has been actioned with a value indicating whether the message was confirmed or not by the user.</returns>
	ValueTask<bool> ShowConfirmMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText);
}