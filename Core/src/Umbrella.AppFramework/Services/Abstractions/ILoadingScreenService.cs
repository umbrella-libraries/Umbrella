
using Umbrella.AppFramework.Services.Enumerations;

namespace Umbrella.AppFramework.Services.Abstractions;

/// <summary>
/// A service used to show and hide an application loading screen.
/// </summary>
public interface ILoadingScreenService
{
	/// <summary>
	/// Occurs when the state of the loading screen has changed.
	/// </summary>
	event Action<LoadingScreenState> OnStateChanged;

	/// <summary>
	/// Shows the loading screen after a delay of <paramref name="delayMilliseconds"/>. If the <see cref="Hide"/> method is
	/// called within the specified delay, the loading screen will not be displayed.
	/// </summary>
	/// <param name="delayMilliseconds">The delay in milliseconds.</param>
	void Show(int delayMilliseconds = 500);

	/// <summary>
	/// Hides the loading screen.
	/// </summary>
	void Hide();
}