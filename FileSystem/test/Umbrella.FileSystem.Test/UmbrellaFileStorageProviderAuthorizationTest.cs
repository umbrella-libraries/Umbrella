using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Umbrella.FileSystem.Abstractions;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Mime.Abstractions;
using Umbrella.Utilities.TypeConverters.Abstractions;

namespace Umbrella.FileSystem.Test;

public class UmbrellaFileStorageProviderAuthorizationTest
{
	[Fact]
	public async Task CreateAsync_NewFile_UsesCreateAuthorization()
	{
		var handler = new RecordingAuthorizationHandler();
		var provider = CreateProvider([handler], allowUnhandledFileAuthorizationChecks: false);

		IUmbrellaFileInfo file = await provider.CreateAsync("/documents/test.bin", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);
		Assert.Equal([UmbrellaFileOperationType.Create], handler.Operations);
	}

	[Fact]
	public async Task GetAsync_ExistingFile_UsesReadAuthorization()
	{
		var handler = new RecordingAuthorizationHandler();
		var provider = CreateProvider([handler], allowUnhandledFileAuthorizationChecks: false);

		provider.AddExistingFile("/documents/existing.bin");

		IUmbrellaFileInfo? file = await provider.GetAsync("/documents/existing.bin", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(file);
		Assert.Equal([UmbrellaFileOperationType.Read], handler.Operations);
	}

	[Fact]
	public async Task CreateAsync_NewFile_ThrowsWhenCreateIsUnhandled()
	{
		var provider = CreateProvider([], allowUnhandledFileAuthorizationChecks: false);

		UmbrellaFileSystemException exception = await Assert
			.ThrowsAsync<UmbrellaFileSystemException>(() => provider.CreateAsync("/documents/test.bin", TestContext.Current.CancellationToken))
			.ConfigureAwait(true);

		Assert.IsType<UmbrellaFileAccessDeniedException>(exception.InnerException);
	}

	[Fact]
	public async Task WriteFromByteArrayAsync_NewFile_UsesCreateAuthorization()
	{
		var handler = new RecordingAuthorizationHandler();
		var provider = CreateProvider([handler], allowUnhandledFileAuthorizationChecks: false);

		IUmbrellaFileInfo file = await provider.CreateAsync("/documents/test.bin", TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteFromByteArrayAsync([1, 2, 3], cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Equal([UmbrellaFileOperationType.Create, UmbrellaFileOperationType.Create], handler.Operations);
		Assert.False(file.IsNew);
	}

	private static TestFileStorageProvider CreateProvider(IEnumerable<IUmbrellaFileAuthorizationHandler> authorizationHandlers, bool allowUnhandledFileAuthorizationChecks)
	{
		ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
		var provider = new TestFileStorageProvider(
			NullLogger<TestFileStorageProvider>.Instance,
			loggerFactory,
			CoreUtilitiesMocks.CreateMimeTypeUtility(("bin", "application/octet-stream")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			new UmbrellaFileAuthorizationHandlerRegistry(authorizationHandlers));

		provider.InitializeOptions(new TestFileStorageProviderOptions
		{
			AllowUnhandledFileAuthorizationChecks = allowUnhandledFileAuthorizationChecks
		});

		return provider;
	}

	private sealed class TestFileStorageProvider : UmbrellaFileStorageProvider<TestFileInfo, TestFileStorageProviderOptions>
	{
		private readonly HashSet<string> _existingSubPaths = [];

		public TestFileStorageProvider(
			ILogger logger,
			ILoggerFactory loggerFactory,
			IMimeTypeUtility mimeTypeUtility,
			IGenericTypeConverter genericTypeConverter,
			IUmbrellaFileAuthorizationHandlerRegistry authorizationHandlerRegistry)
			: base(logger, loggerFactory, mimeTypeUtility, genericTypeConverter, authorizationHandlerRegistry)
		{
		}

		public void AddExistingFile(string subpath)
			=> _existingSubPaths.Add(SanitizeSubPathCore(subpath));

		protected override async Task<IUmbrellaFileInfo?> GetFileAsync(string subpath, bool isNew, CancellationToken cancellationToken)
		{
			string cleanedSubPath = SanitizeSubPathCore(subpath);

			if (!isNew && !_existingSubPaths.Contains(cleanedSubPath))
				return null;

			var fileInfo = new TestFileInfo(cleanedSubPath, AuthorizeAsync, isNew, MarkPersisted);

			return await FinalizeResolvedFileAsync(fileInfo, subpath, cancellationToken).ConfigureAwait(false);
		}

		private void MarkPersisted(string subpath)
			=> _existingSubPaths.Add(subpath);
	}

	private sealed class TestFileInfo : IUmbrellaFileInfo
	{
		private readonly UmbrellaFileAccessAuthorizor _accessAuthorizor;
		private readonly Action<string> _markPersisted;

		public TestFileInfo(string subPath, UmbrellaFileAccessAuthorizor accessAuthorizor, bool isNew, Action<string> markPersisted)
		{
			SubPath = subPath;
			_accessAuthorizor = accessAuthorizor;
			IsNew = isNew;
			_markPersisted = markPersisted;
			Name = Path.GetFileName(subPath);
			ContentType = "application/octet-stream";
			Length = -1;
		}

		public bool IsNew { get; private set; }

		public string Name { get; }

		public string SubPath { get; }

		public long Length { get; private set; }

		public DateTimeOffset? LastModified { get; private set; }

		public string? ContentType { get; }

		public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<byte[]> ReadAsByteArrayAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task WriteToStreamAsync(Stream target, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public async Task WriteFromByteArrayAsync(byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
		{
			if (!await _accessAuthorizor(this, IsNew ? UmbrellaFileOperationType.Create : UmbrellaFileOperationType.Update, cancellationToken).ConfigureAwait(false))
				throw new UmbrellaFileAccessDeniedException(SubPath);

			Length = bytes.LongLength;
			LastModified = DateTimeOffset.UtcNow;
			IsNew = false;
			_markPersisted(SubPath);
		}

		public Task WriteFromStreamAsync(Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<Stream> ReadAsStreamAsync(int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<IUmbrellaFileInfo> CopyAsync(string destinationSubpath, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<IUmbrellaFileInfo> MoveAsync(string destinationSubpath, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task<T> GetMetadataValueAsync<T>(string key, T fallback = default!, Func<string?, T>? customValueConverter = null, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task SetMetadataValueAsync<T>(string key, T value, bool writeChanges = true, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task RemoveMetadataValueAsync(string key, bool writeChanges = true, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task ClearMetadataAsync(bool writeChanges = true, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();

		public Task WriteMetadataChangesAsync(CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();
	}

	private sealed class TestFileStorageProviderOptions : UmbrellaFileStorageProviderOptionsBase;

	private sealed class RecordingAuthorizationHandler : IUmbrellaFileAuthorizationHandler
	{
		public string DirectoryName => "documents";

		public List<UmbrellaFileOperationType> Operations { get; } = [];

		public Task<bool> AuthorizeAsync(IUmbrellaFileInfo fileInfo, UmbrellaFileOperationType operationType, CancellationToken cancellationToken = default)
		{
			Operations.Add(operationType);

			return Task.FromResult(true);
		}
	}
}
