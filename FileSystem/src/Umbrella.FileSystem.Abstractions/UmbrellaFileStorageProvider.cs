
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
public abstract partial class UmbrellaFileStorageProvider<TFileInfo, TOptions>
	where TFileInfo : IUmbrellaFileInfo
	where TOptions : UmbrellaFileStorageProviderOptionsBase
{
	#region Private Static Members
	private static readonly char[] _subpathTrimCharacters = [' ', '\\', '/', '~', ' '];
	private static readonly Regex _multipleSlashSelector = CreateMultipleSlashSelector();
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
	/// Gets the authorization handler registry.
	/// </summary>
	protected IUmbrellaFileAuthorizationHandlerRegistry AuthorizationHandlerRegistry { get; }

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
	/// <param name="authorizationHandlerRegistry">The authorization handler registry.</param>
	protected UmbrellaFileStorageProvider(
		ILogger logger,
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		IUmbrellaFileAuthorizationHandlerRegistry authorizationHandlerRegistry)
	{
		Guard.IsNotNull(logger);
		Guard.IsNotNull(loggerFactory);
		Guard.IsNotNull(mimeTypeUtility);
		Guard.IsNotNull(genericTypeConverter);
		Guard.IsNotNull(authorizationHandlerRegistry);

		Logger = logger;
		LoggerFactory = loggerFactory;
		MimeTypeUtility = mimeTypeUtility;
		GenericTypeConverter = genericTypeConverter;
		AuthorizationHandlerRegistry = authorizationHandlerRegistry;
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

			if (fileInfo is null)
				throw new UmbrellaFileNotFoundException(subpath);

			return !await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Create, cancellationToken).ConfigureAwait(false)
				? throw new UmbrellaFileAccessDeniedException(subpath)
				: fileInfo;
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceSubpath, destinationSubpath }) && exc is not UmbrellaFileNotFoundException)
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationSubpath }) && exc is not UmbrellaFileNotFoundException)
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationFile }) && exc is not UmbrellaFileNotFoundException)
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceSubpath, destinationSubpath }) && exc is not UmbrellaFileNotFoundException)
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationSubpath }) && exc is not UmbrellaFileNotFoundException)
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
		catch (Exception exc) when (Logger.WriteError(exc, new { sourceFile, destinationFile }) && exc is not UmbrellaFileNotFoundException)
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

	/// <summary>
	/// Finalizes a resolved file by applying the standard read authorization check for existing files while allowing new
	/// files to flow back to the caller for create authorization.
	/// </summary>
	/// <remarks>
	/// Provider-specific <c>GetFileAsync(..., isNew: true)</c> implementations should return a new file instance without
	/// applying a read authorization check. This helper preserves that behavior in one place so <see cref="CreateAsync(string, CancellationToken)"/>
	/// can subsequently authorize with <see cref="UmbrellaFileOperationType.Create"/>.
	/// </remarks>
	/// <typeparam name="TResolvedFileInfo">The resolved file info type.</typeparam>
	/// <param name="fileInfo">The resolved file information.</param>
	/// <param name="subpath">The original requested subpath.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The resolved file information when access is permitted.</returns>
	protected async Task<TResolvedFileInfo> FinalizeResolvedFileAsync<TResolvedFileInfo>(TResolvedFileInfo fileInfo, string subpath, CancellationToken cancellationToken)
		where TResolvedFileInfo : IUmbrellaFileInfo
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);
		Guard.IsNotNullOrWhiteSpace(subpath);

		// New files must skip the provider-level read check here so CreateAsync/Write* can authorize them as Create instead.
		if (fileInfo.IsNew)
			return fileInfo;

		return !await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false)
			? throw new UmbrellaFileAccessDeniedException(subpath)
			: fileInfo;
	}

	/// <summary>
	/// Performs an access check on the file to ensure it can be accessed in the current context.
	/// </summary>
	/// <param name="fileInfo">The file information.</param>
	/// <param name="policy">The file access policy.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// An awaitable <see cref="Task"/> that returns <see langword="true" /> if the file passes the check; otherwise
	/// <see langword="false" />.
	/// </returns>
	protected virtual async Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType policy, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(fileInfo);

		IUmbrellaFileAuthorizationHandler? authorizationHandler = AuthorizationHandlerRegistry.GetByFileInfo(fileInfo);

		return authorizationHandler is not null
			? await authorizationHandler.AuthorizeAsync(fileInfo, policy, cancellationToken).ConfigureAwait(false)
			: Options.AllowUnhandledFileAuthorizationChecks;
	}
	#endregion

	#region Abstract Methods		
	/// <summary>
	/// Gets the file at the specified <paramref name="subpath"/>.
	/// </summary>
	/// <param name="subpath">The subpath.</param>
	/// <param name="isNew">
	/// Specifies if the caller is resolving a new file instance for creation. When <see langword="true"/>, implementations
	/// should resolve the file information without applying a read authorization check.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable Task that returns the file.</returns>
	protected abstract Task<IUmbrellaFileInfo?> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken);
	#endregion

#if NET7_0_OR_GREATER
	[GeneratedRegex("/+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex CreateMultipleSlashSelector();
#else
	private static Regex CreateMultipleSlashSelector() => new("/+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif
}

/// <summary>
/// Defines the signature of a delegate that performs an access check on a file to ensure it can be accessed in the
/// current context.
/// </summary>
/// <param name="fileInfo">The file information.</param>
/// <param name="policy">The file access policy.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>An awaitable Task that returns <see langword="true" /> if the file passes the check; otherwise <see langword="false" />.</returns>
public delegate Task<bool> UmbrellaFileAccessAuthorizor(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType policy, CancellationToken cancellationToken);
