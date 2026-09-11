using System.Net.Http;
using System.Reflection;
using System.Text;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Dataverse;
using Umbrella.FileSystem.SharePoint;
using Umbrella.Internal.Mocks;

namespace Umbrella.FileSystem.Test;

public class UmbrellaRemoteFileMetadataTest
{
	private static CancellationToken Token => TestContext.Current.CancellationToken;
	private static readonly UmbrellaFileAccessAuthorizor _allow = (_, _, _) => Task.FromResult(true);

	[Theory]
	[InlineData(DataverseMetadataColumnType.Text)]
	[InlineData(DataverseMetadataColumnType.Boolean)]
	[InlineData(DataverseMetadataColumnType.Integer)]
	[InlineData(DataverseMetadataColumnType.Decimal)]
	[InlineData(DataverseMetadataColumnType.DateTime)]
	[InlineData(DataverseMetadataColumnType.Lookup)]
	[InlineData(DataverseMetadataColumnType.Owner)]
	public async Task DataverseNativePreservesColumnTypesAndDeferredChanges(DataverseMetadataColumnType type)
	{
		var id = Guid.NewGuid();
		object value = type switch
		{
			DataverseMetadataColumnType.Text => "title",
			DataverseMetadataColumnType.Boolean => true,
			DataverseMetadataColumnType.Integer => 42,
			DataverseMetadataColumnType.Decimal => 12.5m,
			DataverseMetadataColumnType.DateTime => new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
			_ => id
		};
		var client = new Mock<IOrganizationServiceAsync2>();
		var entity = new Entity("note", id) { ["subject"] = "old" };
		_ = client.Setup(x => x.RetrieveAsync("note", id, It.IsAny<ColumnSet>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => entity);
		_ = client.Setup(x => x.UpdateAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>()))
			.Callback<Entity, CancellationToken>((update, _) =>
			{
				foreach (var pair in update.Attributes)
					entity[pair.Key] = pair.Value;
			}).Returns(Task.CompletedTask);
		var options = new UmbrellaDataverseFileStorageProviderOptions
		{
			DataverseClient = client.Object,
			TableName = "note",
			MetadataColumnMappings = new(StringComparer.OrdinalIgnoreCase)
			{
				["Title"] = new() { ColumnName = "subject", ColumnType = type, LookupTableName = "systemuser" }
			}
		};
		options.MetadataColumnMappings["Alias"] = options.MetadataColumnMappings["Title"];
		var file = CreateDataverse(options, id, new UmbrellaDataverseFileMetadataProvider(MetadataTestStore.Converter));
		await file.SetMetadataValueAsync("Title", value, false, Token);
		Assert.Equal(await file.GetMetadataValueAsync<string>("Title", cancellationToken: Token), await file.GetMetadataValueAsync<string>("Alias", cancellationToken: Token));
		Assert.Equal("old", entity["subject"]);
		await file.WriteMetadataChangesAsync(Token);
		if (type is DataverseMetadataColumnType.Lookup or DataverseMetadataColumnType.Owner)
		{
			var reference = Assert.IsType<EntityReference>(entity["subject"]);
			Assert.Equal(id, reference.Id);
			Assert.Equal("systemuser", reference.LogicalName);
			Assert.Equal(id.ToString(), await file.GetMetadataValueAsync<string>("title", cancellationToken: Token));
		}
		else
		{
			Assert.Equal(value, entity["subject"]);
		}

		await file.RemoveMetadataValueAsync("TITLE", false, Token);
		Assert.Equal("fallback", await file.GetMetadataValueAsync("title", "fallback", cancellationToken: Token));
		await file.WriteMetadataChangesAsync(Token);
		Assert.Null(entity["subject"]);
		await file.SetMetadataValueAsync("unmapped", "ignored", cancellationToken: Token);
		Assert.False(entity.Contains("unmapped"));
		await file.SetMetadataValueAsync("Title", value, cancellationToken: Token);
		await file.ClearMetadataAsync(cancellationToken: Token);
		Assert.Null(entity["subject"]);
	}

	[Fact]
	public async Task DataverseCustomBackendAvoidsMappedColumnsAndPreservesUnsupportedOperations()
	{
		var client = new Mock<IOrganizationServiceAsync2>(MockBehavior.Strict);
		var options = new UmbrellaDataverseFileStorageProviderOptions { DataverseClient = client.Object };
		var store = new MetadataTestStore();
		var file = CreateDataverse(options, Guid.NewGuid(), store);
		await file.SetMetadataValueAsync("arbitrary", 123, cancellationToken: Token);
		Assert.Equal(123, await file.GetMetadataValueAsync<int>("arbitrary", cancellationToken: Token));
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => file.CopyAsync("/target", Token));
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => file.MoveAsync("/target", Token));
		client.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task AzureNativeWritesAreNotOverwrittenByStaleUploadProperties()
	{
		Dictionary<string, string> persisted = new(StringComparer.OrdinalIgnoreCase) { ["key"] = "old" };
		var blob = new Mock<BlobClient>();
		_ = blob.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => Response.FromValue(BlobsModelFactory.BlobProperties(metadata: new Dictionary<string, string>(persisted)), Mock.Of<Response>()));
		_ = blob.Setup(x => x.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.Callback<IDictionary<string, string>, BlobRequestConditions, CancellationToken>((values, _, _) => persisted = new(values, StringComparer.OrdinalIgnoreCase))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobInfo(default, default), Mock.Of<Response>()));
		_ = blob.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobHttpHeaders>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<IProgress<long>>(), It.IsAny<AccessTier?>(), It.IsAny<StorageTransferOptions>(), It.IsAny<CancellationToken>()))
			.Callback<Stream, BlobHttpHeaders, IDictionary<string, string>, BlobRequestConditions, IProgress<long>, AccessTier?, StorageTransferOptions, CancellationToken>((_, _, values, _, _, _, _, _) => persisted = new(values, StringComparer.OrdinalIgnoreCase))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(default, default, [], "", 0), Mock.Of<Response>()));
		var file = CreateAzure(blob.Object, new UmbrellaAzureBlobFileMetadataProvider(MetadataTestStore.Converter));
		typeof(UmbrellaAzureBlobFileInfo).GetField("_blobProperties", BindingFlags.NonPublic | BindingFlags.Instance)!
			.SetValue(file, BlobsModelFactory.BlobProperties(metadata: new Dictionary<string, string> { ["key"] = "stale" }));
		await file.SetMetadataValueAsync("key", "saved", cancellationToken: Token);
		await file.SetMetadataValueAsync("key", "pending", false, Token);
		await file.WriteFromByteArrayAsync([1, 2], cancellationToken: Token);
		Assert.Equal("saved", persisted["key"]);
		Assert.Equal("pending", await file.GetMetadataValueAsync<string>("KEY", cancellationToken: Token));
		await file.ClearMetadataAsync(cancellationToken: Token);
		Assert.Empty(persisted);
	}

	[Fact]
	public async Task AzureCustomMetadataDoesNotAccessBlobMetadataAndCleansAfterDeletion()
	{
		var blob = new Mock<BlobClient>(MockBehavior.Strict);
		_ = blob.Setup(x => x.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
		var store = new MetadataTestStore();
		var file = CreateAzure(blob.Object, store);
		await file.SetMetadataValueAsync("key", 1, cancellationToken: Token);
		Assert.Equal(1, await file.GetMetadataValueAsync<int>("key", cancellationToken: Token));
		Assert.True(await file.DeleteAsync(Token));
		Assert.Empty(store.Files);
		blob.Verify(x => x.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
		blob.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task AzureMissingFileCleanupDoesNotCreateAContainer()
	{
		var service = new Mock<BlobServiceClient>(MockBehavior.Strict);
		var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
		var blob = new Mock<BlobClient>(MockBehavior.Strict);
		_ = service.Setup(x => x.GetBlobContainerClient("container")).Returns(container.Object);
		_ = container.Setup(x => x.GetBlobClient("file.bin")).Returns(blob.Object);
		_ = blob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
		var store = new MetadataTestStore();
		store.Files[("remote", "/container/file.bin")] = new() { ["key"] = "value" };
		using var provider = new TestAzureProvider(service.Object, store);
		Assert.True(await provider.DeleteAsync("/container/file.bin", Token));
		Assert.Empty(store.Files);
		container.Verify(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task SharePointCustomMetadataSurvivesUploadAndCopiesBeforeMoveDeletion()
	{
		var requests = new List<HttpMethod>();
		using var handler = new GraphHandler(request =>
		{
			requests.Add(request.Method);
			if (request.Method == HttpMethod.Delete)
				return new(HttpStatusCode.NoContent);
			if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/content", StringComparison.Ordinal))
				return new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
			return Json("{\"id\":\"item\",\"size\":3}");
		});
		using var http = new HttpClient(handler);
		using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());
		var store = new MetadataTestStore();
		var source = CreateSharePoint(graph, http, store, "/logical/source.bin", true);
		_ = await Assert.ThrowsAsync<InvalidOperationException>(() => source.SetMetadataValueAsync("key", 1, cancellationToken: Token));
		await source.WriteFromByteArrayAsync([1], cancellationToken: Token);
		await source.SetMetadataValueAsync("key", 42, cancellationToken: Token);
		await source.WriteFromByteArrayAsync([2], cancellationToken: Token);
		Assert.Equal(42, await source.GetMetadataValueAsync<int>("key", cancellationToken: Token));
		var target = CreateSharePoint(graph, http, store, "/logical/target.bin", true);
		store.FailWrites = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => source.MoveAsync(target, Token));
		Assert.DoesNotContain(HttpMethod.Delete, requests);
		store.FailWrites = false;
		_ = await source.MoveAsync(target, Token);
		Assert.Equal(HttpMethod.Delete, requests.Last());
		Assert.False(store.Files.ContainsKey(("remote", "/logical/source.bin")));
		Assert.Equal(42, await target.GetMetadataValueAsync<int>("key", cancellationToken: Token));
		Assert.All(store.Contexts, context => Assert.StartsWith("/logical/", context.SubPath, StringComparison.Ordinal));
	}

	[Fact]
	public async Task SharePointDefaultMetadataIsUnsupportedButFileDeletionStillWorks()
	{
		using var handler = new GraphHandler(_ => new(HttpStatusCode.NoContent));
		using var http = new HttpClient(handler);
		using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());
		var file = CreateSharePoint(graph, http, new UmbrellaUnsupportedFileMetadataProvider(), "/logical/source.bin");
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => file.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => file.SetMetadataValueAsync("key", "value", cancellationToken: Token));
		_ = await Assert.ThrowsAsync<NotSupportedException>(() => file.ClearMetadataAsync(cancellationToken: Token));
		Assert.True(await file.DeleteAsync(Token));
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(0, true)]
	[InlineData(1, false)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	[InlineData(2, true)]
	public async Task AzureMoveWaitsForCopyAndNeverDeletesSourceOnFailure(int outcome, bool hasMetadata)
	{
		var sourceBlob = new Mock<BlobClient>();
		var targetBlob = new Mock<BlobClient>();
		var completion = new TaskCompletionSource<Response<long>>(TaskCreationOptions.RunContinuationsAsynchronously);
		var operation = new Mock<CopyFromUriOperation>();
		_ = operation.Setup(x => x.WaitForCompletionAsync(It.IsAny<CancellationToken>())).Returns(() => new ValueTask<Response<long>>(completion.Task));
		_ = sourceBlob.SetupGet(x => x.Uri).Returns(new Uri("https://storage.example/container/source"));
		_ = sourceBlob.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
		_ = sourceBlob.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: hasMetadata ? new Dictionary<string, string> { ["key"] = "value" } : []), Mock.Of<Response>()));
		_ = sourceBlob.Setup(x => x.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
		_ = targetBlob.Setup(x => x.StartCopyFromUriAsync(It.IsAny<Uri>(), It.IsAny<BlobCopyFromUriOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(operation.Object);
		_ = targetBlob.Setup(x => x.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobProperties(metadata: new Dictionary<string, string>()), Mock.Of<Response>()));
		_ = targetBlob.Setup(x => x.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobInfo(default, default), Mock.Of<Response>()));
		var backend = new UmbrellaAzureBlobFileMetadataProvider(MetadataTestStore.Converter);
		var source = CreateAzure(sourceBlob.Object, backend);
		var targetOperations = new List<UmbrellaFileOperationType>();
		var target = CreateAzure(targetBlob.Object, backend, true, (_, operationType, _) =>
		{
			targetOperations.Add(operationType);
			return Task.FromResult(operationType != UmbrellaFileOperationType.Update);
		});
		Task<IUmbrellaFileInfo> move = source.MoveAsync(target, Token);
		Assert.False(move.IsCompleted);
		Assert.Equal([UmbrellaFileOperationType.Create], targetOperations);
		sourceBlob.Verify(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
		if (outcome == 0)
		{
			completion.SetResult(Response.FromValue(3L, Mock.Of<Response>()));
			_ = await move;
			targetBlob.Verify(x => x.SetMetadataAsync(It.Is<IDictionary<string, string>>(values => hasMetadata ? values.Count == 1 && values["key"] == "value" : values.Count == 0), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
			sourceBlob.Verify(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
		}
		else
		{
			if (outcome == 1)
			{
				completion.SetException(new RequestFailedException(500, "Copy failed"));
				_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => move);
			}
			else
			{
				completion.SetCanceled(Token);
				_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => move);
			}

			sourceBlob.Verify(x => x.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
			targetBlob.Verify(x => x.SetMetadataAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		Assert.DoesNotContain(UmbrellaFileOperationType.Update, targetOperations);
	}

	[Fact]
	public async Task SharePointProviderUsesLogicalIdentityAndRetainsMetadataWhenDirectoryDeletionFails()
	{
		bool failDelete = true;
		using var handler = new GraphHandler(request =>
		{
			if (request.Method == HttpMethod.Delete)
				return failDelete ? new(HttpStatusCode.InternalServerError) { Content = new StringContent("{}", Encoding.UTF8, "application/json") } : new(HttpStatusCode.NoContent);
			if (request.RequestUri!.AbsolutePath.EndsWith("/drives", StringComparison.Ordinal))
				return Json("{\"value\":[{\"id\":\"drive\",\"name\":\"library\"}]}");
			return Json("{\"id\":\"item\",\"size\":3}");
		});
		using var http = new HttpClient(handler);
		using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());
		var store = new MetadataTestStore();
		using var provider = new UmbrellaSharePointFileStorageProvider(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter, new UmbrellaFileAuthorizationHandlerRegistry([]));
		var options = new UmbrellaSharePointFileStorageProviderOptions
		{
			GraphServiceClient = graph, DownloadHttpClient = http, SiteId = "site", DriveName = "library", AllowUnhandledFileAuthorizationChecks = true,
			MetadataProvider = store, MetadataNamespace = "library-a", SubPathTranslator = path => "physical" + path
		};
		provider.InitializeOptions(options);
		options.MetadataNamespace = "changed-after-initialization";
		var file = await provider.GetAsync("/LOGICAL/Test.bin", Token);
		await file!.SetMetadataValueAsync("key", "value", cancellationToken: Token);
		Assert.True(store.Files.ContainsKey(("library-a", "/logical/test.bin")));
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => provider.DeleteDirectoryAsync("/logical", Token));
		_ = Assert.Single(store.Files);
		failDelete = false;
		await provider.DeleteDirectoryAsync("/logical", Token);
		Assert.Empty(store.Files);
	}

	[Fact]
	public async Task DataverseDirectoryPartialFailureDoesNotCleanMetadata()
	{
		var client = new Mock<IOrganizationServiceAsync2>();
		var first = new Entity("note", Guid.NewGuid());
		var second = new Entity("note", Guid.NewGuid());
		_ = client.Setup(x => x.RetrieveMultipleAsync(It.IsAny<QueryBase>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EntityCollection([first, second]));
		_ = client.Setup(x => x.UpdateAsync(It.Is<Entity>(e => e.Id == first.Id), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
		_ = client.Setup(x => x.UpdateAsync(It.Is<Entity>(e => e.Id == second.Id), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Content deletion failed"));
		var store = new MetadataTestStore();
		store.Files[("notes", $"/note/{first.Id:D}/file.bin")] = new() { ["key"] = "one" };
		store.Files[("notes", $"/note/{second.Id:D}/file.bin")] = new() { ["key"] = "two" };
		var provider = new UmbrellaDataverseFileStorageProvider(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter, new UmbrellaFileAuthorizationHandlerRegistry([]));
		provider.InitializeOptions(new UmbrellaDataverseFileStorageProviderOptions
		{
			DataverseClient = client.Object, TableName = "note", IdColumnName = "noteid", DataColumnName = "data", FileNameColumnName = "filename",
			MetadataProvider = store, MetadataNamespace = "notes"
		});
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => provider.DeleteDirectoryAsync("/note", Token));
		Assert.Equal(2, store.Files.Count);
	}

	[Theory]
	[InlineData("N", true)]
	[InlineData("N", false)]
	[InlineData("D", true)]
	[InlineData("D", false)]
	[InlineData("B", true)]
	[InlineData("B", false)]
	[InlineData("P", true)]
	[InlineData("P", false)]
	public async Task DataverseUsesCanonicalIdentityForLookupEnumerationAndCleanupRetries(string format, bool directory)
	{
		var id = Guid.NewGuid();
		var entity = new Entity("note", id) { ["filename"] = "file.bin" };
		var client = new Mock<IOrganizationServiceAsync2>();
		_ = client.Setup(x => x.RetrieveAsync("note", id, It.IsAny<ColumnSet>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
		_ = client.Setup(x => x.RetrieveMultipleAsync(It.IsAny<QueryBase>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EntityCollection([entity]));
		_ = client.Setup(x => x.UpdateAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>())).Callback<Entity, CancellationToken>((update, _) =>
		{
			foreach (var pair in update.Attributes)
				entity[pair.Key] = pair.Value;
		}).Returns(Task.CompletedTask);
		var store = new MetadataTestStore();
		var provider = new UmbrellaDataverseFileStorageProvider(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter, new UmbrellaFileAuthorizationHandlerRegistry([]));
		provider.InitializeOptions(new UmbrellaDataverseFileStorageProviderOptions
		{
			DataverseClient = client.Object, TableName = "note", IdColumnName = "noteid", DataColumnName = "data", FileNameColumnName = "filename",
			MetadataProvider = store, MetadataNamespace = "notes", AllowUnhandledFileAuthorizationChecks = true
		});
		string inputDirectory = $"/NOTE/{id.ToString(format).ToUpperInvariant()}";
		string inputPath = inputDirectory + "/FILE.BIN";
		string canonical = $"/note/{id:D}/file.bin";
		Assert.Equal(canonical, (await provider.CreateAsync(inputPath, Token)).SubPath);
		var file = (await provider.GetAsync(inputPath, Token))!;
		await file.SetMetadataValueAsync("key", "value", cancellationToken: Token);
		Assert.Equal(canonical, file.SubPath);
		Assert.Equal("value", await (await provider.GetAsync(canonical, Token))!.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		var enumerated = Assert.Single(await provider.EnumerateDirectoryAsync("/note", Token));
		Assert.Equal(canonical, enumerated.SubPath);
		Assert.Equal("value", await enumerated.GetMetadataValueAsync<string>("key", cancellationToken: Token));
		Assert.All(store.Contexts, context => Assert.Equal(canonical, context.SubPath));
		store.FailDeletes = true;
		_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => directory ? provider.DeleteDirectoryAsync(inputDirectory, Token) : provider.DeleteAsync(inputPath, Token));
		_ = Assert.Single(store.Files);
		store.FailDeletes = false;
		if (directory)
			await provider.DeleteDirectoryAsync(inputDirectory, Token);
		else
			Assert.True(await provider.DeleteAsync(inputPath, Token));
		Assert.Empty(store.Files);
	}

	private static UmbrellaDataverseFileInfo CreateDataverse(UmbrellaDataverseFileStorageProviderOptions options, Guid id, IUmbrellaFileMetadataProvider metadata)
		=> Construct<UmbrellaDataverseFileInfo>(NullLogger<UmbrellaDataverseFileInfo>.Instance, MetadataTestStore.Converter,
			$"/note/{id:D}/file.bin", "file.bin", options, _allow, id, false, metadata, "remote");

	private static UmbrellaAzureBlobFileInfo CreateAzure(BlobClient blob, IUmbrellaFileMetadataProvider metadata, bool isNew = false, UmbrellaFileAccessAuthorizor? authorize = null)
		=> Construct<UmbrellaAzureBlobFileInfo>(NullLogger<UmbrellaAzureBlobFileInfo>.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter,
			"/container/file.bin", Mock.Of<IUmbrellaAzureBlobFileStorageProvider>(), authorize ?? _allow, blob, isNew, metadata, "remote");

	private static UmbrellaSharePointFileInfo CreateSharePoint(GraphServiceClient graph, HttpClient download, IUmbrellaFileMetadataProvider metadata, string path, bool isNew = false)
		=> Construct<UmbrellaSharePointFileInfo>(NullLogger<UmbrellaSharePointFileInfo>.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter,
			path, "physical/" + Path.GetFileName(path), Mock.Of<IUmbrellaSharePointFileStorageProvider>(), _allow, graph, "drive", isNew, download, metadata, "remote");

	private static T Construct<T>(params object?[] args) => (T)typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
		.Single(x => x.GetParameters().Length == args.Length).Invoke(args);

	private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	private sealed class GraphHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
	}

	private sealed class TestAzureProvider : UmbrellaAzureBlobStorageFileProvider
	{
		public TestAzureProvider(BlobServiceClient service, IUmbrellaFileMetadataProvider metadata)
			: base(NullLoggerFactory.Instance, CoreUtilitiesMocks.CreateMimeTypeUtility(), MetadataTestStore.Converter, new UmbrellaFileAuthorizationHandlerRegistry([]))
		{
			InitializeOptions(new UmbrellaAzureBlobStorageFileProviderOptions
			{
				StorageConnectionString = "UseDevelopmentStorage=true", MetadataProvider = metadata, MetadataNamespace = "remote", AllowUnhandledFileAuthorizationChecks = true
			});
			ServiceClient = service;
		}
	}
}
