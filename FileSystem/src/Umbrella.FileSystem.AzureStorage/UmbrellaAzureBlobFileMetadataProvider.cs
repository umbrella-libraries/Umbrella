using CommunityToolkit.Diagnostics;
using Azure.Storage.Blobs;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.AzureStorage;

/// <summary>Stores metadata in the blob's native metadata collection.</summary>
public sealed class UmbrellaAzureBlobFileMetadataProvider(IGenericTypeConverter converter) : IUmbrellaFileMetadataProvider
{
	/// <inheritdoc />
	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
		{
		Guard.IsNotNull(context);
		return new Session(((UmbrellaAzureBlobFileInfo)context.FileInfo).Blob, converter);
	}

	/// <inheritdoc />
	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask; // Metadata is deleted with the blob.
	}

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	private sealed class Session(BlobClient blob, IGenericTypeConverter converter) : UmbrellaFileMetadataSession(converter, StringComparer.OrdinalIgnoreCase)
	{
		protected override bool TryConvertValue(string key, object? value, out object? converted)
		{
			converted = value?.ToString();
			return true;
		}

		protected override async Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken)
		{
			var response = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			return response.Value.Metadata.ToDictionary(x => x.Key, x => (object?)x.Value, StringComparer.OrdinalIgnoreCase);
		}

		protected override async Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken)
		{
			Dictionary<string, object?> values = clear ? new(StringComparer.OrdinalIgnoreCase) : await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
			foreach (var pair in changes)
			{
				if (pair.Value is null)
					_ = values.Remove(pair.Key);
				else
					values[pair.Key] = pair.Value;
			}

			_ = await blob.SetMetadataAsync(values.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? ""), cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}
}