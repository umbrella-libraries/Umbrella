
using System.Collections.ObjectModel;
using Azure.Identity;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Disk;
using Umbrella.FileSystem.SharePoint;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Helpers;
using Xunit;
using Xunit.v3.Priority;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
//[assembly: TestCaseOrderer(typeof(Xunit.v3.Priority.PriorityOrderer))]
//[assembly: TestCollectionOrderer("Xunit.Extensions.Ordering.CollectionOrderer", "Xunit.Extensions.Ordering")]
//[assembly: TestFramework("Xunit.Extensions.Ordering.TestFramework", "Xunit.Extensions.Ordering")]

namespace Umbrella.FileSystem.Test;

public class UmbrellaFileProviderTest
{
#if AZUREDEVOPS
        private static readonly string _storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString")!;
#else
#pragma warning disable CA1802 // Use literals where appropriate
	private static readonly string _storageConnectionString = "UseDevelopmentStorage=true";
#pragma warning restore CA1802 // Use literals where appropriate
#endif

	private static readonly IConfiguration _sharePointConfig = new ConfigurationBuilder()
		.AddUserSecrets<UmbrellaFileProviderTest>()
		.AddEnvironmentVariables()
		.Build();

	private static string SharePointTenantId => _sharePointConfig["SharePoint:TenantId"]!;
	private static string SharePointClientId => _sharePointConfig["SharePoint:ClientId"]!;
	private static string SharePointClientSecret => _sharePointConfig["SharePoint:ClientSecret"]!;

	private const string TestFileName = "aspnet-mvc-logo.png";
	private static string? _baseDirectory;

	private static string BaseDirectory
	{
		get
		{
			if (string.IsNullOrEmpty(_baseDirectory))
			{
				string baseDirectory = AppContext.BaseDirectory.ToLowerInvariant();
				int indexToEndAt = baseDirectory.IndexOf(PathHelper.PlatformNormalize($@"\bin\{DebugUtility.BuildConfiguration}\net10.0"), StringComparison.Ordinal);
				_baseDirectory = baseDirectory.Remove(indexToEndAt, baseDirectory.Length - indexToEndAt);
			}

			return _baseDirectory;
		}
	}

	public static List<Func<IUmbrellaFileStorageProvider>> Providers =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider,
		CreateSharePointFileProvider
	];

	// SharePoint restricts % in path segments; exclude from path-variation tests to avoid false failures
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _pathVariationProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider
	];

	// SharePoint throws NotSupportedException for metadata operations
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _metadataCapableProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider
	];

	public static List<string> PathsToTest =
	[
		$"~/images/{TestFileName}",
		$"/images/{TestFileName}",
		$@"\images\{TestFileName}",
		$@"\images/{TestFileName}",
		$@"\images\\\\\\subbie\\\\{TestFileName}",
		$"/images/subfolder1/sub2/{TestFileName}",
		$"/images//////subfolder1/////sub2/{TestFileName}",
		$"/images/subfolder1/su345  __---!!^^%b2/{TestFileName}",
		$"/images/subfolder1/sub   2/{TestFileName}"
	];

	public static List<object[]> ProvidersMemberData = Providers.Select(x => new object[] { x }).ToList();
	public static List<object[]> MetadataCapableProvidersMemberData = _metadataCapableProviders.Select(x => new object[] { x }).ToList();
	public static List<object[]> PathsToTestMemberData = PathsToTest.Select(x => new object[] { x }).ToList();

	public static Collection<object[]> ProvidersAndPathsMemberData = [];

	static UmbrellaFileProviderTest()
	{
		foreach (var provider in _pathVariationProviders)
		{
			foreach (string path in PathsToTest)
			{
				ProvidersAndPathsMemberData.Add([provider, path]);
			}
		}
	}

	[Theory]
	[MemberData(nameof(ProvidersAndPathsMemberData))]
	public async Task CreateAsync_FromPathAsync(Func<IUmbrellaFileStorageProvider> providerFunc, string path)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		IUmbrellaFileInfo file = await provider.CreateAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckPOCOFileType(provider, file);
		Assert.Equal(-1, file.Length);
		Assert.Null(file.LastModified);
		Assert.Equal(TestFileName, file.Name);
		Assert.Equal("image/png", file.ContentType);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_FromVirtualPath_Write_DeleteFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		IUmbrellaFileInfo file = await provider.CreateAsync($"~/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersAndPathsMemberData))]
	public async Task CreateAsync_Write_ReadBytes_DeleteFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc, string path)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Create the file
		IUmbrellaFileInfo file = await provider.CreateAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		bytes = await file.ReadAsByteArrayAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_GetAsync_ReadBytes_DeleteFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		IUmbrellaFileInfo file = await provider.CreateAsync($"/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Get the file
		IUmbrellaFileInfo? retrievedFile = await provider.GetAsync($"/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(retrievedFile);

		CheckWrittenFileAssertions(provider, retrievedFile!, bytes.Length, TestFileName);

		_ = await file.ReadAsByteArrayAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		Assert.Equal(bytes.Length, retrievedFile!.Length);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_GetAsync_ReadBytes_DeleteFile_CasingMismatchAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		IUmbrellaFileInfo file = await provider.CreateAsync($"/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		// Get the file but with a different casing
		IUmbrellaFileInfo? retrievedFile = await provider.GetAsync($"/images/{TestFileName.ToUpperInvariant()}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(retrievedFile);

		CheckWrittenFileAssertions(provider, retrievedFile!, bytes.Length, TestFileName);

		_ = await file.ReadAsByteArrayAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		Assert.Equal(bytes.Length, retrievedFile!.Length);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersAndPathsMemberData))]
	public async Task CreateAsync_Write_ReadStream_DeleteFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc, string path)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Create the file
		IUmbrellaFileInfo file = await provider.CreateAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		using (var ms = new MemoryStream())
		{
			await file.WriteToStreamAsync(ms, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
			bytes = ms.ToArray();
		}

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_GetAsync_ReadStream_DeleteFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		IUmbrellaFileInfo file = await provider.CreateAsync($"/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Get the file
		IUmbrellaFileInfo? retrievedFile = await provider.GetAsync($"/images/{TestFileName}", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(retrievedFile);

		CheckWrittenFileAssertions(provider, retrievedFile!, bytes.Length, TestFileName);

		byte[] retrievedBytes;

		using (var ms = new MemoryStream())
		{
			await file.WriteToStreamAsync(ms, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
			retrievedBytes = ms.ToArray();
		}

		Assert.Equal(bytes.Length, retrievedBytes.Length);

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task GetAsync_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		IUmbrellaFileInfo? retrievedFile = await provider.GetAsync($"/images/doesnotexist.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Null(retrievedFile);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_GetAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string path = "/images/createbutnowrite.jpg";
		var file = await provider.CreateAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		IUmbrellaFileInfo? reloadedFile = await provider.GetAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Should fail as not writing to the file won't push it to blob storage
		Assert.Null(reloadedFile);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_ExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string path = "/images/createbutnowrite.jpg";
		var file = await provider.CreateAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		bool exists = await provider.ExistsAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Should be false as not calling write shouldn't create anything
		Assert.False(exists);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_ExistsAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		bool exists = await provider.ExistsAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(exists);

		// Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task SaveAsyncBytes_GetAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo fileInfo = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

		IUmbrellaFileInfo? reloadedFileInfo = await provider.GetAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(reloadedFileInfo);

		CheckWrittenFileAssertions(provider, reloadedFileInfo!, bytes.Length, TestFileName);

		//Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task SaveAsyncStream_GetAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		using Stream stream = File.OpenRead(physicalPath);
		stream.Position = 20;

		string subpath = $"/images/{TestFileName}";

		var fileInfo = await provider.SaveAsync(subpath, stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, fileInfo, (int)stream.Length, TestFileName);

		IUmbrellaFileInfo? reloadedFileInfo = await provider.GetAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(reloadedFileInfo);

		CheckWrittenFileAssertions(provider, reloadedFileInfo!, (int)stream.Length, TestFileName);

		//Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task SaveAsyncBytes_ExistsAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		var fileInfo = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

		bool exists = await provider.ExistsAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(exists);

		//Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task SaveAsyncStream_ExistsAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		using Stream stream = File.OpenRead(physicalPath);
		stream.Position = 20;

		string subpath = $"/images/{TestFileName}";

		var fileInfo = await provider.SaveAsync(subpath, stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, fileInfo, (int)stream.Length, TestFileName);

		bool exists = await provider.ExistsAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(exists);

		//Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	#region Copy
	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CopyAsync_FromPath_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			_ = await provider.CopyAsync("~/images/notexists.jpg", "~/images/willfail.png", CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CopyAsync_FromFileBytes_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc) =>
		//Should be a file system exception with a file not found exception inside
		await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

			byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

			_ = await provider.DeleteAsync(fileInfo, TestContext.Current.CancellationToken).ConfigureAwait(true);

			//At this point the file will not exist
			_ = await provider.CopyAsync(fileInfo, "~/images/willfail.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CopyAsync_FromFileStream_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc) =>
		//Should be a file system exception with a file not found exception inside
		await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

			Stream stream = File.OpenRead(physicalPath);
			stream.Position = 20;

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

			_ = await provider.DeleteAsync(fileInfo, TestContext.Current.CancellationToken).ConfigureAwait(true);

			//At this point the file will not exist
			_ = await provider.CopyAsync(fileInfo, "~/images/willfail.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromPath_ToPathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var copy = await provider.CopyAsync(subpath, "/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromFile_ToPathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var copy = await provider.CopyAsync(file, "/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromFile_ToFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Create the copy file
		var copy = await provider.CreateAsync("/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Act
		_ = await provider.CopyAsync(file, copy, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromPath_ToPath_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var copy = await provider.CopyAsync(subpath, "/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromFile_ToPath_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var copy = await provider.CopyAsync(file, "/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_CopyAsync_FromFile_ToFile_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Create the copy file
		var copy = await provider.CreateAsync("/images/xx/copy.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Act
		_ = await provider.CopyAsync(file, copy, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, copy.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, copy, bytes.Length, "copy.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(copy, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_CopyAsync_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			// Should fail because you can't copy a new file
			var fileInfo = await provider.CreateAsync("~/images/testimage.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);

			var copy = await provider.CopyAsync(fileInfo, "~/images/copy.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);
	#endregion

	#region Move
	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task MoveAsync_FromPath_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			_ = await provider.MoveAsync("~/images/notexists.jpg", "~/images/willfail.png", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task MoveAsync_FromFileBytes_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc) =>
		//Should be a file system exception with a file not found exception inside
		await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

			byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

			_ = await provider.DeleteAsync(fileInfo, TestContext.Current.CancellationToken).ConfigureAwait(true);

			//At this point the file will not exist
			_ = await provider.MoveAsync(fileInfo, "~/images/willfail.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task MoveAsync_FromFileStream_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc) =>
		//Should be a file system exception with a file not found exception inside
		await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

			Stream stream = File.OpenRead(physicalPath);
			stream.Position = 20;

			string subpath = $"/images/{TestFileName}";

			var fileInfo = await provider.SaveAsync(subpath, stream, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

			_ = await provider.DeleteAsync(fileInfo, TestContext.Current.CancellationToken).ConfigureAwait(true);

			//At this point the file will not exist
			_ = await provider.MoveAsync(fileInfo, "~/images/willfail.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromPath_ToPathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var move = await provider.MoveAsync(subpath, "/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromFile_ToPathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var move = await provider.MoveAsync(file, "/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromFile_ToFileAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Create the move file
		var move = await provider.CreateAsync("/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Act
		_ = await provider.MoveAsync(file, move, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromPath_ToPath_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var move = await provider.MoveAsync(subpath, "/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromFile_ToPath_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		//Act
		var move = await provider.MoveAsync(file, "/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task CreateAsync_Write_MoveAsync_FromFile_ToFile_WithMetadataAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Create the move file
		var move = await provider.CreateAsync("/images/xx/move.png", TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Act
		_ = await provider.MoveAsync(file, move, TestContext.Current.CancellationToken).ConfigureAwait(true);

		//Assert
		Assert.Equal(bytes.Length, move.Length);
		Assert.Equal("Magic", await file.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Man", await file.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		CheckWrittenFileAssertions(provider, move, bytes.Length, "move.png");

		//Cleanup
		_ = await provider.DeleteAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(move, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_MoveAsync_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			// Should fail because you can't move a new file
			var fileInfo = await provider.CreateAsync("~/images/testimage.jpg", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

			var move = await provider.MoveAsync(fileInfo, "~/images/move.jpg", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);
	#endregion

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task DeleteAsync_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Should fail silently
		bool deleted = await provider.DeleteAsync("/images/notexists.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task Set_Get_MetadataValueAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		// Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, file, bytes.Length, TestFileName);

		// Act
		await file.SetMetadataValueAsync("FirstName", "Richard", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("LastName", "Edwards", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Assert
		IUmbrellaFileInfo? savedFile = await provider.GetAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(savedFile);
		Assert.False(savedFile!.IsNew);
		Assert.Equal("Richard", await file.GetMetadataValueAsync<string>("FirstName", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.Equal("Edwards", await file.GetMetadataValueAsync<string>("LastName", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true));

		// Cleanup
		_ = await provider.DeleteAsync(savedFile, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[Priority(100)]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task Create_DeleteDirectory_TopLevelAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		// Create a top level file at the root of the directory.
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/tempfolder/{TestFileName}";
		_ = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		await provider.DeleteDirectoryAsync("/tempfolder", TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Assert
		Assert.False(await provider.ExistsAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true));
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task Create_DeleteDirectory_DownLevelAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		// Create a top level file at the root of the directory.
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";
		_ = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Now create another 2 file in a nested subdirectories
		string downLevelSubPath1 = $"/images/sub-images/{TestFileName}";
		_ = await provider.SaveAsync(downLevelSubPath1, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		string downLevelSubPath2 = $"/images/sub-images/nested/{TestFileName}";
		_ = await provider.SaveAsync(downLevelSubPath2, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		string downLevelSubPath3 = $"/images/sub-images/nested2/{TestFileName}";
		_ = await provider.SaveAsync(downLevelSubPath3, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		string downLevelSubPath4 = $"/images/sub-images/nested2/nestedmore/{TestFileName}";
		_ = await provider.SaveAsync(downLevelSubPath4, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Now delete only the down level file directory, i.e. /images/sub-images
		// which should also delete the nested directory
		await provider.DeleteDirectoryAsync("/images/sub-images", TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Assert
		Assert.True(await provider.ExistsAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.False(await provider.ExistsAsync(downLevelSubPath1, TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.False(await provider.ExistsAsync(downLevelSubPath2, TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.False(await provider.ExistsAsync(downLevelSubPath3, TestContext.Current.CancellationToken).ConfigureAwait(true));
		Assert.False(await provider.ExistsAsync(downLevelSubPath4, TestContext.Current.CancellationToken).ConfigureAwait(true));

		// Cleanup
		_ = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task Create_EnumerateDirectoryAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		// Create a top level file at the root of the directory.
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		string subpath = $"/images/{TestFileName}";
		_ = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Now create another 2 files in a subdirectory
		string downLevelSubPath1 = $"/images/sub-images/{TestFileName}";
		_ = await provider.SaveAsync(downLevelSubPath1, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		string downLevelSubPath2 = $"/images/sub-images/the-other-file.png";
		_ = await provider.SaveAsync(downLevelSubPath2, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Now enumerate the files
		var topLevelResults = await provider.EnumerateDirectoryAsync("/images", TestContext.Current.CancellationToken).ConfigureAwait(true);
		var downLevelResults = await provider.EnumerateDirectoryAsync("/images/sub-images", TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Assert
		_ = Assert.Single(topLevelResults);
		Assert.Equal(2, downLevelResults.Count);

		Assert.Equal(subpath, topLevelResults.ElementAt(0).SubPath);
		Assert.Equal(downLevelSubPath1, downLevelResults.ElementAt(0).SubPath);
		Assert.Equal(downLevelSubPath2, downLevelResults.ElementAt(1).SubPath);

		// Cleanup
		_ = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(downLevelSubPath1, TestContext.Current.CancellationToken).ConfigureAwait(true);
		_ = await provider.DeleteAsync(downLevelSubPath2, TestContext.Current.CancellationToken).ConfigureAwait(true);
	}

	[Theory]
	[MemberData(nameof(MetadataCapableProvidersMemberData))]
	public async Task Create_WriteMetaDataValue_Reload_WriteMetaDataWithoutLoadingAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();

		//Arrange
		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo file = await provider.CreateAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(file.IsNew);

		await file.WriteFromByteArrayAsync(bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Write some metadata
		await file.SetMetadataValueAsync("Name", "Magic", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.SetMetadataValueAsync("Description", "Man", writeChanges: false, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		await file.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		// Reload the file
		IUmbrellaFileInfo? reloadedFile = await provider.GetAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(reloadedFile);

		// Write without loading first
		await reloadedFile!.WriteMetadataChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

		string metaName = await reloadedFile.GetMetadataValueAsync<string>("Name", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
		string metaDescription = await reloadedFile.GetMetadataValueAsync<string>("Description", cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Equal("Magic", metaName);
		Assert.Equal("Man", metaDescription);
	}

	private static UmbrellaAzureBlobStorageFileProvider CreateAzureBlobFileProvider()
	{
		var options = new UmbrellaAzureBlobStorageFileProviderOptions
		{
			StorageConnectionString = _storageConnectionString,
			AllowUnhandledFileAuthorizationChecks = true
		};

#pragma warning disable CA2000 // Dispose objects before losing scope
		var provider = new UmbrellaAzureBlobStorageFileProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaAzureBlobStorageFileProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg,", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());
#pragma warning restore CA2000 // Dispose objects before losing scope

		provider.InitializeOptions(options);

		return provider;
	}

	private static UmbrellaDiskFileStorageProvider CreateDiskFileProvider()
	{
		var options = new UmbrellaDiskFileStorageProviderOptions
		{
			RootPhysicalPath = BaseDirectory,
			AllowUnhandledFileAuthorizationChecks = true
		};

#pragma warning disable CA2000 // Dispose objects before losing scope
		var provider = new UmbrellaDiskFileStorageProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaDiskFileStorageProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());
#pragma warning restore CA2000 // Dispose objects before losing scope

		provider.InitializeOptions(options);

		return provider;
	}

	private static void CheckWrittenFileAssertions(IUmbrellaFileStorageProvider provider, IUmbrellaFileInfo file, int length, string fileName)
	{
		CheckPOCOFileType(provider, file);
		Assert.False(file.IsNew);
		Assert.Equal(length, file.Length);
		Assert.Equal(DateTimeOffset.UtcNow.Date, file.LastModified!.Value.UtcDateTime.Date);
		Assert.Equal(fileName, file.Name);
		Assert.Equal("image/png", file.ContentType);
	}

	private static UmbrellaFileAuthorizationHandlerRegistry CreateAuthorizationHandlerRegistry()
		=> new UmbrellaFileAuthorizationHandlerRegistry([]);

	private static UmbrellaSharePointFileStorageProvider CreateSharePointFileProvider()
	{
		var options = new UmbrellaSharePointFileStorageProviderOptions
		{
			SiteId = "zinofi.sharepoint.com:/sites/Berkeley:",
			DriveName = "General Document",
			GraphServiceClient = new GraphServiceClient(new ClientSecretCredential(SharePointTenantId, SharePointClientId, SharePointClientSecret)),
			AllowUnhandledFileAuthorizationChecks = true
		};

#pragma warning disable CA2000
		var provider = new UmbrellaSharePointFileStorageProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaSharePointFileStorageProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());
#pragma warning restore CA2000

		provider.InitializeOptions(options);

		return provider;
	}

	private static void CheckPOCOFileType(IUmbrellaFileStorageProvider provider, IUmbrellaFileInfo file)
	{
		object _ = provider switch
		{
			UmbrellaAzureBlobStorageFileProvider => Assert.IsType<UmbrellaAzureBlobFileInfo>(file),
			UmbrellaDiskFileStorageProvider => Assert.IsType<UmbrellaDiskFileInfo>(file),
			UmbrellaSharePointFileStorageProvider => Assert.IsType<UmbrellaSharePointFileInfo>(file),
			_ => throw new InvalidOperationException("Unsupported provider."),
		};
	}
}
