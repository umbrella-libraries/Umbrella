using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Caching.Abstractions;
using Umbrella.Utilities.Security.Extensions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// A file handler for accessing files stored in the temporary files directory.
/// </summary>
/// <seealso cref="UmbrellaFileHandler{Int32}" />
/// <seealso cref="IUmbrellaTempFileHandler" />
public class UmbrellaTempFileHandler : UmbrellaFileHandler<int>, IUmbrellaTempFileHandler
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

	/// <inheritdoc/>
	public override async Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		try
		{
			string fileInfoCreatedById = await fileInfo.GetCreatedByIdAsync<string>(cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(fileInfoCreatedById))
				return true;

			if (ClaimsPrincipal.Current is null)
				return false;

			string currentUserId = ClaimsPrincipal.Current.GetId<string>();

			if (string.IsNullOrEmpty(currentUserId))
				return false;

			return fileInfoCreatedById.Equals(currentUserId, StringComparison.Ordinal);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo.Name, operationType }))
		{
			throw new UmbrellaFileSystemException("There has been a problem determing if the specified file can be accessed based on the current context.", exc);
		}
	}
}