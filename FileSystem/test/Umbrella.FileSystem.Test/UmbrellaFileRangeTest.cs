using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Dataverse;
using Umbrella.FileSystem.Disk;
using Umbrella.FileSystem.SharePoint;
using Umbrella.Internal.Mocks;
namespace Umbrella.FileSystem.Test;
public class UmbrellaFileRangeTest
{
	private static readonly byte[] _bytes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
	private static readonly UmbrellaFileAccessAuthorizor _allow = (_, _, _) => Task.FromResult(true);
	private static readonly UmbrellaFileAccessAuthorizor _deny = (_, _, _) => Task.FromResult(false);
	[Fact]
	public async Task DiskSeeksAndDisposesFileHandle()
	{
		string path = Path.GetTempFileName();
		try
		{
			await File.WriteAllBytesAsync(path, _bytes, TestContext.Current.CancellationToken);
			var file = Construct<UmbrellaDiskFileInfo>(NullLogger<UmbrellaDiskFileInfo>.Instance,
				CoreUtilitiesMocks.CreateMimeTypeUtility(), CoreUtilitiesMocks.CreateGenericTypeConverter(),
				"/video.mp4", Mock.Of<IUmbrellaDiskFileStorageProvider>(), _allow, new FileInfo(path), false);
			using (Stream range = await file.ReadRangeAsStreamAsync(7, 2, cancellationToken: TestContext.Current.CancellationToken))
				Assert.Equal(new byte[] { 7, 8 }, await ReadAllAsync(range));
			using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public async Task AzureRequestsExactRangeAndDisposesDownload()
	{
		var source = new MemoryStream([7, 8, 9]);
		var blob = new Mock<BlobClient>();
		_ = blob.Setup(x => x.DownloadStreamingAsync(It.Is<BlobDownloadOptions>(o => o.Range.Offset == 7 && o.Range.Length == 2), It.IsAny<CancellationToken>()))
			.ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobDownloadStreamingResult(content: source), Mock.Of<Response>()));
		var file = Construct<UmbrellaAzureBlobFileInfo>(NullLogger<UmbrellaAzureBlobFileInfo>.Instance,
			CoreUtilitiesMocks.CreateMimeTypeUtility(), CoreUtilitiesMocks.CreateGenericTypeConverter(),
			"/video.mp4", Mock.Of<IUmbrellaAzureBlobFileStorageProvider>(), _allow, blob.Object, false);
		typeof(UmbrellaAzureBlobFileInfo).GetField("_blobProperties", BindingFlags.NonPublic | BindingFlags.Instance)!
			.SetValue(file, BlobsModelFactory.BlobProperties(contentLength: 10));
		using (Stream range = await file.ReadRangeAsStreamAsync(7, 2, cancellationToken: TestContext.Current.CancellationToken))
			Assert.Equal(new byte[] { 7, 8 }, await ReadAllAsync(range));
		Assert.False(source.CanRead);
		blob.VerifyAll();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DataverseSlicesExistingBase64AndChecksAuthorization(bool denied)
	{
		var file = CreateDataverse(denied ? _deny : _allow);
		if (denied)
		{
			_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.ReadRangeAsStreamAsync(7, 2, cancellationToken: TestContext.Current.CancellationToken));
		}
		else
		{
			using Stream range = await file.ReadRangeAsStreamAsync(7, 2, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal(new byte[] { 7, 8 }, await ReadAllAsync(range));
		}
	}

	[Theory]
	[InlineData(-1, 1)]
	[InlineData(10, 1)]
	[InlineData(0, 0)]
	[InlineData(0, -1)]
	[InlineData(8, 3)]
	[InlineData(1, long.MaxValue)]
	public async Task ProviderRejectsInvalidRange(long offset, long length)
	{
		_ = await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => CreateDataverse(_allow).ReadRangeAsStreamAsync(offset, length, cancellationToken: TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ProviderPreservesCancellation()
	{
		using var cts = new CancellationTokenSource();
		UmbrellaFileAccessAuthorizor cancel = async (_, _, token) =>
		{
			await cts.CancelAsync();
			throw new OperationCanceledException(token);
		};
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateDataverse(cancel).ReadRangeAsStreamAsync(0, 1, cancellationToken: cts.Token));
	}

	[Theory]
	[InlineData(206, false)]
	[InlineData(200, false)]
	[InlineData(206, true)]
	public async Task SharePointRequestsDownloadUrlAndHandlesResponses(int status, bool incorrectRange)
	{
		using var graphHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent("{\"@microsoft.graph.downloadUrl\":\"https://download.example/video.mp4\"}", System.Text.Encoding.UTF8, "application/json")
		});
		using var graphHttp = new HttpClient(graphHandler, disposeHandler: false);
		using var graph = new GraphServiceClient(graphHttp, new AnonymousAuthenticationProvider());
		var source = new MemoryStream(status == 200 ? _bytes : [7, 8]);
		using var downloadHandler = new StubHandler(request =>
		{
			Assert.Equal("https://download.example/video.mp4", request.RequestUri!.AbsoluteUri);
			Assert.Null(request.Headers.Authorization);
			Assert.Equal("bytes=7-8", request.Headers.Range!.ToString());
			var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StreamContent(source) };
			if (status == 206)
			{
				response.Content.Headers.ContentRange = new ContentRangeHeaderValue(incorrectRange ? 6 : 7, 8, 10);
			}

			return response;
		});
		using var download = new HttpClient(downloadHandler, disposeHandler: false);
		var file = Construct<UmbrellaSharePointFileInfo>(NullLogger<UmbrellaSharePointFileInfo>.Instance,
			CoreUtilitiesMocks.CreateMimeTypeUtility(), CoreUtilitiesMocks.CreateGenericTypeConverter(),
			"/video.mp4", "video.mp4", Mock.Of<IUmbrellaSharePointFileStorageProvider>(), _allow, graph, "drive", false, download);
		await (Task)typeof(UmbrellaSharePointFileInfo).GetMethod("InitializeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(file, [TestContext.Current.CancellationToken, new DriveItem { Size = 10 }])!;
		if (incorrectRange)
		{
			_ = await Assert.ThrowsAsync<UmbrellaFileSystemException>(() => file.ReadRangeAsStreamAsync(7, 2, cancellationToken: TestContext.Current.CancellationToken));
		}
		else
		{
			using Stream range = await file.ReadRangeAsStreamAsync(7, 2, bufferSizeOverride: 2, cancellationToken: TestContext.Current.CancellationToken);
			Assert.Equal(new byte[] { 7, 8 }, await ReadAllAsync(range));
		}

		Assert.False(source.CanRead);
	}

	[Fact]
	public async Task BoundedStreamRejectsTruncationAndHonorsCancellation()
	{
		using var source = new MemoryStream([1]);
		using var range = new UmbrellaFileRangeStream(source, 2);
		byte[] buffer = new byte[10];
		Assert.Equal(1, await range.ReadAsync(buffer, TestContext.Current.CancellationToken));
		_ = await Assert.ThrowsAsync<EndOfStreamException>(() => range.ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask());
		_ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => range.ReadAsync(buffer, new CancellationToken(true)).AsTask());
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task ProviderRejectsInvalidBufferSize(int bufferSize)
	{
		_ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateDataverse(_allow).ReadRangeAsStreamAsync(0, 1, bufferSize, TestContext.Current.CancellationToken));
	}

	[Fact]
	public void BoundedStreamStopsAtRangeEndAndOwnsResources()
	{
		var source = new MemoryStream([1, 2, 3]);
		var owner = new Mock<IDisposable>();
		using (var range = new UmbrellaFileRangeStream(source, 2, owner.Object))
		{
			byte[] buffer = new byte[10];
			Assert.Equal(2, range.Read(buffer, 0, 10));
			Assert.Equal(0, range.Read(buffer, 0, 10));
			Assert.Equal(2, source.Position);
			Assert.Equal(new byte[] { 1, 2 }, buffer.Take(2));
		}

		Assert.False(source.CanRead);
		owner.Verify(x => x.Dispose(), Times.Once);
	}

	private static UmbrellaDataverseFileInfo CreateDataverse(UmbrellaFileAccessAuthorizor authorizor)
	{
		var file = Construct<UmbrellaDataverseFileInfo>(NullLogger<UmbrellaDataverseFileInfo>.Instance,
			CoreUtilitiesMocks.CreateGenericTypeConverter(), "/video.mp4", "video.mp4", new UmbrellaDataverseFileStorageProviderOptions(), authorizor, Guid.NewGuid(), false);
		_ = typeof(UmbrellaDataverseFileInfo).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(file, [Convert.ToBase64String(_bytes), null, "video/mp4", null]);
		return file;
	}

	private static T Construct<T>(params object[] args)
	{
		object?[] constructorArgs = [.. args, new UmbrellaUnsupportedFileMetadataProvider(), null];
		return (T)typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
			.Single(x => x.GetParameters().Length == constructorArgs.Length).Invoke(constructorArgs);
	}
	private static async Task<byte[]> ReadAllAsync(Stream source)
	{
		using var target = new MemoryStream();
		await source.CopyToAsync(target, TestContext.Current.CancellationToken);
		return target.ToArray();
	}

	private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			=> Task.FromResult(respond(request));
	}
}