using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Xunit;

namespace Umbrella.FileSystem.Test;

public class UmbrellaFileHandlerTest
{
	[Fact]
	public async Task GetVersionTokenAsync_FromSubpath_UsesProviderAndMetadataToken()
	{
		DateTimeOffset lastModified = new(2025, 5, 13, 10, 15, 30, TimeSpan.Zero);
		const long length = 1234;

		var fileInfoMock = CreateFileInfoMock("/documents/test.bin", lastModified, length);
		var providerMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = providerMock.Setup(x => x.GetAsync("/documents/test.bin", TestContext.Current.CancellationToken)).ReturnsAsync(fileInfoMock.Object);

		var handler = CreateHandler(providerMock.Object);

		string? token = await handler.GetVersionTokenAsync("/documents/test.bin", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Equal(CreateMetadataVersionToken(lastModified, length), token);
		providerMock.Verify(x => x.GetAsync("/documents/test.bin", TestContext.Current.CancellationToken), Times.Once);
		fileInfoMock.Verify(x => x.ExistsAsync(It.IsAny<CancellationToken>()), Times.Never);
		fileInfoMock.Verify(x => x.ReadAsStreamAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetVersionedWebFilePathAsync_UsesResolvedFileNameAndMetadataToken()
	{
		DateTimeOffset lastModified = new(2025, 5, 13, 10, 15, 30, TimeSpan.Zero);
		const long length = 1234;

		var fileInfoMock = CreateFileInfoMock("/documents/test.bin", lastModified, length, fileName: "Resolved-Name.bin");
		var providerMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = providerMock
			.Setup(x => x.GetAsync("/documents/test.bin", TestContext.Current.CancellationToken))
			.ReturnsAsync(fileInfoMock.Object);

		var handler = CreateHandler(providerMock.Object);

		UmbrellaVersionedUrl? result = await handler
			.GetVersionedWebFilePathAsync(default, "test.bin", TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		Assert.NotNull(result);
		Assert.Equal("/files/documents/resolved-name.bin", result.Value.Url);
		Assert.Equal(CreateMetadataVersionToken(lastModified, length), result.Value.VersionToken);
		providerMock.Verify(x => x.GetAsync("/documents/test.bin", TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task GetVersionTokenAsync_FromFileInfo_UsesMetadataTokenWhenAvailable()
	{
		DateTimeOffset lastModified = new(2025, 5, 13, 10, 15, 30, TimeSpan.Zero);
		const long length = 4321;

		var fileInfoMock = CreateFileInfoMock("/documents/test.bin", lastModified, length);
		var handler = CreateHandler();

		string? token = await handler.GetVersionTokenAsync(fileInfoMock.Object, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Equal(CreateMetadataVersionToken(lastModified, length), token);
		fileInfoMock.Verify(x => x.ExistsAsync(It.IsAny<CancellationToken>()), Times.Never);
		fileInfoMock.Verify(x => x.ReadAsStreamAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetVersionTokenAsync_FromFileInfo_FallsBackToContentHashWhenLastModifiedMissing()
	{
		byte[] bytes = [1, 2, 3, 4, 5];

		var fileInfoMock = CreateFileInfoMock("/documents/test.bin", null, bytes.LongLength);
		_ = fileInfoMock.Setup(x => x.ExistsAsync(TestContext.Current.CancellationToken)).ReturnsAsync(true);
		_ = fileInfoMock
			.Setup(x => x.ReadAsStreamAsync(null, TestContext.Current.CancellationToken))
			.Returns(() => Task.FromResult<Stream>(new MemoryStream(bytes)));

		var handler = CreateHandler();

		string? token = await handler.GetVersionTokenAsync(fileInfoMock.Object, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Equal(CreateHashVersionToken(bytes), token);
		fileInfoMock.Verify(x => x.ExistsAsync(TestContext.Current.CancellationToken), Times.Once);
		fileInfoMock.Verify(x => x.ReadAsStreamAsync(null, TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task GetVersionTokenAsync_FromSubpath_ReturnsNullWhenFileDoesNotExist()
	{
		var providerMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = providerMock.Setup(x => x.GetAsync("/documents/missing.bin", TestContext.Current.CancellationToken)).ReturnsAsync((IUmbrellaFileInfo?)null);

		var handler = CreateHandler(providerMock.Object);

		string? token = await handler.GetVersionTokenAsync("/documents/missing.bin", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Null(token);
		providerMock.Verify(x => x.GetAsync("/documents/missing.bin", TestContext.Current.CancellationToken), Times.Once);
	}

	[Fact]
	public async Task GetVersionTokenAsync_FromFileInfo_ReturnsNullWhenFileDoesNotExist()
	{
		var fileInfoMock = CreateFileInfoMock("/documents/missing.bin", null, -1);
		_ = fileInfoMock.Setup(x => x.ExistsAsync(TestContext.Current.CancellationToken)).ReturnsAsync(false);

		var handler = CreateHandler();

		string? token = await handler.GetVersionTokenAsync(fileInfoMock.Object, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Null(token);
		fileInfoMock.Verify(x => x.ExistsAsync(TestContext.Current.CancellationToken), Times.Once);
		fileInfoMock.Verify(x => x.ReadAsStreamAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task GetVersionedWebFilePathAsync_FallsBackToContentHashWhenLastModifiedMissing()
	{
		byte[] bytes = [1, 2, 3, 4, 5];

		var fileInfoMock = CreateFileInfoMock("/documents/test.bin", null, bytes.LongLength);
		_ = fileInfoMock.Setup(x => x.ExistsAsync(TestContext.Current.CancellationToken)).ReturnsAsync(true);
		_ = fileInfoMock
			.Setup(x => x.ReadAsStreamAsync(null, TestContext.Current.CancellationToken))
			.Returns(() => Task.FromResult<Stream>(new MemoryStream(bytes)));

		var providerMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = providerMock
			.Setup(x => x.GetAsync("/documents/test.bin", TestContext.Current.CancellationToken))
			.ReturnsAsync(fileInfoMock.Object);

		var handler = CreateHandler(providerMock.Object);

		UmbrellaVersionedUrl? result = await handler
			.GetVersionedWebFilePathAsync(default, "test.bin", TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		Assert.NotNull(result);
		Assert.Equal("/files/documents/test.bin", result.Value.Url);
		Assert.Equal(CreateHashVersionToken(bytes), result.Value.VersionToken);
	}

	[Fact]
	public async Task GetVersionedWebFilePathAsync_ReturnsNullWhenFileDoesNotExist()
	{
		var providerMock = new Mock<IUmbrellaFileStorageProvider>();
		_ = providerMock
			.Setup(x => x.GetAsync("/documents/missing.bin", TestContext.Current.CancellationToken))
			.ReturnsAsync((IUmbrellaFileInfo?)null);

		var handler = CreateHandler(providerMock.Object);

		UmbrellaVersionedUrl? result = await handler
			.GetVersionedWebFilePathAsync(default, "missing.bin", TestContext.Current.CancellationToken)
			.ConfigureAwait(true);

		Assert.Null(result);
	}

	private static TestFileHandler CreateHandler(IUmbrellaFileStorageProvider? fileProvider = null)
	{
		var optionsMock = new Mock<IUmbrellaFileStorageProviderOptions>();
		_ = optionsMock.SetupGet(x => x.TempFilesDirectoryName).Returns("temp-files");
		_ = optionsMock.SetupGet(x => x.WebFilesDirectoryName).Returns("files");

		return new TestFileHandler(
			new Mock<ILogger>().Object,
			CoreUtilitiesMocks.CreateHybridCache(),
			CoreUtilitiesMocks.CreateCacheKeyUtility(),
			fileProvider ?? new Mock<IUmbrellaFileStorageProvider>().Object,
			optionsMock.Object);
	}

	private static Mock<IUmbrellaFileInfo> CreateFileInfoMock(string subpath, DateTimeOffset? lastModified, long length, string? fileName = null)
	{
		var fileInfoMock = new Mock<IUmbrellaFileInfo>();
		_ = fileInfoMock.SetupGet(x => x.IsNew).Returns(false);
		_ = fileInfoMock.SetupGet(x => x.Name).Returns(fileName ?? Path.GetFileName(subpath));
		_ = fileInfoMock.SetupGet(x => x.SubPath).Returns(subpath);
		_ = fileInfoMock.SetupGet(x => x.Length).Returns(length);
		_ = fileInfoMock.SetupGet(x => x.LastModified).Returns(lastModified);

		return fileInfoMock;
	}

	private static string CreateMetadataVersionToken(DateTimeOffset lastModified, long length)
		=> Convert.ToString(lastModified.UtcDateTime.ToFileTimeUtc() ^ length, 16);

	private static string CreateHashVersionToken(byte[] bytes)
		=> Convert.ToHexStringLower(SHA256.HashData(bytes));

	private sealed class TestFileHandler : UmbrellaFileHandler
	{
		public TestFileHandler(
			ILogger logger,
			Umbrella.Utilities.Caching.Abstractions.IHybridCache cache,
			Umbrella.Utilities.Caching.Abstractions.ICacheKeyUtility cacheKeyUtility,
			IUmbrellaFileStorageProvider fileProvider,
			IUmbrellaFileStorageProviderOptions options)
			: base(logger, cache, cacheKeyUtility, fileProvider, options)
		{
		}

		public override string DirectoryName => "documents";

		public override Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);
	}
}
