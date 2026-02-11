
namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Contains core properties and methods used by implementations of <see cref="IUmbrellaFileStorageProvider"/>.
/// </summary>
public interface IUmbrellaFileStorageProviderOptions
{
	/// <summary>
	/// Gets or sets the name of the temporary files directory.
	/// </summary>
	/// <remarks>Defaults to <see cref="UmbrellaFileSystemConstants.DefaultTempFilesDirectoryName"/>.</remarks>
	string TempFilesDirectoryName { get; }

	/// <summary>
	/// Gets or sets the name of the web files directory.
	/// </summary>
	/// <remarks>Defaults to <see cref="UmbrellaFileSystemConstants.DefaultWebFilesDirectoryName"/>.</remarks>
	string WebFilesDirectoryName { get; set; }
}