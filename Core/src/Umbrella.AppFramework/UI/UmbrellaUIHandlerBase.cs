using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Security.Abstractions;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.Utilities.Http;

namespace Umbrella.AppFramework.UI;

/// <summary>
/// A base class used to provide common functionality for application view models and shared UI handlers.
/// </summary>
/// <remarks>
/// This class underpines multiple application frameworks, e.g. Blazor and Xamarin.
/// Whilst implementing <see cref="INotifyPropertyChanged"/> is not required for Blazor, it is required
/// for Xamarin to be able to detect changes in property values.
/// </remarks>
/// <seealso cref="INotifyPropertyChanged" />
public abstract class UmbrellaUIHandlerBase : INotifyPropertyChanged
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the dialog utility.
	/// </summary>
	protected IDialogService DialogUtility { get; }

	/// <summary>
	/// Gets the authentication helper.
	/// </summary>
	protected IAppAuthHelper AuthHelper { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaUIHandlerBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="dialogUtility">The dialog utility.</param>
	/// <param name="authHelper">The authentication helper.</param>
	protected UmbrellaUIHandlerBase(
		ILogger logger,
		IDialogService dialogUtility,
		IAppAuthHelper authHelper)
	{
		Logger = logger;
		DialogUtility = dialogUtility;
		AuthHelper = authHelper;
	}

	/// <summary>
	/// Gets the <see cref="ClaimsPrincipal"/> for the current user.
	/// </summary>
	/// <returns>The <see cref="ClaimsPrincipal"/>.</returns>
	protected async ValueTask<ClaimsPrincipal> GetClaimsPrincipalAsync() => await AuthHelper.GetCurrentClaimsPrincipalAsync().ConfigureAwait(false);

	/// <summary>
	/// Shows a friendly error message for the specified <paramref name="problemDetails"/> using the <see cref="DialogUtility"/>.
	/// </summary>
	/// <param name="problemDetails">The problem details.</param>
	/// <param name="title">The title.</param>
	protected ValueTask ShowProblemDetailsErrorMessageAsync(HttpProblemDetails? problemDetails, string title = "Error")
		=> DialogUtility.ShowProblemDetailsErrorMessageAsync(problemDetails, title);

	/// <summary>
	/// Sets the property value on the specified <paramref name="backingStore"/>, fires any specified <paramref name="onChanged"/> action and also calls the
	/// <see cref="OnPropertyChanged(string)"/> method (which internally fires the <see cref="PropertyChanged"/> event).
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="backingStore">The backing store.</param>
	/// <param name="value">The value.</param>
	/// <param name="propertyName">Name of the property.</param>
	/// <param name="onChanged">The optional <see cref="Action"/> to be invoked if the <paramref name="value"/> being assigned to the <paramref name="backingStore"/> has changed.</param>
	/// <returns><see langword="true"/> if the property value has changed; otherwise <see langword="false"/>.</returns>
	protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action? onChanged = null)
	{
		if (EqualityComparer<T>.Default.Equals(backingStore, value))
			return false;

		backingStore = value;
		onChanged?.Invoke();
		OnPropertyChanged(propertyName);

		return true;
	}

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>
	/// This should be called when a property has been changed.
	/// </summary>
	/// <param name="propertyName">Name of the property.</param>
	protected void OnPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}