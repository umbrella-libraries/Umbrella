using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A file handler for accessing files stored in the temporary files directory.
/// </summary>
/// <seealso cref="UmbrellaFileHandler{Int32}" />
/// <seealso cref="IUmbrellaTempFileHandler" />
public class UmbrellaTempFileHandler : UmbrellaFileHandler, IUmbrellaTempFileHandler
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaTempFileHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="cache">The cache.</param>
	/// <param name="cacheKeyUtility">The cache key utility.</param>
	/// <param name="fileProvider">The file provider.</param>
	/// <param name="options">The options.</param>
	public UmbrellaTempFileHandler(
		ILogger<UmbrellaTempFileHandler> logger,
		IHybridCache cache,
		ICacheKeyUtility cacheKeyUtility,
		IUmbrellaFileStorageProvider fileProvider,
		IUmbrellaFileStorageProviderOptions options)
		: base(logger, cache, cacheKeyUtility, fileProvider, options)
	{
	}

	/// <inheritdoc/>
	public override string DirectoryName => Options.TempFilesDirectoryName;
}
