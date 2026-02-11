


namespace Umbrella.AppFramework.Services.Enumerations;

/// <summary>
/// Represents the current state of the loading screen.
/// </summary>
public enum LoadingScreenState
{
	/// <summary>
	/// Specifies that the loading screen has been requested to be displayed but that it is not yet visible.
	/// </summary>
	Requested,

	/// <summary>
	/// Specifies that the loading screen is current being displayed.
	/// </summary>
	Visible,

	/// <summary>
	/// Specifies that the loading screen is currently hidden.
	/// </summary>
	Hidden
}