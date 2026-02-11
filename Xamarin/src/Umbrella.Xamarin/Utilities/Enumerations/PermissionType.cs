
namespace Umbrella.Xamarin.Utilities.Enumerations;

/// <summary>
/// Represents a Xamarin permission.
/// </summary>
public enum PermissionType
{
	/// <summary>
	/// Encapuslates the underlying required OS permissions required to access existing photos stored on a device.
	/// </summary>
	SavedPhoto,

	/// <summary>
	/// Encapuslates the underlying required OS permissions required to access existing videos stored on a device.
	/// </summary>
	SavedVideo,

	/// <summary>
	/// Encapuslates the underlying required OS permissions required to take a new photo using the device's camera.
	/// </summary>
	NewPhoto,

	/// <summary>
	/// Encapuslates the underlying required OS permissions required to take a new video using the device's camera.
	/// </summary>
	NewVideo,

	/// <summary>
	/// Encapuslates the underlying required OS permissions required to access existing files stored on a device.
	/// </summary>
	File
}