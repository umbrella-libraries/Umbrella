
using System.Security.Claims;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A handler used to perform authorization checks on files before they are accessed.
/// </summary>
/// <remarks>
/// These handlers must be registered as singletons with the application DI container.
/// </remarks>
public interface IUmbrellaFileAuthorizationHandler
{
	/// <summary>
	/// Gets the name of the directory.
	/// </summary>
	string DirectoryName { get; }

	/// <summary>
	/// Determines whether the specified <paramref name="fileInfo"/> can be accessed in the current context, e.g. by the current <see cref="ClaimsPrincipal"/>.
	/// </summary>
	/// <param name="fileInfo">The file.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the file can be accessed; otherwise <see langword="false"/></returns>
	Task<bool> CanAccessAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default);
}