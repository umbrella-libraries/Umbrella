using Umbrella.FileSystem.Abstractions;
using Umbrella.Utilities.TypeConverters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Umbrella.FileSystem.Test;

internal sealed class MetadataTestStore : IUmbrellaFileMetadataProvider
{
	internal static GenericTypeConverter Converter { get; } = new(NullLogger<GenericTypeConverter>.Instance);
	internal Dictionary<(string?, string), Dictionary<string, object?>> Files { get; } = [];
	internal bool FailWrites { get; set; }
	internal bool FailDeletes { get; set; }
	internal int Writes { get; private set; }
	internal List<UmbrellaFileMetadataContext> Contexts { get; } = [];

	public IUmbrellaFileMetadataSession CreateSession(UmbrellaFileMetadataContext context)
	{
		Contexts.Add(context);
		return new Session(this, (context.StorageNamespace, context.SubPath));
	}

	public Task DeleteFileAsync(UmbrellaFileMetadataContext context, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (FailDeletes)
			throw new IOException("Injected cleanup failure");
		_ = Files.Remove((context.StorageNamespace, context.SubPath));
		return Task.CompletedTask;
	}

	public Task DeleteDirectoryAsync(string? storageNamespace, string subPath, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (FailDeletes)
			throw new IOException("Injected cleanup failure");
		foreach (var key in Files.Keys.Where(x => x.Item1 == storageNamespace && (x.Item2 == subPath || x.Item2.StartsWith(subPath.TrimEnd('/') + "/", StringComparison.Ordinal))).ToArray())
			_ = Files.Remove(key);
		return Task.CompletedTask;
	}

	private sealed class Session(MetadataTestStore store, (string?, string) key) : UmbrellaFileMetadataSession(Converter)
	{
		protected override Task<Dictionary<string, object?>> ReadCoreAsync(CancellationToken cancellationToken)
			=> Task.FromResult(store.Files.TryGetValue(key, out var values) ? new Dictionary<string, object?>(values) : []);

		protected override Task WriteCoreAsync(IReadOnlyDictionary<string, object?> changes, bool clear, CancellationToken cancellationToken)
		{
			if (store.FailWrites)
				throw new IOException("Injected persistence failure");
			Dictionary<string, object?> values = clear || !store.Files.TryGetValue(key, out var old) ? [] : new(old);
			foreach (var pair in changes)
			{
				if (pair.Value is null)
					_ = values.Remove(pair.Key);
				else
					values[pair.Key] = pair.Value;
			}

			store.Files[key] = values;
			store.Writes++;
			return Task.CompletedTask;
		}
	}
}