using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Disk;

/// <summary>Stores metadata in JSON sidecar files with the .umfsmeta extension.</summary>
public sealed class UmbrellaDiskFileMetadataProvider(IGenericTypeConverter converter, ILogger<UmbrellaDiskFileMetadataProvider> logger) : IUmbrellaFileMetadataProvider
{
	/// <inheritdoc />
	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
		=> new Session(GetPath(context), converter, logger);

	/// <inheritdoc />
	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		File.Delete(GetPath(context));
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		// Sidecars are deleted with their containing directory.
		return Task.CompletedTask;
	}

	private static string GetPath(UmbrellaFileMetadataContext context)
		{
		Guard.IsNotNull(context);
		return ((UmbrellaDiskFileInfo)context.FileInfo).PhysicalFileInfo.FullName + UmbrellaDiskFileStorageConstants.MetadataFileExtension;
	}

	private sealed class Session(string path, IGenericTypeConverter converter, ILogger logger) : UmbrellaFileMetadataSession(converter)
	{
		protected override bool TryConvertValue(string key, object? value, out object? converted)
		{
			converted = value?.ToString();
			return true;
		}

		protected override async Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken)
		{
			if (!File.Exists(path))
				return [];
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
			using var reader = new StreamReader(stream);
			#if NET8_0_OR_GREATER
			string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
			string json = await reader.ReadToEndAsync().ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
#endif
			if (string.IsNullOrWhiteSpace(json))
				return [];
			try
			{
				return (UmbrellaStatics.DeserializeJson<Dictionary<string, string>>(json) ?? [])
					.ToDictionary(x => x.Key, x => (object?)x.Value, StringComparer.Ordinal);
			}
			catch (Exception exc) when (logger.WriteError(exc, new { path }, "The metadata JSON could not be deserialized. Treating it as empty."))
			{
				return [];
			}
		}

		protected override async Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken)
		{
			Dictionary<string, object?> values = clear ? [] : await ReadCoreAsync(cancellationToken).ConfigureAwait(false);
			foreach (var pair in changes)
			{
				if (pair.Value is null)
					_ = values.Remove(pair.Key);
				else
					values[pair.Key] = pair.Value;
			}

			cancellationToken.ThrowIfCancellationRequested();
			if (values.Count == 0)
			{
				File.Delete(path);
				return;
			}

			string json = UmbrellaStatics.SerializeJson(values.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? ""));
			using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
			using var writer = new StreamWriter(stream);
			await writer.WriteAsync(json).ConfigureAwait(false);
		}
	}
}