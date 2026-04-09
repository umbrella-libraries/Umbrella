using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Disk;

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses the physical disk as the underlying storage mechanism.
/// </summary>
/// <seealso cref="UmbrellaDiskFileStorageProvider{UmbrellaDiskFileProviderOptions}" />
public class UmbrellaDiskFileStorageProvider : UmbrellaDiskFileStorageProvider<UmbrellaDiskFileStorageProviderOptions>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDiskFileStorageProvider"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	public UmbrellaDiskFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter)
		: base(loggerFactory, mimeTypeUtility, genericTypeConverter)
	{
	}
}

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses the physical disk as the underlying storage mechanism.
/// </summary>
/// <typeparam name="TOptions">The type of the options.</typeparam>
/// <seealso cref="UmbrellaDiskFileStorageProvider{UmbrellaDiskFileProviderOptions}" />
public class UmbrellaDiskFileStorageProvider<TOptions> : UmbrellaFileStorageProvider<UmbrellaDiskFileInfo, UmbrellaDiskFileStorageProviderOptions>, IUmbrellaDiskFileStorageProvider
	where TOptions : UmbrellaDiskFileStorageProviderOptions
{
	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDiskFileStorageProvider{TOptions}"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	public UmbrellaDiskFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter)
		: base(loggerFactory.CreateLogger<UmbrellaDiskFileStorageProvider>(), loggerFactory, mimeTypeUtility, genericTypeConverter)
	{
	}
	#endregion

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			string physicalPath = CleanPath(subpath);

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { subpath, physicalPath }, "Directory");

			if (Directory.Exists(physicalPath))
			{
				try
				{
					Directory.Delete(physicalPath, true);
				}
				catch
				{
					_ = Logger.WriteWarning(state: new { subpath, physicalPath }, message: "The specified directory to be deleted no longer exists, most likely because of a race condition.");
				}
			}

			return Task.CompletedTask;
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
			string physicalPath = CleanPath(subpath);

			if (Logger.IsEnabled(LogLevel.Debug))
				Logger.WriteDebug(new { subpath, physicalPath }, "Directory");

			var directoryInfo = new DirectoryInfo(physicalPath);

			if (!directoryInfo.Exists)
				return Array.Empty<UmbrellaDiskFileInfo>();

			UmbrellaDiskFileInfo[] files = directoryInfo
				.GetFiles()
				.Where(x => !x.Extension.Equals(UmbrellaDiskFileStorageConstants.MetadataFileExtension, StringComparison.OrdinalIgnoreCase))
				.Select(x => new UmbrellaDiskFileInfo(FileInfoLoggerInstance, MimeTypeUtility, GenericTypeConverter, SanitizeSubPathCore($"{subpath}/{x.Name}"), this, AuthorizeAsync, x, false))
				.ToArray();

			var lstResult = new List<UmbrellaDiskFileInfo>();

			foreach (var file in files)
			{
				if (await AuthorizeAsync(file, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
					lstResult.Add(file);
				else
					_ = Logger.WriteWarning(state: new { file.SubPath }, message: "File access failed.");
			}

			return lstResult;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException("There has been a problem enumerating the files in the specified directory.", exc);
		}
	}

	#region Overridden Methods
	/// <inheritdoc />
	protected override async Task<IUmbrellaFileInfo?> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath, nameof(subpath));

		string physicalPath = CleanPath(subpath);
		string cleanedSubPath = SanitizeSubPathCore(subpath);

		if (Logger.IsEnabled(LogLevel.Debug))
			Logger.WriteDebug(new { subpath, cleanedSubPath, physicalPath }, "File");

		var physicalFileInfo = new FileInfo(physicalPath);

		if (!isNew && !physicalFileInfo.Exists)
			return null;

		var fileInfo = new UmbrellaDiskFileInfo(FileInfoLoggerInstance, MimeTypeUtility, GenericTypeConverter, cleanedSubPath, this, AuthorizeAsync, physicalFileInfo, isNew);

		return !await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false)
			? throw new UmbrellaFileAccessDeniedException(subpath)
			: (IUmbrellaFileInfo)fileInfo;
	}
	#endregion

	#region Private Methods
	private string CleanPath(string subpath)
	{
		// Sanitize subpath
		string coreSubpath = SanitizeSubPathCore(subpath);

		var cleanedPathBuilder = new StringBuilder(coreSubpath);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			_ = cleanedPathBuilder
				.Replace('/', '\\')
				.Insert(0, Options.RootPhysicalPath + @"\");
		}
		else
		{
			_ = cleanedPathBuilder.Insert(0, Options.RootPhysicalPath + "/");
		}

		return cleanedPathBuilder.ToString();
	}
	#endregion
}