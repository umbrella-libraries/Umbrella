
namespace Umbrella.Utilities.Options.Abstractions;

/// <summary>
/// An interface that can be used to enable development mode for Umbrella Options types based on a global setting.
/// </summary>
public interface IDevelopmentModeUmbrellaOptions
{
	/// <summary>
	/// Sets the development mode.
	/// </summary>
	/// <param name="isDevelopmentMode">Specifies if the current application is running in development mode.</param>
	void SetDevelopmentMode(bool isDevelopmentMode);
}