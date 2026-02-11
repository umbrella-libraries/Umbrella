


using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Constants;
using Umbrella.AspNetCore.Blazor.Components.Dialog.Abstractions;
using Umbrella.AspNetCore.Shared.Extensions;
using Umbrella.AspNetCore.Shared.Services.Abstractions;
using Umbrella.Utilities.Http;
using Umbrella.Utilities.Http.Abstractions;
using Umbrella.Utilities.Primitives.Abstractions;

namespace Umbrella.AspNetCore.Blazor.Components.Dialog;

/// <summary>
/// Used to show dialogs in Blazor applications.
/// </summary>
/// <seealso cref="IUmbrellaDialogService" />
public class UmbrellaDialogService : IUmbrellaDialogService
{
	private static readonly ModalParameters _emptyModalParameters = [];

	private readonly ConcurrentDictionary<Type, IReadOnlyCollection<AuthorizeAttribute>> _authorizationAttributeCache = new();

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultMessageButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCloseButtonText, UmbrellaDialogButtonType.Primary)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultSuccessMessageButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCloseButtonText, UmbrellaDialogButtonType.Success)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultDangerMessageButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCloseButtonText, UmbrellaDialogButtonType.Danger)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultInfoMessageButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCloseButtonText, UmbrellaDialogButtonType.Info)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultWarningMessageButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCloseButtonText, UmbrellaDialogButtonType.Warning)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultConfirmButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCancelButtonText, UmbrellaDialogButtonType.Default, true),
		new UmbrellaDialogButton(DialogDefaults.DefaultConfirmButtonText, UmbrellaDialogButtonType.Primary)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultConfirmSuccessButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCancelButtonText, UmbrellaDialogButtonType.Default, true),
		new UmbrellaDialogButton(DialogDefaults.DefaultConfirmButtonText, UmbrellaDialogButtonType.Success)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultConfirmDangerButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCancelButtonText, UmbrellaDialogButtonType.Default, true),
		new UmbrellaDialogButton(DialogDefaults.DefaultConfirmButtonText, UmbrellaDialogButtonType.Danger)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultConfirmInfoButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCancelButtonText, UmbrellaDialogButtonType.Default, true),
		new UmbrellaDialogButton(DialogDefaults.DefaultConfirmButtonText, UmbrellaDialogButtonType.Info)
	};

	private readonly IReadOnlyCollection<UmbrellaDialogButton> _defaultConfirmWarningButtons = new[]
	{
		new UmbrellaDialogButton(DialogDefaults.DefaultCancelButtonText, UmbrellaDialogButtonType.Default, true),
		new UmbrellaDialogButton(DialogDefaults.DefaultConfirmButtonText, UmbrellaDialogButtonType.Warning)
	};

	private readonly ILogger _logger;
	private readonly IDialogTrackerService _dialogTracker;
	private readonly IModalService _modalService;
	private readonly IServiceProvider _serviceProvider;
	private readonly IHttpContextService _httpContextService;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDialogService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dialogTracker">The dialog tracker.</param>
	/// <param name="modalService">The modal service.</param>
	/// <param name="serviceProvider">The service provider.</param>
	/// <param name="httpContextService">The HTTP context service.</param>
	public UmbrellaDialogService(
		ILogger<UmbrellaDialogService> logger,
		IDialogTrackerService dialogTracker,
		IModalService modalService,
		IServiceProvider serviceProvider,
		IHttpContextService httpContextService)
	{
		_logger = logger;
		_dialogTracker = dialogTracker;
		_modalService = modalService;
		_serviceProvider = serviceProvider;
		_httpContextService = httpContextService;
	}

	/// <inheritdoc />
	public async ValueTask ShowMessageAsync(string message, string title, string closeButtonText = DialogDefaults.DefaultCloseButtonText, bool showCloseIcon = false)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Primary) };

			_ = await ShowDialogAsync(message, title, "u-dialog--message", buttons, showCloseIcon: showCloseIcon);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowDangerMessageAsync(string message = "An unknown error has occurred. Please try again.", string title = "Error", string closeButtonText = DialogDefaults.DefaultCloseButtonText)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultDangerMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Danger) };

			_ = await ShowDialogAsync(message, title, "u-dialog--message u-dialog--message-danger", buttons);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowInfoMessageAsync(string message, string title = "Information", string closeButtonText = DialogDefaults.DefaultCloseButtonText)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultInfoMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Info) };

			_logger.WriteInformation(message: "Opening info dialog");

			_ = await ShowDialogAsync(message, title, "u-dialog--message u-dialog--message-info", buttons);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowSuccessMessageAsync(string message, string title = "Success", string closeButtonText = DialogDefaults.DefaultCloseButtonText)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultSuccessMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Success) };

			_ = await ShowDialogAsync(message, title, "u-dialog--message u-dialog--message-success", buttons);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask ShowWarningMessageAsync(string message, string title = "Warning", string closeButtonText = DialogDefaults.DefaultCloseButtonText)
	{
		try
		{
			int code = _dialogTracker.GenerateCode(message, title, null, closeButtonText);

			if (!_dialogTracker.TrackOpen(code))
				return;

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultWarningMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Warning) };

			_ = await ShowDialogAsync(message, title, "u-dialog--message u-dialog--message-warning", buttons);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText)
	{
		try
		{
			var buttons = GetConfirmButtons(UmbrellaDialogButtonType.Primary, _defaultConfirmButtons, acceptButtonText, cancelButtonText);

			ModalResult result = await ShowDialogAsync(message, title, "u-dialog--confirm", buttons);

			return !result.Cancelled;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmDangerMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText)
	{
		try
		{
			var buttons = GetConfirmButtons(UmbrellaDialogButtonType.Danger, _defaultConfirmDangerButtons, acceptButtonText, cancelButtonText);

			ModalResult result = await ShowDialogAsync(message, title, "u-dialog--confirm u-dialog--confirm-danger", buttons);

			return !result.Cancelled;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmSuccessMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText)
	{
		try
		{
			var buttons = GetConfirmButtons(UmbrellaDialogButtonType.Success, _defaultConfirmSuccessButtons, acceptButtonText, cancelButtonText);

			ModalResult result = await ShowDialogAsync(message, title, "u-dialog--confirm u-dialog--confirm-success", buttons);

			return !result.Cancelled;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmInfoMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText)
	{
		try
		{
			var buttons = GetConfirmButtons(UmbrellaDialogButtonType.Info, _defaultConfirmInfoButtons, acceptButtonText, cancelButtonText);

			ModalResult result = await ShowDialogAsync(message, title, "u-dialog--confirm u-dialog--confirm-info", buttons);

			return !result.Cancelled;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<bool> ShowConfirmWarningMessageAsync(string message, string title, string acceptButtonText = DialogDefaults.DefaultConfirmButtonText, string cancelButtonText = DialogDefaults.DefaultCancelButtonText)
	{
		try
		{
			var buttons = GetConfirmButtons(UmbrellaDialogButtonType.Warning, _defaultConfirmWarningButtons, acceptButtonText, cancelButtonText);

			ModalResult result = await ShowDialogAsync(message, title, "u-dialog--confirm u-dialog--confirm-warning", buttons);

			return !result.Cancelled;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	/// <inheritdoc />
	public async ValueTask<ModalResult> ShowDialogAsync(string message, string title, string cssClass, IReadOnlyCollection<UmbrellaDialogButton> buttons, string? subTitle = null, bool showCloseIcon = false)
	{
		try
		{
			var parameters = new ModalParameters
			{
				{ nameof(UmbrellaDialog.SubTitle), subTitle ?? "" },
				{ nameof(UmbrellaDialog.Message), message },
				{ nameof(UmbrellaDialog.Buttons), buttons },
				{ nameof(UmbrellaDialog.ShowCloseButton), showCloseIcon }
			};

			var options = new ModalOptions
			{
				Class = cssClass,
				DisableBackgroundCancel = true,
				UseCustomLayout = true,
				HideCloseButton = !showCloseIcon
			};

			IModalReference modal = _modalService.Show<UmbrellaDialog>(title, parameters, options);

			return await modal.Result;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message, title, cssClass, buttons, subTitle }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
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

			var buttons = closeButtonText is DialogDefaults.DefaultCloseButtonText
				? _defaultDangerMessageButtons
				: new[] { new UmbrellaDialogButton(closeButtonText, UmbrellaDialogButtonType.Danger) };

			_ = await ShowDialogAsync(message, title, "u-dialog--message u-dialog--message-danger", buttons);

			_dialogTracker.Close(code);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { title }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
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
	public ValueTask<ModalResult> ShowDialogAsync<T>(string title, string cssClass, ModalParameters? modalParameters = null)
		where T : ComponentBase => ShowDialogAsync<T, ModalResult>(title, cssClass, modalParameters);

	/// <inheritdoc />
	public async ValueTask<TResult> ShowDialogAsync<T, TResult>(string title, string cssClass, ModalParameters? modalParameters = null)
		where T : ComponentBase
		where TResult : ModalResult
	{
		try
		{
			IReadOnlyCollection<AuthorizeAttribute> authorizeAttributes = _authorizationAttributeCache.GetOrAdd(typeof(T), key => key.GetCustomAttributes<AuthorizeAttribute>(true).ToArray());

			if (authorizeAttributes.Count > 0)
			{
				static void ThrowAccessDeniedException() => throw new UnauthorizedAccessException("The current user is not permitted to access the specified dialog.");

				AuthenticationStateProvider? authenticationStateProvider = _serviceProvider.GetService<AuthenticationStateProvider>();

				ClaimsPrincipal? claimsPrincipal = null;

				if (authenticationStateProvider is not null)
				{
					var authState = await authenticationStateProvider.GetAuthenticationStateAsync();

					claimsPrincipal = authState.User;
				}

				claimsPrincipal ??= _httpContextService.User ?? new ClaimsPrincipal(new ClaimsIdentity());

				if (claimsPrincipal?.Identity?.IsAuthenticated is not true)
					ThrowAccessDeniedException();

				// Manually resolve the service here from the provider as the service may not have been registered in the DI container
				// if the component is being used in a project that doesn't require authorization.
				IAuthorizationService authorizationService = _serviceProvider.GetRequiredService<IAuthorizationService>();

				// We will now check all authorization attributes. The first one that fails will throw an exception.
				foreach (AuthorizeAttribute authorizeAttribute in authorizeAttributes)
				{
					bool authorized = await authorizationService.AuthorizeRolesAndPolicyAsync(claimsPrincipal!, authorizeAttribute.Roles, authorizeAttribute.Policy).ConfigureAwait(false);

					if (!authorized)
						ThrowAccessDeniedException();
				}
			}

			var options = new ModalOptions
			{
				Class = cssClass,
				DisableBackgroundCancel = true,
				UseCustomLayout = true
			};

			IModalReference modal = modalParameters is not null
				? _modalService.Show<T>(title, modalParameters, options)
				: _modalService.Show<T>(title, _emptyModalParameters, options);

			return (TResult)await modal.Result;
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { title, cssClass }))
		{
			throw new UmbrellaBlazorException("There has been a problem showing the dialog.", exc);
		}
	}

	private static IReadOnlyCollection<UmbrellaDialogButton> GetConfirmButtons(UmbrellaDialogButtonType acceptButtonType, IReadOnlyCollection<UmbrellaDialogButton> defaultButtons, string acceptButtonText, string cancelButtonText)
		=> acceptButtonText is DialogDefaults.DefaultConfirmButtonText && cancelButtonText is DialogDefaults.DefaultCancelButtonText
			? defaultButtons
			: new[]
			{
				new UmbrellaDialogButton(cancelButtonText, UmbrellaDialogButtonType.Default, true),
				new UmbrellaDialogButton(acceptButtonText, acceptButtonType)
			};

	/// <inheritdoc/>
	public ValueTask ShowConcurrencyDangerMessageAsync(string message = DialogDefaults.ConcurrencyErrorMessage) => ShowDangerMessageAsync(message);
}