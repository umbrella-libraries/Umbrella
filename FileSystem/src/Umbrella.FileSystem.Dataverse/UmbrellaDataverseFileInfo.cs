using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Globalization;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// An implementation of <see cref="IUmbrellaFileInfo"/> that uses a Microsoft Dataverse table column
/// as the underlying storage mechanism, encoding file content as a base64 string.
/// </summary>
/// <seealso cref="IUmbrellaFileInfo" />
public record UmbrellaDataverseFileInfo : IUmbrellaFileInfo
{
	#region Private Members
	private readonly Guid _recordId;
	private readonly UmbrellaDataverseFileStorageProviderOptions _options;
	private long _length = -1;
	private string? _cachedBase64;
	private Dictionary<string, object?>? _metadataCache;
	private readonly Dictionary<string, object?> _pendingMetadataAttributes = new(StringComparer.OrdinalIgnoreCase);
	#endregion

	#region Protected Properties
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the file access authorizor.
	/// </summary>
	protected UmbrellaFileAccessAuthorizor AccessAuthorizor { get; }

	/// <summary>
	/// Gets the generic type converter.
	/// </summary>
	protected IGenericTypeConverter GenericTypeConverter { get; }
	#endregion

	#region Public Properties
	/// <inheritdoc />
	public bool IsNew { get; private set; }

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string SubPath { get; }

	/// <inheritdoc />
	public long Length => _length;

	/// <inheritdoc />
	public DateTimeOffset? LastModified { get; private set; }

	/// <inheritdoc />
	public string? ContentType { get; set; }
	#endregion

	#region Constructors
	internal UmbrellaDataverseFileInfo(
		ILogger<UmbrellaDataverseFileInfo> logger,
		IGenericTypeConverter genericTypeConverter,
		string subPath,
		string name,
		UmbrellaDataverseFileStorageProviderOptions options,
		UmbrellaFileAccessAuthorizor accessAuthorizor,
		Guid recordId,
		bool isNew)
	{
		Logger = logger;
		GenericTypeConverter = genericTypeConverter;
		SubPath = subPath;
		Name = name;
		_options = options;
		AccessAuthorizor = accessAuthorizor;
		_recordId = recordId;
		IsNew = isNew;
	}
	#endregion

	#region Internal Methods
	internal void Initialize(string? base64Content, DateTimeOffset? lastModified, string? contentType, long? length = null)
	{
		_cachedBase64 = base64Content;
		LastModified = lastModified;
		ContentType = contentType;

		if (_cachedBase64 is not null)
			_length = ComputeByteLength(_cachedBase64);
		else if (length.HasValue)
			_length = length.Value;
	}
	#endregion

	#region IUmbrellaFileInfo Members
	/// <inheritdoc />
	public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (IsNew)
				return false;

			if (_cachedBase64 is not null)
				return true;

			Entity entity = await _options.DataverseClient.RetrieveAsync(
				_options.TableName,
				_recordId,
				new ColumnSet(_options.DataColumnName),
				cancellationToken).ConfigureAwait(false);

			string? base64 = entity.GetAttributeValue<string>(_options.DataColumnName);

			if (base64 is null)
				return false;

			_cachedBase64 = base64;
			_length = ComputeByteLength(base64);

			return true;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem determining if the file exists.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Delete, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			if (_options.DeleteRecordOnFileDelete)
			{
				await _options.DataverseClient.DeleteAsync(_options.TableName, _recordId, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				var entity = new Entity(_options.TableName, _recordId);
				entity[_options.DataColumnName] = null;
				entity[_options.FileNameColumnName] = null;
				await _options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
			}

			_cachedBase64 = null;
			_length = -1;

			return true;
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been a problem deleting the file.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			string base64 = await GetBase64Async(cancellationToken).ConfigureAwait(false);

			return Convert.FromBase64String(base64);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem reading the file to a byte array.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNull(target);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			byte[] bytes = await ReadAsByteArrayAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);
			await target.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing the file to the specified stream.", exc);
		}
	}

	/// <inheritdoc />
	public virtual async Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(bytes);
		Guard.HasSizeGreaterThan(bytes, 0);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			if (!await AccessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			string base64 = Convert.ToBase64String(bytes);

			var entity = new Entity(_options.TableName, _recordId);
			entity[_options.DataColumnName] = base64;
			entity[_options.FileNameColumnName] = Name;

			if (!string.IsNullOrWhiteSpace(_options.MimeTypeColumnName))
				entity[_options.MimeTypeColumnName] = ContentType;

			var request = new UpsertRequest { Target = entity };
			_ = await _options.DataverseClient.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

			_cachedBase64 = base64;
			_length = bytes.LongLength;
			LastModified = DateTimeOffset.UtcNow;
			IsNew = false;
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified byte array.", exc);
		}
	}

	/// <inheritdoc />
	public async Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(stream);

		if (bufferSizeOverride.HasValue)
			Guard.IsGreaterThanOrEqualTo(bufferSizeOverride.Value, 1);

		try
		{
			using var ms = new MemoryStream();

			if (stream.CanSeek)
				stream.Position = 0;

			await stream.CopyToAsync(ms, bufferSizeOverride ?? UmbrellaFileSystemConstants.LargeBufferSize, cancellationToken).ConfigureAwait(false);

			await WriteFromByteArrayAsync(ms.ToArray(), bufferSizeOverride, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem writing to the file from the specified stream.", exc);
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

			byte[] bytes = await ReadAsByteArrayAsync(bufferSizeOverride, cancellationToken).ConfigureAwait(false);

			return new MemoryStream(bytes);
		}
		catch (Exception exc) when (Logger.WriteError(exc, new { bufferSizeOverride }))
		{
			throw new UmbrellaFileSystemException("There has been a problem reading the file as a stream.", exc);
		}
	}

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Copy is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Copy is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Move is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public virtual Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException("Move is not supported by the Dataverse file provider.");

	/// <inheritdoc />
	public async Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ThrowIfIsNew();
		Guard.IsNotNullOrWhiteSpace(key);

		try
		{
			if (!_options.MetadataColumnMappings.TryGetValue(key, out DataverseMetadataColumnMapping? mapping))
				return fallback;

			// Pending writes take precedence over the cache
			if (_pendingMetadataAttributes.TryGetValue(mapping.ColumnName, out object? pendingValue))
				return GenericTypeConverter.Convert(ConvertAttributeToString(pendingValue), fallback, customValueConverter)!;

			if (_metadataCache is null)
				await ReloadMetadataAsync(cancellationToken).ConfigureAwait(false);

			if (_metadataCache is null || !_metadataCache.TryGetValue(mapping.ColumnName, out object? rawValue))
				return fallback;

			return GenericTypeConverter.Convert(ConvertAttributeToString(rawValue), fallback, customValueConverter)!;
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
			if (!_options.MetadataColumnMappings.TryGetValue(key, out DataverseMetadataColumnMapping? mapping))
				return;

			object? dataverseAttribute = ConvertToDataverseAttribute(mapping, value);

			_pendingMetadataAttributes[mapping.ColumnName] = dataverseAttribute;

			_metadataCache ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
			_metadataCache[mapping.ColumnName] = dataverseAttribute;

			if (writeChanges)
				await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
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
			if (!_options.MetadataColumnMappings.TryGetValue(key, out DataverseMetadataColumnMapping? mapping))
				return;

			_pendingMetadataAttributes[mapping.ColumnName] = null;

			if (writeChanges)
				await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
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
			foreach (DataverseMetadataColumnMapping mapping in _options.MetadataColumnMappings.Values)
				_pendingMetadataAttributes[mapping.ColumnName] = null;

			if (writeChanges)
				await WriteMetadataChangesAsync(cancellationToken).ConfigureAwait(false);
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

		if (_pendingMetadataAttributes.Count is 0)
			return;

		try
		{
			if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			var entity = new Entity(_options.TableName, _recordId);

			foreach (var (columnName, attributeValue) in _pendingMetadataAttributes)
				entity[columnName] = attributeValue;

			await _options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);

			_pendingMetadataAttributes.Clear();
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw new UmbrellaFileSystemException("There has been an error writing the metadata changes.", exc);
		}
	}
	#endregion

	#region Private Methods
	private async Task<string> GetBase64Async(CancellationToken cancellationToken)
	{
		if (_cachedBase64 is not null)
			return _cachedBase64;

		if (!await AccessAuthorizor(this, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
			throw new UmbrellaFileAccessDeniedException(SubPath);

		Entity entity = await _options.DataverseClient.RetrieveAsync(
			_options.TableName,
			_recordId,
			new ColumnSet(_options.DataColumnName),
			cancellationToken).ConfigureAwait(false);

		string? base64 = entity.GetAttributeValue<string>(_options.DataColumnName);

		if (string.IsNullOrEmpty(base64))
			throw new UmbrellaFileNotFoundException(SubPath);

		_cachedBase64 = base64;
		_length = ComputeByteLength(base64);

		return base64;
	}

	private async Task ReloadMetadataAsync(CancellationToken cancellationToken)
	{
		if (_options.MetadataColumnMappings.Count is 0)
		{
			_metadataCache = [];
			return;
		}

		string[] columnNames = [.. _options.MetadataColumnMappings.Values.Select(m => m.ColumnName)];

		Entity entity = await _options.DataverseClient.RetrieveAsync(
			_options.TableName,
			_recordId,
			new ColumnSet(columnNames),
			cancellationToken).ConfigureAwait(false);

		_metadataCache = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

		foreach (string columnName in columnNames)
		{
			_metadataCache[columnName] = entity.Contains(columnName) ? entity[columnName] : null;
		}

		// Overlay any already-pending changes so subsequent reads reflect local state
		foreach (var (col, val) in _pendingMetadataAttributes)
			_metadataCache[col] = val;
	}

	private void ThrowIfIsNew()
	{
		if (IsNew)
			throw new InvalidOperationException("Cannot perform this operation on a newly created file. The file must first be written to.");
	}

	private static long ComputeByteLength(string base64)
	{
		int padding = base64.EndsWith("==", StringComparison.Ordinal) ? 2
			: base64.EndsWith("=", StringComparison.Ordinal) ? 1
			: 0;

		return (base64.Length * 3L / 4L) - padding;
	}

	private static string? ConvertAttributeToString(object? value) => value switch
	{
		null => null,
		string s => s,
		bool b => b.ToString(CultureInfo.InvariantCulture),
		int i => i.ToString(CultureInfo.InvariantCulture),
		decimal d => d.ToString(CultureInfo.InvariantCulture),
		DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
		EntityReference er => er.Id.ToString(),
		_ => value.ToString(),
	};

	private static object? ConvertToDataverseAttribute(DataverseMetadataColumnMapping mapping, object? value)
	{
		if (value is null)
			return null;

		return mapping.ColumnType switch
		{
			DataverseMetadataColumnType.Text => value.ToString(),
			DataverseMetadataColumnType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
			DataverseMetadataColumnType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture),
			DataverseMetadataColumnType.Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
			DataverseMetadataColumnType.DateTime => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
			DataverseMetadataColumnType.Lookup or DataverseMetadataColumnType.Owner =>
				new EntityReference(
					mapping.LookupTableName!,
					value is Guid g ? g : Guid.Parse(value.ToString()!)),
			_ => value.ToString(),
		};
	}
	#endregion
}
