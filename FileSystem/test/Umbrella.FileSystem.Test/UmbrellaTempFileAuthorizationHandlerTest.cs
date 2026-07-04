using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.Test;

public class UmbrellaTempFileAuthorizationHandlerTest
{
	[Fact]
	public async Task AuthorizeAsync_CreateNewFile_ReturnsTrueWithoutReadingMetadata()
	{
		var fileInfoMock = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = fileInfoMock.SetupGet(x => x.IsNew).Returns(true);
		_ = fileInfoMock.SetupGet(x => x.Name).Returns("upload.bin");

		var handler = CreateHandler();

		bool result = await handler.AuthorizeAsync(fileInfoMock.Object, UmbrellaFileOperationType.Create, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(result);
		fileInfoMock.Verify(x => x.GetMetadataValueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string?, string>>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task AuthorizeAsync_ExistingFileCreatedByCurrentUser_ReturnsTrue()
	{
		var originalPrincipal = Thread.CurrentPrincipal as ClaimsPrincipal;
		Thread.CurrentPrincipal = CreatePrincipal("user-1");

		try
		{
			var fileInfo = CreateExistingFileInfo("user-1");
			var handler = CreateHandler();

			bool result = await handler.AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Delete, TestContext.Current.CancellationToken).ConfigureAwait(true);

			Assert.True(result);
		}
		finally
		{
			Thread.CurrentPrincipal = originalPrincipal;
		}
	}

	[Fact]
	public async Task AuthorizeAsync_ExistingFileCreatedByDifferentUser_ReturnsFalse()
	{
		var originalPrincipal = Thread.CurrentPrincipal as ClaimsPrincipal;
		Thread.CurrentPrincipal = CreatePrincipal("user-2");

		try
		{
			var fileInfo = CreateExistingFileInfo("user-1");
			var handler = CreateHandler();

			bool result = await handler.AuthorizeAsync(fileInfo, UmbrellaFileOperationType.Delete, TestContext.Current.CancellationToken).ConfigureAwait(true);

			Assert.False(result);
		}
		finally
		{
			Thread.CurrentPrincipal = originalPrincipal;
		}
	}

	private static UmbrellaTempFileAuthorizationHandler CreateHandler()
		=> new(
			NullLogger<UmbrellaTempFileAuthorizationHandler>.Instance,
			new TestFileStorageProviderOptions());

	private static IUmbrellaFileInfo CreateExistingFileInfo(string createdById)
	{
		var fileInfoMock = new Mock<IUmbrellaFileInfo>(MockBehavior.Strict);
		_ = fileInfoMock.SetupGet(x => x.IsNew).Returns(false);
		_ = fileInfoMock.SetupGet(x => x.Name).Returns("upload.bin");
		_ = fileInfoMock
			.Setup(x => x.GetMetadataValueAsync(
				UmbrellaFileSystemConstants.CreatedByIdMetadataKey,
				It.IsAny<string>(),
				It.IsAny<Func<string?, string>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(createdById);

		return fileInfoMock.Object;
	}

	private static ClaimsPrincipal CreatePrincipal(string userId)
		=> new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

	private sealed class TestFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase;
}
