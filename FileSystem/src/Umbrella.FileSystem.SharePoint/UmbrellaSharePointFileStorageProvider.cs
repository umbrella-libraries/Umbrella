using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.SharePoint;

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses SharePoint via Microsoft Graph as the underlying storage mechanism.
/// </summary>
/// <seealso cref="UmbrellaSharePointFileStorageProvider{UmbrellaSharePointFileStorageProviderOptions}" />
public class UmbrellaSharePointFileStorageProvider : UmbrellaSharePointFileStorageProvider<UmbrellaSharePointFileStorageProviderOptions>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaSharePointFileStorageProvider"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	public UmbrellaSharePointFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter)
		: base(loggerFactory, mimeTypeUtility, genericTypeConverter)
	{
	}
}

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses SharePoint via Microsoft Graph as the underlying storage mechanism.
/// </summary>
/// <typeparam name="TOptions">The type of the provider options.</typeparam>
public class UmbrellaSharePointFileStorageProvider<TOptions> : UmbrellaFileStorageProvider<UmbrellaSharePointFileInfo, UmbrellaSharePointFileStorageProviderOptions>, IUmbrellaSharePointFileStorageProvider, IDisposable
	where TOptions : UmbrellaSharePointFileStorageProviderOptions
{
	#region Private Members
	private readonly SemaphoreSlim _driveIdLock = new(1, 1);
	private string? _driveId;
	#endregion

	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaSharePointFileStorageProvider{TOptions}"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	public UmbrellaSharePointFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter)
		: base(loggerFactory.CreateLogger<UmbrellaSharePointFileStorageProvider>(), loggerFactory, mimeTypeUtility, genericTypeConverter)
	{
	}
	#endregion

	#region IUmbrellaFileStorageProvider Members
	/// <inheritdoc />
	public async Task DeleteDirectoryAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			string logicalPath = SanitizeSubPathCore(subpath);
			string spFolderPath = GetSharePointPath(logicalPath);
			string driveId = await GetDriveIdAsync(cancellationToken).ConfigureAwait(false);

			await Options.GraphServiceClient.Drives[driveId].Root
				.ItemWithPath(spFolderPath)
				.DeleteAsync(cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (ODataError odataError) when (odataError.ResponseStatusCode == 404)
		{
			// Directory doesn't exist — nothing to do.
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the specified directory.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyCollection<IUmbrellaFileInfo>> EnumerateDirectoryAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			string logicalPath = SanitizeSubPathCore(subpath);
			string spFolderPath = GetSharePointPath(logicalPath);
			string driveId = await GetDriveIdAsync(cancellationToken).ConfigureAwait(false);

			DriveItemCollectionResponse? children = await Options.GraphServiceClient.Drives[driveId].Root
				.ItemWithPath(spFolderPath)
				.Children
				.GetAsync(config =>
				{
					config.QueryParameters.Select = ["id", "name", "size", "lastModifiedDateTime", "file"];
				}, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (children?.Value is null)
				return Array.Empty<IUmbrellaFileInfo>();

			var lstResult = new List<UmbrellaSharePointFileInfo>();

			foreach (DriveItem item in children.Value)
			{
				if (item.File is null || string.IsNullOrWhiteSpace(item.Name))
					continue;

				string spItemPath = spFolderPath + "/" + item.Name;
				string logicalItemPath = GetLogicalPath(spItemPath);

				var fileInfo = new UmbrellaSharePointFileInfo(
					FileInfoLoggerInstance,
					MimeTypeUtility,
					GenericTypeConverter,
					logicalItemPath,
					spItemPath,
					this,
					AuthorizeAsync,
					Options.GraphServiceClient,
					driveId,
					false);

				await fileInfo.InitializeAsync(cancellationToken, item).ConfigureAwait(false);

				if (await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
					lstResult.Add(fileInfo);
				else
					_ = Logger.WriteWarning(state: new { fileInfo.SubPath }, message: "File access denied.");
			}

			return lstResult;
		}
		catch (ODataError odataError) when (odataError.ResponseStatusCode == 404)
		{
			return Array.Empty<IUmbrellaFileInfo>();
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem enumerating the files in the specified directory.", exc);
		}
	}
	#endregion

	#region Overridden Methods
	/// <inheritdoc />
	protected override async Task<IUmbrellaFileInfo?> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		string logicalPath = SanitizeSubPathCore(subpath);
		string driveId = await GetDriveIdAsync(cancellationToken).ConfigureAwait(false);
		string spPath = GetSharePointPath(logicalPath);

		DriveItem? preLoadedItem = null;

		if (!isNew)
		{
			try
			{
				preLoadedItem = await Options.GraphServiceClient.Drives[driveId].Root
					.ItemWithPath(spPath)
					.GetAsync(config =>
					{
						config.QueryParameters.Select = ["id", "size", "lastModifiedDateTime", "file"];
					}, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
			}
			catch (ODataError odataError) when (odataError.ResponseStatusCode == 404)
			{
				return null;
			}
		}

		var fileInfo = new UmbrellaSharePointFileInfo(
			FileInfoLoggerInstance,
			MimeTypeUtility,
			GenericTypeConverter,
			logicalPath,
			spPath,
			this,
			AuthorizeAsync,
			Options.GraphServiceClient,
			driveId,
			isNew);

		await fileInfo.InitializeAsync(cancellationToken, preLoadedItem).ConfigureAwait(false);

		return !await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false)
			? throw new UmbrellaFileAccessDeniedException(subpath)
			: (IUmbrellaFileInfo)fileInfo;
	}
	#endregion

	#region Private Methods
	private string GetSharePointPath(string logicalSubPath)
		=> Options.SubPathTranslator?.Invoke(logicalSubPath) ?? logicalSubPath.TrimStart('/');

	private string GetLogicalPath(string spRelPath)
		=> Options.SubPathReverseTranslator?.Invoke(spRelPath) ?? "/" + spRelPath;

	private Task<string> GetDriveIdAsync(CancellationToken cancellationToken)
		=> _driveId is not null ? Task.FromResult(_driveId) : AcquireDriveIdAsync(cancellationToken);

	private async Task<string> AcquireDriveIdAsync(CancellationToken cancellationToken)
	{
		await _driveIdLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return _driveId ??= await ResolveDriveIdAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_ = _driveIdLock.Release();
		}
	}

	private async Task<string> ResolveDriveIdAsync(CancellationToken cancellationToken)
	{
		DriveCollectionResponse? drives = await Options.GraphServiceClient.Sites[Options.SiteId].Drives
			.GetAsync(config =>
			{
				config.QueryParameters.Select = ["id", "name"];
			}, cancellationToken: cancellationToken)
			.ConfigureAwait(false);

		Drive? drive = drives?.Value?.FirstOrDefault(x => x is not null && string.Equals(x.Name, Options.DriveName, StringComparison.OrdinalIgnoreCase));

		return !string.IsNullOrWhiteSpace(drive?.Id)
			? drive!.Id
			: throw new InvalidOperationException($"The SharePoint document library '{Options.DriveName}' could not be found for site '{Options.SiteId}'.");
	}
	#endregion

	#region IDisposable Support
	private bool _isDisposed;

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			if (disposing)
				_driveIdLock.Dispose();

			_isDisposed = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
