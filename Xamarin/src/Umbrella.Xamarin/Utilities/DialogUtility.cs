
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Constants;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;
using Umbrella.Xamarin.Exceptions;
using Xamarin.Forms;

namespace Umbrella.Xamarin.Utilities;

/// <summary>
/// A utility used to show application dialogs.
/// </summary>
public class DialogUtility : IDialogService
{
	private readonly ILogger _logger;
	private readonly IDialogTrackerService _dialogTracker;

	/// <summary>
	/// Initializes a new instance of the <see cref="DialogUtility"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dialogTracker">The dialog tracker.</param>
	public DialogUtility(
		ILogger<DialogUtility> logger,
		IDialogTrackerService dialogTracker)
	{
		_logger = logger;
		_dialogTracker = dialogTracker;
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmMessageAsync(string message, string title, string acceptButtonText = "Confirm", string cancelButtonText = "Cancel")
	{
		try
		{
			return await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, acceptButtonText, cancelButtonText));
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowDangerMessageAsync(string message = DialogDefaults.UnknownErrorMessage, string title = "Error", string closeButtonText = "Close")
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowInfoMessageAsync(string message, string title = "Information", string closeButtonText = "Close")
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title, closeButtonText }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowMessageAsync(string message, string title, string closeButtonText = "Close", bool showCloseIcon = false)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title, closeButtonText, showCloseIcon }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowSuccessMessageAsync(string message, string title = "Success", string closeButtonText = "Close")
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowValidationResultsMessageAsync(IEnumerable<ValidationResult> validationResults, string introMessage = "Please correct all validation errors.", string title = "Error", string closeButtonText = "Close")
	{
		try
		{
			string message = validationResults.ToValidationSummaryMessage(introMessage);

			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { title }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public ValueTask ShowProblemDetailsErrorMessageAsync(HttpProblemDetails? problemDetails, string title = "Error") => ShowDangerMessageAsync(problemDetails?.Detail ?? DialogDefaults.UnknownErrorMessage, title);

	/// <inheritdoc />
	public async ValueTask ShowOperationResultErrorMessageAsync(IOperationResult? operationResult, string title = "Error")
	{
		string? message = operationResult switch
		{
			IHttpOperationResult httpOperationResult => httpOperationResult.ProblemDetails?.Detail ?? httpOperationResult.PrimaryValidationMessage,
			not null => operationResult.PrimaryValidationMessage,
			_ => null
		};

		message ??= DialogDefaults.UnknownErrorMessage;

		await ShowDangerMessageAsync(message, title);
	}

	/// <inheritdoc />
	public async ValueTask ShowWarningMessageAsync(string message, string title = "Warning", string closeButtonText = "Close")
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			await Device.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlert(title, message, closeButtonText));

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaXamarinException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc/>
	public ValueTask ShowConcurrencyDangerMessageAsync(string message = DialogDefaults.ConcurrencyErrorMessage) => ShowDangerMessageAsync(message);
}