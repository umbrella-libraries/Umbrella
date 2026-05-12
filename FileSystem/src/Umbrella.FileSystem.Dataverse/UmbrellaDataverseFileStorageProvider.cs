using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses a
/// Microsoft Dataverse table column as the underlying storage mechanism, encoding file content as base64.
/// </summary>
/// <seealso cref="UmbrellaDataverseFileStorageProvider{UmbrellaDataverseFileStorageProviderOptions}" />
public class UmbrellaDataverseFileStorageProvider : UmbrellaDataverseFileStorageProvider<UmbrellaDataverseFileStorageProviderOptions>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataverseFileStorageProvider"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	/// <param name="authorizationHandlerRegistry">The authorization handler registry.</param>
	public UmbrellaDataverseFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		IUmbrellaFileAuthorizationHandlerRegistry authorizationHandlerRegistry)
		: base(loggerFactory, mimeTypeUtility, genericTypeConverter, authorizationHandlerRegistry)
	{
	}
}

/// <summary>
/// An implementation of <see cref="UmbrellaFileStorageProvider{TFileInfo, TOptions}"/> which uses a
/// Microsoft Dataverse table column as the underlying storage mechanism, encoding file content as base64.
/// </summary>
/// <typeparam name="TOptions">The type of the provider options.</typeparam>
public class UmbrellaDataverseFileStorageProvider<TOptions> : UmbrellaFileStorageProvider<UmbrellaDataverseFileInfo, TOptions>, IUmbrellaDataverseFileStorageProvider
	where TOptions : UmbrellaDataverseFileStorageProviderOptions
{
	#region Constructors
	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaDataverseFileStorageProvider{TOptions}"/> class.
	/// </summary>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="mimeTypeUtility">The MIME type utility.</param>
	/// <param name="genericTypeConverter">The generic type converter.</param>
	/// <param name="authorizationHandlerRegistry">The authorization handler registry.</param>
	public UmbrellaDataverseFileStorageProvider(
		ILoggerFactory loggerFactory,
		IMimeTypeUtility mimeTypeUtility,
		IGenericTypeConverter genericTypeConverter,
		IUmbrellaFileAuthorizationHandlerRegistry authorizationHandlerRegistry)
		: base(loggerFactory.CreateLogger<UmbrellaDataverseFileStorageProvider>(), loggerFactory, mimeTypeUtility, genericTypeConverter, authorizationHandlerRegistry)
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
			string[] parts = logicalPath.TrimStart('/').Split('/');

			if (parts.Length >= 2)
			{
				// /tableName/recordId — clear or delete the single record's file
				if (!Guid.TryParse(parts[1], out Guid recordId))
					throw new ArgumentException($"The record ID segment '{parts[1]}' in subpath '{subpath}' is not a valid GUID.");

				if (Options.DeleteRecordOnFileDelete)
				{
					await Options.DataverseClient.DeleteAsync(Options.TableName, recordId, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					var entity = new Entity(Options.TableName, recordId);
					entity[Options.DataColumnName] = null;
					entity[Options.FileNameColumnName] = null;
					await Options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
				}
			}
			else
			{
				// /tableName — delete all records that have file content
				var query = new QueryExpression(Options.TableName)
				{
					ColumnSet = new ColumnSet(Options.IdColumnName),
					Criteria = new FilterExpression
					{
						Conditions =
						{
							new ConditionExpression(Options.DataColumnName, ConditionOperator.NotNull)
						}
					}
				};

				EntityCollection results = await Options.DataverseClient.RetrieveMultipleAsync(query, cancellationToken).ConfigureAwait(false);

				foreach (Entity record in results.Entities)
				{
					Guid recordId = record.Id;

					if (Options.DeleteRecordOnFileDelete)
					{
						await Options.DataverseClient.DeleteAsync(Options.TableName, recordId, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						var entity = new Entity(Options.TableName, recordId);
						entity[Options.DataColumnName] = null;
						entity[Options.FileNameColumnName] = null;
						await Options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
					}
				}
			}
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
			string[] parts = logicalPath.TrimStart('/').Split('/');

			if (parts.Length >= 2)
			{
				// /tableName/recordId — return the single file for that record (if it has content)
				if (!Guid.TryParse(parts[1], out Guid recordId))
					throw new ArgumentException($"The record ID segment '{parts[1]}' in subpath '{subpath}' is not a valid GUID.");

				IUmbrellaFileInfo? fileInfo = await GetAsync(logicalPath + "/", cancellationToken).ConfigureAwait(false);

				return fileInfo is not null ? [fileInfo] : [];
			}
			else
			{
				// /tableName — list all records that have file content
				string[] enumerateColumns = string.IsNullOrWhiteSpace(Options.FileSizeColumnName)
					? [Options.IdColumnName, Options.FileNameColumnName, "modifiedon"]
					: [Options.IdColumnName, Options.FileNameColumnName, "modifiedon", Options.FileSizeColumnName];

				var query = new QueryExpression(Options.TableName)
				{
					ColumnSet = new ColumnSet(enumerateColumns),
					Criteria = new FilterExpression
					{
						Conditions =
						{
							new ConditionExpression(Options.DataColumnName, ConditionOperator.NotNull)
						}
					}
				};

				EntityCollection results = await Options.DataverseClient.RetrieveMultipleAsync(query, cancellationToken).ConfigureAwait(false);

				var lstResult = new List<UmbrellaDataverseFileInfo>(results.Entities.Count);

				foreach (Entity record in results.Entities)
				{
					Guid recordId = record.Id;
					string? fileName = record.GetAttributeValue<string>(Options.FileNameColumnName);

					if (string.IsNullOrWhiteSpace(fileName))
						continue;

					string itemSubPath = $"/{Options.TableName}/{recordId:D}/{fileName}";
					string? contentType = MimeTypeUtility.GetMimeType(fileName);
					DateTime? modifiedOn = record.GetAttributeValue<DateTime?>("modifiedon");
					long? fileSize = string.IsNullOrWhiteSpace(Options.FileSizeColumnName)
						? null
						: record.GetAttributeValue<int?>(Options.FileSizeColumnName);

					var fileInfo = new UmbrellaDataverseFileInfo(
						FileInfoLoggerInstance,
						GenericTypeConverter,
						itemSubPath,
						fileName,
						Options,
						AuthorizeAsync,
						recordId,
						false);

					fileInfo.Initialize(null, modifiedOn.HasValue ? new DateTimeOffset(modifiedOn.Value, TimeSpan.Zero) : null, contentType, fileSize);

					if (await AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Read, cancellationToken).ConfigureAwait(false))
						lstResult.Add(fileInfo);
					else
						_ = Logger.WriteWarning(state: new { fileInfo.SubPath }, message: "File access denied.");
				}

				return lstResult;
			}
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
		string[] parts = logicalPath.TrimStart('/').Split('/', 3);

		if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
			throw new ArgumentException($"The subpath '{subpath}' is not in the expected format '/tableName/recordId/fileName.extension'.");

		string tableName = parts[0];
		string recordIdSegment = parts[1];
		string fileName = parts[2];

		if (!string.Equals(tableName, Options.TableName, StringComparison.OrdinalIgnoreCase))
			throw new ArgumentException($"The table name '{tableName}' in subpath '{subpath}' does not match the configured table name '{Options.TableName}'.");

		if (!Guid.TryParse(recordIdSegment, out Guid recordId))
			throw new ArgumentException($"The record ID segment '{recordIdSegment}' in subpath '{subpath}' is not a valid GUID.");

		DateTimeOffset? lastModified = null;

		long? fileSize = null;

		if (!isNew)
		{
			try
			{
				string[] metaColumns = string.IsNullOrWhiteSpace(Options.FileSizeColumnName)
					? [Options.FileNameColumnName, "modifiedon"]
					: [Options.FileNameColumnName, "modifiedon", Options.FileSizeColumnName];

				Entity entity = await Options.DataverseClient.RetrieveAsync(
					Options.TableName,
					recordId,
					new ColumnSet(metaColumns),
					cancellationToken).ConfigureAwait(false);

				if (string.IsNullOrEmpty(entity.GetAttributeValue<string>(Options.FileNameColumnName)))
					return null;

				DateTime? modifiedOn = entity.GetAttributeValue<DateTime?>("modifiedon");

				if (modifiedOn.HasValue)
					lastModified = new DateTimeOffset(modifiedOn.Value, TimeSpan.Zero);

				if (!string.IsNullOrWhiteSpace(Options.FileSizeColumnName))
					fileSize = entity.GetAttributeValue<int?>(Options.FileSizeColumnName);
			}
			catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex)
				when (ex.Detail?.ErrorCode == -2147185406
					  || ex.Message.Contains("Does Not Exist", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
		}

		string? contentType = MimeTypeUtility.GetMimeType(fileName);

		var fileInfo = new UmbrellaDataverseFileInfo(
			FileInfoLoggerInstance,
			GenericTypeConverter,
			logicalPath,
			fileName,
			Options,
			AuthorizeAsync,
			recordId,
			isNew);

		fileInfo.Initialize(null, lastModified, contentType, fileSize);

		return await FinalizeResolvedFileAsync(fileInfo, subpath, cancellationToken).ConfigureAwait(false);
	}
	#endregion
}
