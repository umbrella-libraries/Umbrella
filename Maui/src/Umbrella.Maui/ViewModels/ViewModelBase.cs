
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.UI;
using Umbrella.Maui.ObjectModel.Abstractions;
using Umbrella.Maui.Utilities.Abstractions;

namespace Umbrella.Maui.ViewModels;

/// <summary>
/// A base view model which all Xamarin view models should extend.
/// </summary>
/// <seealso cref="UmbrellaUIHandlerBase" />
public abstract class ViewModelBase : UmbrellaUIHandlerBase
{
	private bool _isBusy;
	private Page? _currentPage;
	private bool _isRefreshing;
	private LayoutState _currentState = LayoutState.Loading;

	/// <summary>
	/// Gets or sets a value indicating whether this instance is refreshing.
	/// </summary>
	/// <remarks>
	/// This is usually bound to a control, e.g. a <see cref="RefreshView"/>, which means its value is set by the control.
	/// However, it needs to be manually set to <see langword="false"/> when the refresh operation has been completed
	/// in order for the UI to change state.
	/// </remarks>
	public bool IsRefreshing
	{
		get => _isRefreshing;
		set => SetProperty(ref _isRefreshing, value);
	}

	/// <summary>
	/// Gets or sets the current layout state. The initial state is <see cref="LayoutState.Loading"/>.
	/// </summary>
	public LayoutState CurrentState
	{
		get => _currentState;
		set => SetProperty(ref _currentState, value);
	}

	/// <summary>
	/// Gets or sets the current page.
	/// </summary>
	/// <remarks>This is required for ViewModels which need access to the current Xamarin page, e.g. it needs to be passed to the <see cref="IMauiValidationUtility"/>.</remarks>
	/// <exception cref="Exception">The CurrentPage property has not been set.</exception>
	public Page? CurrentPage
	{
		get => _currentPage ?? throw new InvalidOperationException("The CurrentPage property has not been set.");
		set => _currentPage = value;
	}

	/// <summary>
	/// Gets or sets a value indicating whether this instance is busy.
	/// </summary>
	/// <remarks>This is useful when implementing mutex code blocks, e.g. when loading items from a remote API and wanting to ensure duplicate requests don't take place.</remarks>
	public bool IsBusy
	{
		get => _isBusy;
		set => SetProperty(ref _isBusy, value);
	}

	/// <summary>
	/// Gets the reload button command.
	/// </summary>
	public AsyncRelayCommand ReloadButtonCommand { get; }

	/// <summary>
	/// Gets the OpenUrlInternal command.
	/// </summary>
	public AsyncRelayCommand<string?> OpenUrlInternalCommand { get; }

	/// <summary>
	/// Gets the OpenUrlExternal command.
	/// </summary>
	public AsyncRelayCommand<string?> OpenUrlExternalCommand { get; }

	/// <summary>
	/// Gets the navigate back button command.
	/// </summary>
	public AsyncRelayCommand NavigateBackButtonCommand { get; }

	/// <summary>
	/// Gets the navigate to application path button command.
	/// </summary>
	public AsyncRelayCommand<string?> NavigateToAppPathButtonCommand { get; }

	/// <summary>
	/// Gets the command factory.
	/// </summary>
	protected IUmbrellaCommandFactory CommandFactory { get; }

	/// <summary>
	/// Gets a value which specifies whether or not a check should be made for an active network connection when the <see cref="ReloadButtonCommand" /> is invoked.
	/// </summary>
	/// <remarks>Defaults to <see langword="true"/>.</remarks>
	protected virtual bool CheckNetworkConnectionOnReload { get; } = true;

	/// <summary>
	/// Initializes a new instance of the <see cref="ViewModelBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="authHelper">The authentication helper.</param>
	/// <param name="commandFactory">The command factory.</param>
	/// <param name="dialogUtility">The dialog utility.</param>
	protected ViewModelBase(
		ILogger logger,
		IAppAuthHelper authHelper,
		IDialogService dialogUtility,
		IUmbrellaCommandFactory commandFactory)
		: base(logger, dialogUtility, authHelper)
	{
		Guard.IsNotNull(commandFactory);

		CommandFactory = commandFactory;

		NavigateBackButtonCommand = commandFactory.CreateAsyncCommand(NavigateBackAsync);
		NavigateToAppPathButtonCommand = commandFactory.CreateAsyncCommand<string?>(NavigateToAppPathAsync);
		ReloadButtonCommand = commandFactory.CreateAsyncCommand(OnReloadButtonClickedAsync, CheckNetworkConnectionOnReload);
		OpenUrlInternalCommand = commandFactory.CreateAsyncCommand<string?>(x => OpenUrlAsync(x, true), true);
		OpenUrlExternalCommand = commandFactory.CreateAsyncCommand<string?>(x => OpenUrlAsync(x, false), true);
	}

	/// <summary>
	/// Sets the <see cref="IsBusy"/> and <see cref="IsRefreshing"/> flags to <see langword="false" />.
	/// </summary>
	protected void ClearFlags() => IsBusy = IsRefreshing = false;

	/// <summary>
	/// Called when the <see cref="ReloadButtonCommand"/> is invoked. Unless overridden, this method
	/// just returns <see cref="Task.CompletedTask"/>.
	/// </summary>
	/// <returns>An awaitable Task that completes when the operation completes.</returns>
	protected virtual Task OnReloadButtonClickedAsync() => Task.CompletedTask;

	/// <summary>
	/// Opens the specified <paramref name="url"/>.
	/// </summary>
	/// <param name="url">The URL to open.</param>
	/// <param name="openInsideApp">
	/// If <see langword="true"/>, opens the specified <paramref name="url"/> inside the app; otherwise
	/// the <paramref name="url"/> will open in the default web browser.
	/// </param>
	/// <returns>An awaitable Task that completes when the operation completes.</returns>
	protected async Task OpenUrlAsync(string? url, bool openInsideApp)
	{
		Guard.IsNotNullOrWhiteSpace(url);

		try
		{
			_ = await Browser.OpenAsync(url, openInsideApp ? BrowserLaunchMode.SystemPreferred : BrowserLaunchMode.External);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { url, openInsideApp }))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <summary>
	/// Performs navigation from the current page to the specified path.
	/// </summary>
	/// <param name="path">The application path to navigate to.</param>
	/// <returns>An awaitable Task that completes when the operation completes.</returns>
	protected async Task NavigateToAppPathAsync(string? path)
	{
		Guard.IsNotNullOrWhiteSpace(path);

		try
		{
			await Shell.Current.GoToAsync(path);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { path }))
		{
			await DialogUtility.ShowDangerMessageAsync();
		}
	}

	/// <summary>
	/// Performs navigation from the current page to the previous page in the navigation stack.
	/// </summary>
	/// <returns>An awaitable Task that completes when the operation completes.</returns>
	protected Task NavigateBackAsync() => NavigateToAppPathAsync("..");
}