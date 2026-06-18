using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.Test;

public class UmbrellaFileAuthorizationHandlerRegistryTest
{
	[Fact]
	public void GetByFileInfo_ResolvesHandlerFromTopLevelDirectory()
	{
		var registry = new UmbrellaFileAuthorizationHandlerRegistry(
		[
			new DocumentsAuthorizationHandler()
		]);
		IUmbrellaFileAuthorizationHandler? handler = registry.GetByFileInfo(CreateFileInfo("/documents/sub-folder/file.bin"));

		Assert.NotNull(handler);
		Assert.IsType<DocumentsAuthorizationHandler>(handler);
	}

	[Fact]
	public void GetByFileInfo_ReturnsNullWhenNoHandlerExists()
	{
		var registry = new UmbrellaFileAuthorizationHandlerRegistry(
		[
			new ImagesAuthorizationHandler()
		]);
		IUmbrellaFileAuthorizationHandler? handler = registry.GetByFileInfo(CreateFileInfo("/documents/file.bin"));

		Assert.Null(handler);
	}

	[Fact]
	public void Constructor_ThrowsWhenDirectoryNamesNormalizeToSameValue()
	{
		Exception exception = Assert.ThrowsAny<Exception>(() => new UmbrellaFileAuthorizationHandlerRegistry(
		[
			new DocumentsAuthorizationHandler(),
			new DuplicateDocumentsAuthorizationHandler()
		]));

		Assert.Contains("documents", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void AddUmbrellaFileSystemCore_WhenCalledTwice_ResolvesBuiltInTempHandler()
	{
		var options = new TestFileStorageProviderOptions();
		var services = CreateServices(options);

		_ = services.AddUmbrellaFileSystemCore();
		_ = services.AddUmbrellaFileSystemCore();

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IUmbrellaFileAuthorizationHandlerRegistry registry = serviceProvider.GetRequiredService<IUmbrellaFileAuthorizationHandlerRegistry>();

		IUmbrellaFileAuthorizationHandler? expectedHandler = registry.GetByDirectoryName(options.TempFilesDirectoryName);
		IUmbrellaFileAuthorizationHandler? actualHandler = registry.GetByFileInfo(CreateFileInfo($"/{options.TempFilesDirectoryName}/upload.bin"));

		Assert.NotNull(expectedHandler);
		Assert.IsType<UmbrellaTempFileAuthorizationHandler>(expectedHandler);
		Assert.Same(expectedHandler, actualHandler);
	}

	private static ServiceCollection CreateServices(IUmbrellaFileStorageProviderOptions options)
	{
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddSingleton<IUmbrellaFileStorageProviderOptions>(options);

		return services;
	}

	private static IUmbrellaFileInfo CreateFileInfo(string subPath)
	{
		var fileInfoMock = new Mock<IUmbrellaFileInfo>();
		_ = fileInfoMock.SetupGet(x => x.SubPath).Returns(subPath);

		return fileInfoMock.Object;
	}

	private sealed class TestFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase;

	private abstract class TestAuthorizationHandlerBase : IUmbrellaFileAuthorizationHandler
	{
		public abstract string DirectoryName { get; }

		public Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);
	}

	private sealed class DocumentsAuthorizationHandler : TestAuthorizationHandlerBase
	{
		public override string DirectoryName => "Documents";
	}

	private sealed class DuplicateDocumentsAuthorizationHandler : TestAuthorizationHandlerBase
	{
		public override string DirectoryName => "documents";
	}

	private sealed class ImagesAuthorizationHandler : TestAuthorizationHandlerBase
	{
		public override string DirectoryName => "images";
	}
}
