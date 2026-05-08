using Microsoft.Extensions.Logging;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// Serves as the base class for file authorization handlers.
/// </summary>
public abstract class UmbrellaFileAuthorizationHandler : IUmbrellaFileAuthorizationHandler
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileAuthorizationHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	protected UmbrellaFileAuthorizationHandler(ILogger logger)
	{
		Logger = logger;
	}

	/// <inheritdoc />
	public abstract string DirectoryName { get; }

	/// <inheritdoc />
	public abstract Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default);
}
