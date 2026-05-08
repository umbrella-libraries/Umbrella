using CommunityToolkit.Diagnostics;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// The default <see cref="IUmbrellaFileAuthorizationHandlerRegistry"/> implementation.
/// </summary>
public class UmbrellaFileAuthorizationHandlerRegistry : IUmbrellaFileAuthorizationHandlerRegistry
{
	private readonly Dictionary<string, IUmbrellaFileAuthorizationHandler> _handlerMappings;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileAuthorizationHandlerRegistry"/> class.
	/// </summary>
	/// <param name="authorizationHandlers">The registered authorization handlers.</param>
	/// <exception cref="UmbrellaFileSystemException">
	/// Multiple <see cref="IUmbrellaFileAuthorizationHandler"/> instances were registered for the same normalized directory name.
	/// </exception>
	public UmbrellaFileAuthorizationHandlerRegistry(IEnumerable<IUmbrellaFileAuthorizationHandler> authorizationHandlers)
	{
		Guard.IsNotNull(authorizationHandlers);

		var handlerMappings = new Dictionary<string, IUmbrellaFileAuthorizationHandler>(StringComparer.Ordinal);

		foreach (IUmbrellaFileAuthorizationHandler authorizationHandler in authorizationHandlers)
		{
			string normalizedDirectoryName = NormalizeDirectoryName(authorizationHandler.DirectoryName);

			if (handlerMappings.ContainsKey(normalizedDirectoryName))
			{
				throw new UmbrellaFileSystemException(
					$"Multiple {nameof(IUmbrellaFileAuthorizationHandler)} instances were registered for the normalized directory name '{normalizedDirectoryName}'. Directory names must be unique.");
			}

			handlerMappings.Add(normalizedDirectoryName, authorizationHandler);
		}

		_handlerMappings = handlerMappings;
	}

	/// <inheritdoc />
	public IUmbrellaFileAuthorizationHandler? GetByDirectoryName(string directoryName)
	{
		Guard.IsNotNullOrWhiteSpace(directoryName);

		string normalizedDirectoryName = NormalizeDirectoryName(directoryName);

		return _handlerMappings.TryGetValue(normalizedDirectoryName, out IUmbrellaFileAuthorizationHandler? authorizationHandler)
			? authorizationHandler
			: null;
	}

	/// <inheritdoc />
	public IUmbrellaFileAuthorizationHandler? GetByFileInfo(IUmbrellaFileInfo fileInfo)
	{
		Guard.IsNotNull(fileInfo);

		string? directoryName = GetTopLevelDirectoryName(fileInfo.SubPath);

		if (string.IsNullOrWhiteSpace(directoryName))
			return null;

		return GetByDirectoryName(directoryName!);
	}

	private static string? GetTopLevelDirectoryName(string subPath)
	{
		if (string.IsNullOrWhiteSpace(subPath))
			return null;

		string normalizedSubPath = subPath.Trim().Replace('\\', '/').Trim('/');

		if (string.IsNullOrWhiteSpace(normalizedSubPath))
			return null;

#if NETSTANDARD2_0
		int firstSeparatorIndex = normalizedSubPath.IndexOf('/');
#else
		int firstSeparatorIndex = normalizedSubPath.IndexOf('/', StringComparison.Ordinal);
#endif

		return firstSeparatorIndex >= 0
			? normalizedSubPath[..firstSeparatorIndex]
			: normalizedSubPath;
	}

	private static string NormalizeDirectoryName(string directoryName)
	{
		string normalizedDirectoryName = directoryName.Trim().Replace('\\', '/').Trim('/');

		if (string.IsNullOrWhiteSpace(normalizedDirectoryName))
			throw new UmbrellaFileSystemException($"The {nameof(IUmbrellaFileAuthorizationHandler.DirectoryName)} value cannot be empty.");

		return normalizedDirectoryName.ToLowerInvariant();
	}
}
