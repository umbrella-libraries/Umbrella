


namespace Umbrella.AppFramework.Services.Constants;

/// <summary>
/// Constants that define default values used by dialogs.
/// </summary>
public static class DialogDefaults
{
	/// <summary>
	/// The default confirm button text
	/// </summary>
	public const string DefaultConfirmButtonText = "Confirm";

	/// <summary>
	/// The default close button text
	/// </summary>
	public const string DefaultCloseButtonText = "Close";

	/// <summary>
	/// The default cancel button text
	/// </summary>
	public const string DefaultCancelButtonText = "Cancel";

	/// <summary>
	/// The default unknown error message
	/// </summary>
	public const string UnknownErrorMessage = "An unknown error has occurred. Please try again.";

	/// <summary>
	/// The default intro message for validation errors.
	/// </summary>
	public const string ValidationResultsIntroMessage = "Please correct all validation errors.";

	/// <summary>
	/// The default concurrency error message
	/// </summary>
	public const string ConcurrencyErrorMessage = "The data on this page has changed since it was loaded. Please refresh the page to continue.";
}