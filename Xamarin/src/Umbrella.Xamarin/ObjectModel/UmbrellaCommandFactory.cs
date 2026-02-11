
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.Utilities.Networking.Abstractions;
using Umbrella.Xamarin.ObjectModel.Abstractions;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Umbrella.Xamarin.ObjectModel;

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
	public Command CreateCommand(Action execute, Func<bool>? canExecute = null) => new(execute, canExecute);

	/// <inheritdoc />
	public Command<T> CreateCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) => new(execute, canExecute);

	/// <inheritdoc />
	public AsyncCommand CreateAsyncCommand(Func<Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, () => CanExecute(canExecute, checkNetworkConnection), allowsMultipleExecutions: false);

	/// <inheritdoc />
	public AsyncCommand<T> CreateAsyncCommand<T>(Func<T, Task> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, () => CanExecute(canExecute, checkNetworkConnection), allowsMultipleExecutions: false);

	/// <inheritdoc />
	public AsyncValueCommand CreateAsyncValueCommand(Func<ValueTask> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, () => CanExecute(canExecute, checkNetworkConnection), allowsMultipleExecutions: false);

	/// <inheritdoc />
	public AsyncValueCommand<T> CreateAsyncValueCommand<T>(Func<T, ValueTask> execute, bool checkNetworkConnection = false, Func<bool>? canExecute = null) => new(execute!, () => CanExecute(canExecute, checkNetworkConnection), allowsMultipleExecutions: false);

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