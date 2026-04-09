
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Disk;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses the physical disk as the underlying storage mechanism.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaDiskFileInfo : IUmbrellaFileInfo
{
	#region Private Members
	private readonly string _metadataFullFileName;
	private Dictionary<string, string>? _metadataDictionary;
	#endregion

	#region Protected Properties		
	/// <summary>
	/// Gets the <see cref="ILogger" />.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the provider used to load this file instance.
	/// </summary>
	protected IUmbrellaDiskFileStorageProvider Provider { get; }

	/// <summary>
	/// Gets the file access authorizor.
	/// </summary>
	protected UmbrellaFileAccessAuthorizor AccessAuthorizor { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }
	#endregion

	#region Internal Properties
	internal FileInfo PhysicalFileInfo { get; }
	#endregion

	#region Public Properties
	/// <inheritdoc />
	public bool IsNew { get; private set; }

	/// <inheritdoc />
	public string Name => PhysicalFileInfo.Name;

	/// <inheritdoc />
	public string SubPath { get; }

	/// <inheritdoc />
	public long Length => PhysicalFileInfo.Exists && !IsNew ? PhysicalFileInfo.Length : -1;

	/// <inheritdoc />
	public DateTimeOffset? LastModified => PhysicalFileInfo.Exists && !IsNew ? PhysicalFileInfo.LastWriteTimeUtc : null;

	/// <inheritdoc />
	public string ContentType { get; }
	#endregion

	#region Constructors
	internal UmbrellaDiskFileInfo(
		ILogger<UmbrellaDiskFileInfo> logger,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		string subpath,
		IUmbrellaDiskFileStorageProvider provider,
	  UmbrellaFileAccessAuthorizor accessAuthorizor,
		FileInfo physicalFileInfo,
		bool isNew)
	{
		if (subpath.EndsWith(UmbrellaDiskFileStorageConstants.MetadataFileExtension, StringComparison.OrdinalIgnoreCase))
			throw new UmbrellaFileSystemException($"Files with the extension '{UmbrellaDiskFileStorageConstants.MetadataFileExtension}' are not permitted.");

		Logger = logger;
		Provider = provider;
		AccessAuthorizor = accessAuthorizor;
		GenericTypeConverter = genericTypeConverter;
		PhysicalFileInfo = physicalFileInfo;
		IsNew = isNew;
		SubPath = subpath;

		ContentType = mimeTypeUtility.GetMimeType(Name);

		_metadataFullFileName = PhysicalFileInfo.FullName + UmbrellaDiskFileStorageConstants.MetadataFileExtension;
	}
	#endregion

	#region IUmbrellaFileInfo Members
	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			var destinationFile = (UmbrellaDiskFileInfo)await Provider.CreateAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);

			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There was a problem copying the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaDiskFileInfo>(destinationFile);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Create, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			var target = (UmbrellaDiskFileInfo)destinationFile;

			Guard.IsNotNull(target.PhysicalFileInfo.Directory);

			if (!target.PhysicalFileInfo.Directory.Exists)
				target.PhysicalFileInfo.Directory.Create();

			File.Copy(PhysicalFileInfo.FullName, target.PhysicalFileInfo.FullName, true);

			if (File.Exists(_metadataFullFileName))
				File.Copy(_metadataFullFileName, target.PhysicalFileInfo.FullName + UmbrellaDiskFileStorageConstants.MetadataFileExtension, true);

			target.IsNew = false;

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There was a problem copying the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNullOrWhiteSpace(destinationSubpath);

		try
		{
			IUmbrellaFileInfo destinationFile = await CopyAsync(destinationSubpath, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationSubpath }))
		{
			throw new UmbrellaFileSystemException("There was a problem moving the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(destinationFile);
		Guard.IsOfType<UmbrellaDiskFileInfo>(destinationFile);

		try
		{
			_ = await CopyAsync(destinationFile, cancellationToken).ConfigureAwait(false);
			_ = await DeleteAsync(cancellationToken).ConfigureAwait(false);

			return destinationFile;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { destinationFile }))
		{
			throw new UmbrellaFileSystemException("There was a problem moving the current file to the specified destination.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			PhysicalFileInfo.Delete();
			File.Delete(_metadataFullFileName);

			return true;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There was a problem deleting the current file.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return PhysicalFileInfo.Exists;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There was a problem determining if the current file exists.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileNotFoundException(SubPath);

			byte[] bytes = new byte[PhysicalFileInfo.Length];

			using (var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize, true))
			{
#if NET6_0_OR_GREATER
				_ = await fs.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
				_ = await fs.ReadAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
			}

			return bytes;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNull(target, nameof(target));

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			int bufferSize = bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize;

			using var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true);
			await fs.CopyToAsync(target, bufferSize, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(bytes);
		Guard.HasSizeGreaterThan(bytes, 0);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			using var ms = new MemoryStream(bytes);
			await WriteFromStreamAsync(ms, bufferSizeOverride, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(stream, nameof(stream));

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			Guard.IsNotNull(PhysicalFileInfo.Directory);

			if (!PhysicalFileInfo.Directory.Exists)
				PhysicalFileInfo.Directory.Create();

			int bufferSize = bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize;

			using (var fs = new FileStream(PhysicalFileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.Write, bufferSize, true))
			{
				stream.Position = 0;
				await stream.CopyToAsync(fs, bufferSize, cancellationToken).ConfigureAwait(false);
			}

			IsNew = false;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task<Stream> ReadAsStreamAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			return new FileStream(PhysicalFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSizeOverride ?? UmbrellaFileSystemConstants.SmallBufferSize, true);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException(exc.Message, exc);
		}
	}

	/// <inheritdoc />
	public async Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_metadataDictionary is null)
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

			if (_metadataDictionary is not null && _metadataDictionary.TryGetValue(key, out string? rawValue))
				return GenericTypeConverter.Convert(rawValue, fallback, customValueConverter)!;

			return default!;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, fallback, customValueConverter }))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_metadataDictionary is null)
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

			if (_metadataDictionary is not null)
			{
				if (value is null)
				{
					_ = _metadataDictionary.Remove(key);
				}
				else
				{
					_metadataDictionary[key] = value.ToString() ?? "";
				}

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, value, writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (_metadataDictionary is null)
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

			if (_metadataDictionary is not null)
			{
				_ = _metadataDictionary.Remove(key);

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { key, writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error removing the metadata value for the specified key.", exc);
		}
	}

	/// <inheritdoc />
	public async Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		try
		{
			if (_metadataDictionary is null)
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

			if (_metadataDictionary is not null)
			{
				_metadataDictionary.Clear();

				if (writeChanges)
					await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { writeChanges }))
		{
			throw new UmbrellaFileSystemException("There has been an error clearing the metadata.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (_metadataDictionary?.Count > 0)
			{
				string json = UmbrellaStatics.SerializeJson(_metadataDictionary);

				using var fs = new FileStream(_metadataFullFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, true);
				using var sr = new StreamWriter(fs);

				await sr.WriteAsync(json).ConfigureAwait(false);
			}
			else
			{
				// Before deleting the file, reload the metadata just in case we have some on disk that hasn't be loaded into memory.
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

				if (_metadataDictionary is null || _metadataDictionary.Count is 0)
					File.Delete(_metadataFullFileName);
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error writing the metadata changes.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<TUserId> GetCreatedByIdAsync<TUserId>(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await GetMetadataValueAsync<TUserId>(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the id.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetCreatedByIdAsync<TUserId>(TUserId value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await SetMetadataValueAsync(UmbrellaFileSystemConstants.CreatedByIdMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the id.", exc);
		}
	}

	/// <inheritdoc />
	public async Task<string> GetFileNameAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			return await GetMetadataValueAsync<string>(UmbrellaFileSystemConstants.FileNameMetadataKey, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error getting the file name.", exc);
		}
	}

	/// <inheritdoc />
	public async Task SetFileNameAsync(string value, bool writeChanges = true, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			await SetMetadataValueAsync(UmbrellaFileSystemConstants.FileNameMetadataKey, value, writeChanges, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error setting the file name.", exc);
		}
	}
	#endregion

	#region Private Methods
	private async Task ReloadMetadataAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		try
		{
			string? json = null;

			if (!File.Exists(_metadataFullFileName))
			{
				_metadataDictionary = [];
				return;
			}

			using (var fs = new FileStream(_metadataFullFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
			{
				using var sr = new StreamReader(fs);
#if NET8_0_OR_GREATER
				json = await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
				json = await sr.ReadToEndAsync().ConfigureAwait(false);
#endif
			}

			if (string.IsNullOrWhiteSpace(json))
			{
				_metadataDictionary = [];
				return;
			}

			try
			{
				_metadataDictionary = UmbrellaStatics.DeserializeJson<Dictionary<string, string>>(json);
			}
			catch (Exception exc) when (Logger.WriteError(exc, new { json }, "The JSON value stored in the metadata file could not be deserialized to a Dictionary. This error has been handled silently."))
			{
				_metadataDictionary = [];
			}
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error reloading the metadata for the file.", exc);
		}
	}

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot read the contents of a newly created file. The file must first be written to.");
	}
	#endregion
}