namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Resolves <see cref="IUmbrellaFileAuthorizationHandler"/> instances for stored files.
/// </summary>
public interface IUmbrellaFileAuthorizationHandlerRegistry
{
	/// <summary>
	/// Gets the authorization handler for the specified directory name.
	/// </summary>
	/// <param name="directoryName">The directory name.</param>
	/// <returns>The authorization handler if one is registered; otherwise <see langword="null"/>.</returns>
	IUmbrellaFileAuthorizationHandler? GetByDirectoryName(string directoryName);

	/// <summary>
	/// Gets the authorization handler for the specified file.
	/// </summary>
	/// <param name="fileInfo">The file information.</param>
	/// <returns>The authorization handler if one is registered; otherwise <see langword="null"/>.</returns>
	IUmbrellaFileAuthorizationHandler? GetByFileInfo(IUmbrellaFileInfo fileInfo);
}
