


using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.Maui.ObjectModel.Abstractions;
using Umbrella.Utilities.Networking.Abstractions;

namespace Umbrella.Maui.ObjectModel;

/// <summary>
/// A factory used to create instances of <see cref="ICommand"/> for use with views and view models in Xamarin.
/// </summary>
/// <seealso cref="IUmbrellaCommandFactory"/>
public class UmbrellaCommandFactory : IUmbrellaCommandFactory
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaCommandFactory"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dialogUtility">The dialog utility.</param>
	/// <param name="networkConnectionStatusUtility">The network connection status utility.</param>
	public UmbrellaCommandFactory(
		ILogger<UmbrellaCommandFactory> logger,
		IDialogService dialogUtility,
		INetworkConnectionStatusUtility networkConnectionStatusUtility)
	{
		Logger = logger;
		DialogUtility = dialogUtility;
		NetworkConnectionStatusUtility = networkConnectionStatusUtility;
	}

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger<UmbrellaCommandFactory> Logger { get; }

	/// <summary>
	/// Gets the dialog utility.
	/// </summary>
	protected IDialogService DialogUtility { get; }

	/// <summary>
	/// Gets the network connection status utility.
	/// </summary>
	protected INetworkConnectionStatusUtility NetworkConnectionStatusUtility { get; }

	/// <inheritdoc />
	public RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null) => canExecute is null
		? new(execute)
		: new(execute, canExecute);

	/// <inheritdoc />
	public RelayCommand<T> CreateCommand<T>(Action<T?> execute, Predicate<T?>? canExecute = null) => canExecute is null
		? new(execute)
		: new(execute, canExecute);

	/// <inheritdoc />
	public AsyncRelayCommand CreateAsyncCommand(Func<Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, () => CanExecute(canExecute, checkNetworkConnection));

	/// <inheritdoc />
	public AsyncRelayCommand<T> CreateAsyncCommand<T>(Func<T, Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, x => CanExecute(canExecute, checkNetworkConnection));

	private bool CanExecute(Func<bool>? innerCanExecute, bool checkNetworkConnection)
	{
		if (checkNetworkConnection && !NetworkConnectionStatusUtility.IsConnected)
		{
			// TODO: Create an options class to allow this message to be configurable.
			_ = MainThread.InvokeOnMainThreadAsync(() => DialogUtility.ShowDangerMessageAsync("Your device is not connected to the internet. Please check your connection and try again."));

			return false;
		}

		return innerCanExecute is null || innerCanExecute();
	}
}