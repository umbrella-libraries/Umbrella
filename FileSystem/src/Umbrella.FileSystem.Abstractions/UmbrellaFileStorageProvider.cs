
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Extensions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Abstractions;

/// <summary>
/// The abstract base class upon which file provider implementations are built.
/// </summary>
/// <typeparam name="TFileInfo">The type of the file information.</typeparam>
/// <typeparam name="TOptions">The type of the options.</typeparam>
public abstract class UmbrellaFileStorageProvider<TFileInfo, TOptions>
	where TFileInfo : IUmbrellaFileInfo
	where TOptions : UmbrellaFileStorageProviderOptionsBase
{
	#region Private Static Members
	private static readonly char[] _subpathTrimCharacters = [' ', '\\', '/', '~', ' '];
	private static readonly Regex _multipleSlashSelector = new("/+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the log.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the logger factory.
	/// </summary>
	protected ILoggerFactory LoggerFactory { get; }

	/// <summary>
	/// Gets the MIME type utility.
	/// </summary>
	protected IMimeTypeUtility MimeTypeUtility { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }

	/// <summary>
	/// Gets the file information logger instance.
	/// </summary>
	protected ILogger<TFileInfo> FileInfoLoggerInstance { get; }

	/// <summary>
	/// Gets the options.
	/// </summary>
	protected TOptions Options { get; private set; } = null!;
	#endregion

	#region Constructors		
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	protected UmbrellaFileStorageProvider(
		ILogger logger,
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter)
	{
		Logger = logger;
		LoggerFactory = loggerFactory;
		MimeTypeUtility = mimeTypeUtility;
		GenericTypeConverter = genericTypeConverter;
		FileInfoLoggerInstance = LoggerFactory.CreateLogger<TFileInfo>();
	}
	#endregion

	#region Public Methods

	/// <inheritdoc />
	public virtual void InitializeOptions(UmbrellaFileStorageProviderOptionsBase options)
	{
		if (Options is not null)
			throw new UmbrellaFileSystemException("The options have already been initialized for this instance.");

		Options = (TOptions)options;
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CreateAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			IUmbrellaFileInfo? fileInfo = await GetFileAsync(subpath, true, cancellationToken).ConfigureAwait(false);

			return fileInfo is null ? throw new UmbrellaFileNotFoundException(subpath) : fileInfo;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo?> GetAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			return await GetFileAsync(subpath, false, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> DeleteAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			IUmbrellaFileInfo? fileInfo = await GetAsync(subpath, cancellationToken).ConfigureAwait(false);

			return fileInfo is null || await fileInfo.DeleteAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> DeleteAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		try
		{
			return await fileInfo.DeleteAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { fileInfo }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CopyAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(sourceSubpath);
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo? sourceFile = await GetAsync(sourceSubpath, cancellationToken).ConfigureAwait(false);

			return sourceFile is null
				? throw new UmbrellaFileNotFoundException(sourceSubpath)
				: await sourceFile.CopyAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceSubpath, destinationSubpath }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFile);
		Guard.IsOfType<TFileInfo>(sourceFile);
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo? destinationFile = await CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

			return destinationFile is null
				? throw new UmbrellaFileNotFoundException(destinationSubpath)
				: await sourceFile.CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationSubpath }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFile);
		Guard.IsOfType<TFileInfo>(sourceFile);
		Guard.IsOfType<TFileInfo>(destinationFile);

		try
		{
			return await sourceFile.CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationFile }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> MoveAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(sourceSubpath);
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo? sourceFile = await GetAsync(sourceSubpath, cancellationToken).ConfigureAwait(false);

			return sourceFile is null
				? throw new UmbrellaFileNotFoundException(sourceSubpath)
				: await sourceFile.MoveAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceSubpath, destinationSubpath }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFile);
		Guard.IsOfType<TFileInfo>(sourceFile);
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo? destinationFile = await CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

			return destinationFile is null
				? throw new UmbrellaFileNotFoundException(destinationSubpath)
				: await sourceFile.MoveAsync(destinationFile, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationSubpath }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(sourceFile);
		Guard.IsOfType<TFileInfo>(sourceFile);
		Guard.IsOfType<TFileInfo>(destinationFile);

		try
		{
			return await sourceFile.MoveAsync(destinationFile, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationFile }) && (exc is UmbrellaFileNotFoundException) == false)
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> SaveAsync(string subpath, byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);
		Guard.HasSizeGreaterThan(bytes, 0);

		try
		{
			IUmbrellaFileInfo? file = await CreateAsync(subpath, cancellationToken).ConfigureAwait(false) ?? throw new UmbrellaFileNotFoundException(subpath);
			await file.WriteFromByteArrayAsync(bytes, bufferSizeOverride, cancellationToken).ConfigureAwait(false);

			return file;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath, bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<IUmbrellaFileInfo> SaveAsync(string subpath, Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);
		Guard.IsNotNull(stream);

		try
		{
			IUmbrellaFileInfo? file = await CreateAsync(subpath, cancellationToken).ConfigureAwait(false) ?? throw new UmbrellaFileNotFoundException(subpath);
			await file.WriteFromStreamAsync(stream, bufferSizeOverride, cancellationToken).ConfigureAwait(false);

			return file;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath, bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> ExistsAsync(string subpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(subpath);

		try
		{
			IUmbrellaFileInfo? file = await GetFileAsync(subpath, false, cancellationToken).ConfigureAwait(false);

			return file is not null;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { subpath }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}
	#endregion

	#region Protected Methods		
	/// <summary>
	/// Performs core sanitization of the subpath.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <returns>The sanitized subpath.</returns>
	protected string SanitizeSubPathCore(string subpath)
	{
		StringBuilder pathBuilder = new StringBuilder(subpath)
			.Trim(_subpathTrimCharacters)
			.Replace('\\', '/');

		// Force all files to be stored and read in lowercase to avoid issues with Blob Storage
		// and Linux which both use case-sensitive file systems.
		string cleanedName = _multipleSlashSelector.Replace(pathBuilder.ToString(), "/").ToLowerInvariant();

		if (!cleanedName.StartsWith("/", StringComparison.Ordinal))
			cleanedName = "/" + cleanedName;

		return cleanedName;
	}
	#endregion

	#region Abstract Methods		
	/// <summary>
	/// Gets the file at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="isNew">Specifies if the file is new.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable Task that returns the file.</returns>
	protected abstract Task<IUmbrellaFileInfo?> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken);
	#endregion
}