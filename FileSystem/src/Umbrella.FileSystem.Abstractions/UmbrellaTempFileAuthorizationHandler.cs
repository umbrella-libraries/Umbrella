using System.Security.Claims;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Security.Extensions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// The built-in authorization handler for files stored in the temporary files directory.
/// </summary>
public class UmbrellaTempFileAuthorizationHandler : UmbrellaFileAuthorizationHandler
{
	/// <summary>
	/// Gets the file storage provider options.
	/// </summary>
	protected IUmbrellaFileStorageProviderOptions Options { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaTempFileAuthorizationHandler"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The options.</param>
	public UmbrellaTempFileAuthorizationHandler(
		ILogger<UmbrellaTempFileAuthorizationHandler> logger,
		IUmbrellaFileStorageProviderOptions options)
		: base(logger)
	{
		Options = options;
	}

	/// <inheritdoc />
	public override string DirectoryName => Options.TempFilesDirectoryName;

	/// <inheritdoc />
	public override async Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		try
		{
			if (operationType is UmbrellaFileOperationType.Create && fileInfo.IsNew)
				return true;

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
