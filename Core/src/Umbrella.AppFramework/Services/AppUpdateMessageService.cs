


using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Exceptions;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Messages;

namespace Umbrella.AppFramework.Services;

/// <summary>
/// A service used to show a message to the user when an application update is available and optionally force
/// the user to upgrade to the new version.
/// </summary>
/// <seealso cref="IAppUpdateMessageService"/>
public class AppUpdateMessageService : IAppUpdateMessageService
{
	private readonly ILogger _logger;
	private readonly IDialogService _dialogUtility;
	private readonly IUriNavigatorService _uriNavigator;

	/// <inheritdoc />
	public event Func<bool, string, Task> OnShow
	{
		add
		{
			Guard.IsNotNull(value?.Target);
			WeakReferenceMessenger.Default.TryRegister<AppUpdateStateChangedMessage>(value.Target, (_, args) => _ = value.Invoke(args.Value.updateRequired, args.Value.message));
		}
		remove
		{
			Guard.IsNotNull(value?.Target);
			WeakReferenceMessenger.Default.Unregister<AppUpdateStateChangedMessage>(value.Target);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AppUpdateMessageService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dialogUtility">The dialog utility.</param>
	/// <param name="uriNavigator">The URI navigator.</param>
	public AppUpdateMessageService(
		ILogger<AppUpdateMessageService> logger,
		IDialogService dialogUtility,
		IUriNavigatorService uriNavigator)
	{
		_logger = logger;
		_dialogUtility = dialogUtility;
		_uriNavigator = uriNavigator;
	}

	/// <inheritdoc />
	public async ValueTask ShowAsync(bool updateRequired, string message, string? storeLink)
	{
		try
		{
			_ = WeakReferenceMessenger.Default.Send(new AppUpdateStateChangedMessage((updateRequired, message)));

			string title = updateRequired ? "Update Required" : "Update Available";

			if (!string.IsNullOrEmpty(storeLink))
			{
				if (updateRequired)
				{
					await _dialogUtility.ShowMessageAsync(message, title, "Update").ConfigureAwait(false);
					await _uriNavigator.OpenAsync(storeLink!, true).ConfigureAwait(false);
				}
				else
				{
					bool openLink = await _dialogUtility.ShowConfirmMessageAsync(message, title, "Update").ConfigureAwait(false);

					if (openLink)
						await _uriNavigator.OpenAsync(storeLink!, true).ConfigureAwait(false);
				}
			}
			else
			{
				// No store link is available so we can only show the message.
				await _dialogUtility.ShowMessageAsync(message, title, "Close").ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { updateRequired, message }))
		{
			throw new UmbrellaAppFrameworkException("There has been a problem showing the app update message.", exc);
		}
	}
}