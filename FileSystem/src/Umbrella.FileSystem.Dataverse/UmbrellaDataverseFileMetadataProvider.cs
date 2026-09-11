using CommunityToolkit.Diagnostics;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Dataverse;

/// <summary>Stores metadata in configured typed Dataverse columns.</summary>
public sealed class UmbrellaDataverseFileMetadataProvider(IGenericTypeConverter converter) : IUmbrellaFileMetadataProvider
{
	/// <inheritdoc />
	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
	{
		Guard.IsNotNull(context);
		var file = (UmbrellaDataverseFileInfo)context.FileInfo;
		return new Session(file.MetadataOptions, file.RecordId, converter);
	}

	/// <inheritdoc />
	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask; // Preserve mapped columns when only file content is deleted.
	}

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	private sealed class Session(UmbrellaDataverseFileStorageProviderOptions options, Guid recordId, IGenericTypeConverter converter)
		: UmbrellaFileMetadataSession(converter, StringComparer.OrdinalIgnoreCase)
	{
		protected override string NormalizeKey(string key)
			=> options.MetadataColumnMappings.TryGetValue(key, out var mapping)
				? options.MetadataColumnMappings.First(x => string.Equals(x.Value.ColumnName, mapping.ColumnName, StringComparison.OrdinalIgnoreCase)).Key
				: key;

		protected override async Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken)
		{
			if (options.MetadataColumnMappings.Count == 0)
				return new(StringComparer.OrdinalIgnoreCase);
			var entity = await options.DataverseClient.RetrieveAsync(options.TableName, recordId,
				new ColumnSet(options.MetadataColumnMappings.Values.Select(x => x.ColumnName).Distinct().ToArray()), cancellationToken).ConfigureAwait(false);
			return options.MetadataColumnMappings.ToDictionary(x => x.Key,
				x => entity.Contains(x.Value.ColumnName) ? entity[x.Value.ColumnName] : null, StringComparer.OrdinalIgnoreCase);
		}

		protected override async Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken)
		{
			var entity = new Entity(options.TableName, recordId);
			if (clear)
			{
				foreach (var mapping in options.MetadataColumnMappings.Values)
					entity[mapping.ColumnName] = null;
			}

			foreach (var pair in changes)
				entity[options.MetadataColumnMappings[pair.Key].ColumnName] = pair.Value;
			if (entity.Attributes.Count > 0)
				await options.DataverseClient.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
		}

		protected override bool TryConvertValue(string key, object? value, out object? converted)
		{
			converted = null;
			if (!options.MetadataColumnMappings.TryGetValue(key, out var mapping))
				return false;
			if (value is null)
				return true;
			converted = mapping.ColumnType switch
			{
				DataverseMetadataColumnType.Text => value.ToString(),
				DataverseMetadataColumnType.Boolean => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
				DataverseMetadataColumnType.Integer => Convert.ToInt32(value, CultureInfo.InvariantCulture),
				DataverseMetadataColumnType.Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
				DataverseMetadataColumnType.DateTime => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
				DataverseMetadataColumnType.Lookup or DataverseMetadataColumnType.Owner => new EntityReference(mapping.LookupTableName!, value is Guid guid ? guid : Guid.Parse(value.ToString()!)),
				_ => value.ToString()
			};
			return true;
		}

		protected override string? ConvertToString(object? value) => value switch
		{
			null => null,
			bool b => b.ToString(CultureInfo.InvariantCulture),
			int i => i.ToString(CultureInfo.InvariantCulture),
			decimal d => d.ToString(CultureInfo.InvariantCulture),
			DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
			EntityReference reference => reference.Id.ToString(),
			_ => value.ToString()
		};
	}
}