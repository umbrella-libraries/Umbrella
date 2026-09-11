using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.Disk;
using Umbrella.Internal.Mocks;

namespace Umbrella.FileSystem.Test;

public sealed class UmbrellaFileMetadataTest : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "umbrella-metadata-" + Guid.NewGuid().ToString("N"));
	private static CancellationToken Token => TestContext.Current.CancellationToken;

	[Fact]
	public async Task SessionPreservesTypedValuesAndPendingMutations()
	{
		var store = new MetadataTestStore();
		var context = new UmbrellaFileMetadataContext(Mock.Of<IUmbrellaFileInfo>(), "one", "/file");
		var session = store.CreateSession(context);
		await session.SetMetadataValueAsync("number", 42, false, Token);
		Assert.Equal(42, await session.GetMetadataValueAsync<int>("number", cancellationToken: Token));
		Assert.Equal("converted:42", await session.GetMetadataValueAsync("number", "fallback", x => "converted:" + x, Token));
		Assert.Empty(await session.ReadPersistedMetadataAsync(Token));
		Assert.Equal(-1, await store.CreateSession(context).GetMetadataValueAsync("number", -1, cancellationToken: Token));
		await session.WriteMetadataChangesAsync(Token);
		_ = Assert.IsType<int>(store.Files[("one", "/file")]["number"]);
		await session.RemoveMetadataValueAsync("number", false, Token);
		Assert.Equal(-1, await session.GetMetadataValueAsync("number", -1, cancellationToken: Token));
		_ = Assert.Single(await session.ReadPersistedMetadataAsync(Token));
		await session.ClearMetadataAsync(false, Token);
		await session.SetMetadataValueAsync("after-clear", true, false, Token);
		await session.WriteMetadataChangesAsync(Token);
		_ = Assert.Single(await session.ReadPersistedMetadataAsync(Token));
		Assert.True(await session.GetMetadataValueAsync<bool>("after-clear", cancellationToken: Token));
		await session.SetMetadataValueAsync<object?>("after-clear", null, true, Token);
		Assert.Empty(await session.ReadPersistedMetadataAsync(Token));
	}

	[Fact]
	public async Task FailedWritesRetainClearAndPendingValuesForRetry()
	{
		var store = new MetadataTestStore();
		var session = store.CreateSession(new(Mock.Of<IUmbrellaFileInfo>(), "one", "/file"));
		await session.SetMetadataValueAsync("old", 1, cancellationToken: Token);
		await session.ClearMetadataAsync(false, Token);
		store.FailWrites = true;
		_ = await Assert.ThrowsAsync<IOException>(() => session.SetMetadataValueAsync("new", 2, cancellationToken: Token));
		Assert.Equal(-1, await session.GetMetadataValueAsync("old", -1, cancellationToken: Token));
		Assert.Equal(2, await session.GetMetadataValueAsync<int>("new", cancellationToken: Token));
		store.FailWrites = false;
		await session.WriteMetadataChangesAsync(Token);
		Assert.Equal(["new"], (await session.ReadPersistedMetadataAsync(Token)).Keys);
	}

	[Fact]
	public async Task CancellationDoesNotStageOrPersistChanges()
	{
		var store = new MetadataTestStore();
		var session = store.CreateSession(new(Mock.Of<IUmbrellaFileInfo>(), "one", "/file"));
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SetMetadataValueAsync("key", 1, cancellationToken: cancellation.Token));
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ClearMetadataAsync(cancellationToken: cancellation.Token));
		await session.WriteMetadataChangesAsync(Token);
		Assert.Equal(0, store.Writes);
	}

	[Fact]
	public async Task DiskNativeTruncatesJsonAndDeletesLastValue()
	{
		var provider = CreateDisk();
		var file = await WriteFileAsync(provider, "/test.bin");
		await file.SetMetadataValueAsync("key", new string('x', 2048), cancellationToken: Token);
		await file.SetMetadataValueAsync("key", "short", cancellationToken: Token);
		var fresh = await provider.GetAsync(file.SubPath, Token);
		Assert.Equal("short", await fresh!.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		await fresh.RemoveMetadataValueAsync("key", cancellationToken: Token);
		Assert.False(File.Exists(Path.Combine(_root, "test.bin.umfsmeta")));
		await fresh.SetMetadataValueAsync("again", "value", cancellationToken: Token);
		await fresh.ClearMetadataAsync(cancellationToken: Token);
		Assert.False(File.Exists(Path.Combine(_root, "test.bin.umfsmeta")));
	}

	[Fact]
	public async Task CustomBackendIsExclusiveAndNamespaceIsCaptured()
	{
		var native = CreateDisk();
		var file = await WriteFileAsync(native, "/test.bin");
		await file.SetMetadataValueAsync("native", "untouched", cancellationToken: Token);
		var store = new MetadataTestStore();
		var provider = CreateDisk(store, "one");
		var custom = await provider.GetAsync("TEST.BIN", Token);
		Assert.Null(await custom!.GetMetadataValueAsync<string>("native", cancellationToken: Token));
		await custom.SetMetadataValueAsync("external", 7, cancellationToken: Token);
		await custom.WriteFromByteArrayAsync([4, 5], cancellationToken: Token);
		Assert.Equal(7, await custom.GetMetadataValueAsync<int>("external", cancellationToken: Token));
		Assert.Equal("untouched", await (await native.GetAsync("/test.bin", Token))!.GetMetadataValueAsync<string>("native", cancellationToken: Token));
		var other = await CreateDisk(store, "two").GetAsync("/test.bin", Token);
		Assert.Equal(-1, await other!.GetMetadataValueAsync("external", -1, cancellationToken: Token));
		Assert.All(store.Contexts, x => Assert.Equal("/test.bin", x.SubPath));
	}

	[Fact]
	public async Task CopyReplacesDestinationAndExcludesUncommittedSourceChanges()
	{
		var store = new MetadataTestStore();
		var native = CreateDisk();
		var custom = CreateDisk(store);
		var source = await WriteFileAsync(native, "/source.bin");
		await source.SetMetadataValueAsync("key", "persisted", cancellationToken: Token);
		await source.SetMetadataValueAsync("key", "pending", false, Token);
		var target = await WriteFileAsync(custom, "/target.bin");
		await target.SetMetadataValueAsync("stale", true, cancellationToken: Token);
		_ = await source.CopyAsync(target, Token);
		Assert.Equal("persisted", await target.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		Assert.False(await target.GetMetadataValueAsync<bool>("stale", cancellationToken: Token));
		var empty = await WriteFileAsync(native, "/empty.bin");
		_ = await empty.CopyAsync(target, Token);
		Assert.Empty(store.Files[("files", "/target.bin")]);
		await target.SetMetadataValueAsync("reverse", "works", cancellationToken: Token);
		_ = await target.CopyAsync(source, Token);
		Assert.Equal("works", await source.GetMetadataValueAsync<string>("reverse", cancellationToken: Token));
	}

	[Fact]
	public async Task FailedMoveKeepsSourceAndDeleteCleanupCanBeRetried()
	{
		var store = new MetadataTestStore();
		var provider = CreateDisk(store);
		var source = await WriteFileAsync(provider, "/source.bin");
		await source.SetMetadataValueAsync("owner", "user", cancellationToken: Token);
		store.FailWrites = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => source.MoveAsync("/target.bin", Token));
		Assert.True(File.Exists(Path.Combine(_root, "source.bin")));
		Assert.True(store.Files.ContainsKey(("files", "/source.bin")));
		store.FailWrites = false;
		store.FailDeletes = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => provider.DeleteAsync("/source.bin", Token));
		Assert.False(File.Exists(Path.Combine(_root, "source.bin")));
		store.FailDeletes = false;
		Assert.True(await provider.DeleteAsync("/source.bin", Token));
		Assert.False(store.Files.ContainsKey(("files", "/source.bin")));
	}

	[Fact]
	public async Task DirectoryCleanupIsBoundedAndRetryable()
	{
		var store = new MetadataTestStore();
		var provider = CreateDisk(store);
		foreach (string path in new[] { "/dir/a.bin", "/dir/nested/b.bin", "/directory/c.bin" })
			await (await WriteFileAsync(provider, path)).SetMetadataValueAsync("key", "value", cancellationToken: Token);
		store.Files[("other", "/dir/a.bin")] = new() { ["key"] = "other" };
		store.FailDeletes = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => provider.DeleteDirectoryAsync("/dir", Token));
		Assert.False(Directory.Exists(Path.Combine(_root, "dir")));
		store.FailDeletes = false;
		await provider.DeleteDirectoryAsync("/dir", Token);
		Assert.False(store.Files.ContainsKey(("files", "/dir/a.bin")));
		Assert.False(store.Files.ContainsKey(("files", "/dir/nested/b.bin")));
		Assert.True(store.Files.ContainsKey(("files", "/directory/c.bin")));
		Assert.True(store.Files.ContainsKey(("other", "/dir/a.bin")));
	}

	[Fact]
	public async Task MetadataReadsDuringAuthorizationDoNotRecurseAndDeniedWritesDoNotPersist()
	{
		var store = new MetadataTestStore();
		var file = await WriteFileAsync(CreateDisk(store), "/test.bin");
		await file.SetMetadataValueAsync("owner", "user", cancellationToken: Token);
		var handler = new MetadataAuthorizationHandler();
		var provider = CreateDisk(store, handlers: [handler]);
		var authorized = await provider.GetAsync("/test.bin", Token);
		Assert.NotNull(authorized);
		handler.DenyUpdate = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => authorized!.SetMetadataValueAsync("changed", true, cancellationToken: Token));
		Assert.False(store.Files[("files", "/test.bin")].ContainsKey("changed"));
		Assert.InRange(handler.Calls, 2, 4);
	}

	[Fact]
	public async Task MissingFileCleanupReadsSurvivingMetadataAndRequiresDeletePermission()
	{
		var store = new MetadataTestStore();
		var file = await WriteFileAsync(CreateDisk(store), "/test.bin");
		await file.SetMetadataValueAsync("owner", "user", cancellationToken: Token);
		File.Delete(Path.Combine(_root, "test.bin"));
		var handler = new MetadataAuthorizationHandler { DenyDelete = true };
		var provider = CreateDisk(store, handlers: [handler]);
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => provider.DeleteAsync("/test.bin", Token));
		_ = Assert.Single(store.Files);
		handler.DenyDelete = false;
		Assert.True(await provider.DeleteAsync("/test.bin", Token));
		Assert.Empty(store.Files);
	}

	[Fact]
	public void CustomProviderRequiresNamespace()
	{
		_ = Assert.Throws<ArgumentException>(() => CreateDisk(new MetadataTestStore(), " "));
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(false, false, true)]
	[InlineData(false, true, false)]
	[InlineData(false, true, true)]
	[InlineData(true, false, false)]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	[InlineData(true, true, true)]
	public async Task CopyAndMoveToNewFileRequireCreateButNotUpdate(bool move, bool hasMetadata, bool custom)
	{
		var store = custom ? new MetadataTestStore() : null;
		var source = await WriteFileAsync(CreateDisk(store), "/files/source.bin");
		if (hasMetadata)
			await source.SetMetadataValueAsync("key", "value", cancellationToken: Token);
		var handler = new CreateOnlyAuthorizationHandler();
		var restricted = CreateDisk(store, handlers: [handler]);
		source = (await restricted.GetAsync(source.SubPath, Token))!;
		var destination = await restricted.CreateAsync("/files/destination.bin", Token);
		_ = move ? await source.MoveAsync(destination, Token) : await source.CopyAsync(destination, Token);
		Assert.Equal(new byte[] { 1, 2, 3 }, await destination.ReadAsByteArrayAsync(cancellationToken: Token));
		Assert.Equal(hasMetadata ? "value" : null, await destination.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		Assert.DoesNotContain(UmbrellaFileOperationType.Update, handler.Operations);
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => destination.SetMetadataValueAsync("later", "denied", cancellationToken: Token));
		Assert.Equal(!move, File.Exists(Path.Combine(_root, "files", "source.bin")));
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task CopyAndMoveDeniedUpdateLeaveExistingDestinationUntouched(bool move, bool custom)
	{
		var store = custom ? new MetadataTestStore() : null;
		var unrestricted = CreateDisk(store);
		var source = await WriteFileAsync(unrestricted, "/files/source.bin");
		var original = await WriteFileAsync(unrestricted, "/files/destination.bin");
		await original.WriteFromByteArrayAsync([9], cancellationToken: Token);
		await original.SetMetadataValueAsync("key", "original", cancellationToken: Token);
		var restricted = CreateDisk(store, handlers: [new CreateOnlyAuthorizationHandler()]);
		source = (await restricted.GetAsync(source.SubPath, Token))!;
		var destination = (await restricted.GetAsync(original.SubPath, Token))!;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => move ? source.MoveAsync(destination, Token) : source.CopyAsync(destination, Token));
		Assert.Equal(new byte[] { 9 }, await destination.ReadAsByteArrayAsync(cancellationToken: Token));
		Assert.Equal("original", await destination.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		Assert.True(File.Exists(Path.Combine(_root, "files", "source.bin")));
	}

	private sealed class CreateOnlyAuthorizationHandler : IUmbrellaFileAuthorizationHandler
	{
		public string DirectoryName => "files";
		public List<UmbrellaFileOperationType> Operations { get; } = [];
		public Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
		{
			Operations.Add(operationType);
			return Task.FromResult(operationType != UmbrellaFileOperationType.Update);
		}
	}

	private UmbrellaDiskFileStorageProvider CreateDisk(MetadataTestStore? metadata = null, string? metadataNamespace = "files", IEnumerable<IUmbrellaFileAuthorizationHandler>? handlers = null)
	{
		var provider = new UmbrellaDiskFileStorageProvider(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter,
			new UmbrellaFileAuthorizationHandlerRegistry(handlers ?? []));
		provider.InitializeOptions(new UmbrellaDiskFileStorageProviderOptions
		{
			RootPhysicalPath = _root,
			AllowUnhandledFileAuthorizationChecks = handlers is null,
			MetadataProvider = metadata,
			MetadataNamespace = metadataNamespace
		});
		return provider;
	}

	private static async Task<IUmbrellaFileInfo> WriteFileAsync(UmbrellaDiskFileStorageProvider provider, string path)
	{
		var file = await provider.CreateAsync(path, Token);
		await file.WriteFromByteArrayAsync([1, 2, 3], cancellationToken: Token);
		return file;
	}

	private sealed class MetadataAuthorizationHandler : IUmbrellaFileAuthorizationHandler
	{
		internal bool DenyUpdate { get; set; }
		internal bool DenyDelete { get; set; }
		internal int Calls { get; private set; }
		public string DirectoryName => "test.bin";
		public async Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operation, CancellationToken cancellationToken = default)
		{
			Calls++;
			return await fileInfo.GetMetadataValueAsync<string>("owner", cancellationToken: cancellationToken) == "user"
				&& !(DenyUpdate && operation == UmbrellaFileOperationType.Update)
				&& !(DenyDelete && operation == UmbrellaFileOperationType.Delete);
		}
	}

	public void Dispose()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}
}