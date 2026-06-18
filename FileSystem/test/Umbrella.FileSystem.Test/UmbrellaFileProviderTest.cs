
using System.Collections.ObjectModel;
using Azure.Identity;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Umbrella.FileSystem.Abstractions;
using Umbrella.FileSystem.AzureStorage;
using Umbrella.FileSystem.Dataverse;
using Umbrella.FileSystem.Disk;
using Umbrella.FileSystem.SharePoint;
using Umbrella.Internal.Mocks;
using Umbrella.Utilities.Compilation;
using Umbrella.Utilities.Helpers;
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

	private static string DataverseClientId => _sharePointConfig["Dataverse:ClientId"]!;
	private static string DataverseClientSecret => _sharePointConfig["Dataverse:ClientSecret"]!;
	private static string DataverseUrl => _sharePointConfig["Dataverse:Url"]!;

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
		CreateSharePointFileProvider,
		CreateDataverseFileProvider
	];

	// SharePoint restricts % in path segments; exclude from path-variation tests to avoid false failures
	// Dataverse uses a GUID-per-directory model so arbitrary path variations are handled by the adapter
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _pathVariationProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider
	];

	// SharePoint and Dataverse throw NotSupportedException for metadata operations
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _metadataCapableProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider
	];

	// Dataverse throws NotSupportedException for Copy/Move; only these providers support it
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _copyMoveCapableProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider,
		CreateSharePointFileProvider
	];

	// Dataverse records are flat (one file per record) so downlevel directory tests can't work cross-GUID
	private static readonly List<Func<IUmbrellaFileStorageProvider>> _hierarchyProviders =
	[
		CreateAzureBlobFileProvider,
		CreateDiskFileProvider,
		CreateSharePointFileProvider
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
	public static List<object[]> CopyMoveCapableProvidersMemberData = _copyMoveCapableProviders.Select(x => new object[] { x }).ToList();
	public static List<object[]> HierarchyProvidersMemberData = _hierarchyProviders.Select(x => new object[] { x }).ToList();
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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

		IUmbrellaFileInfo? retrievedFile = await provider.GetAsync($"/images/doesnotexist.jpg", TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.Null(retrievedFile);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task CreateAsync_GetAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

		string physicalPath = PathHelper.PlatformNormalize($@"{BaseDirectory}\{TestFileName}");

		byte[] bytes = await File.ReadAllBytesAsync(physicalPath, TestContext.Current.CancellationToken);

		string subpath = $"/images/{TestFileName}";

		IUmbrellaFileInfo fileInfo = await provider.SaveAsync(subpath, bytes, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);

		CheckWrittenFileAssertions(provider, fileInfo, bytes.Length, TestFileName);

		IUmbrellaFileInfo? reloadedFileInfo = await provider.GetAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.NotNull(reloadedFileInfo);

		CheckWrittenFileAssertions(provider, reloadedFileInfo!, bytes.Length, TestFileName);

		// Cleanup
		bool deleted = await provider.DeleteAsync(subpath, TestContext.Current.CancellationToken).ConfigureAwait(true);

		Assert.True(deleted);
	}

	[Theory]
	[MemberData(nameof(ProvidersMemberData))]
	public async Task SaveAsyncStream_GetAsync_DeletePathAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
	{
		Guard.IsNotNull(providerFunc);

		var provider = providerFunc();
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
	public async Task CopyAsync_FromPath_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			_ = await provider.CopyAsync("~/images/notexists.jpg", "~/images/willfail.png", CancellationToken.None).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
	public async Task MoveAsync_FromPath_NotExistsAsync(Func<IUmbrellaFileStorageProvider> providerFunc)
		=> await Assert.ThrowsAsync<UmbrellaFileNotFoundException>(async () =>
		{
			var provider = providerFunc();

			_ = await provider.MoveAsync("~/images/notexists.jpg", "~/images/willfail.png", TestContext.Current.CancellationToken).ConfigureAwait(true);
		}).ConfigureAwait(true);

	[Theory]
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
	[MemberData(nameof(CopyMoveCapableProvidersMemberData))]
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
		await using var cleanup = provider as IAsyncDisposable;

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
		await using var cleanup = provider as IAsyncDisposable;

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
	[MemberData(nameof(HierarchyProvidersMemberData))]
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
	[MemberData(nameof(HierarchyProvidersMemberData))]
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

	private static DataversePathAdapterProvider CreateDataverseFileProvider()
	{
		var connectionOptions = new ConnectionOptions
		{
			ClientId = DataverseClientId,
			ClientSecret = DataverseClientSecret,
			ServiceUri = new Uri(DataverseUrl),
			AuthenticationType = AuthenticationType.ClientSecret
		};

#pragma warning disable CA2000
		var serviceClient = new ServiceClient(connectionOptions,
			serviceClientConfiguration: new ConfigurationOptions { UseWebApi = true });

		var options = new UmbrellaDataverseAnnotationFileStorageProviderOptions
		{
			DataverseClient = serviceClient,
			AllowUnhandledFileAuthorizationChecks = true,
			MetadataColumnMappings = new Dictionary<string, DataverseMetadataColumnMapping>(StringComparer.OrdinalIgnoreCase)
			{
				["Subject"] = new DataverseMetadataColumnMapping { ColumnName = "subject", ColumnType = DataverseMetadataColumnType.Text },
				["IsDocument"] = new DataverseMetadataColumnMapping { ColumnName = "isdocument", ColumnType = DataverseMetadataColumnType.Boolean },
				["NoteText"] = new DataverseMetadataColumnMapping { ColumnName = "notetext", ColumnType = DataverseMetadataColumnType.Text }
			}
		};

		var innerProvider = new UmbrellaDataverseFileStorageProvider(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaDataverseFileStorageProvider>(),
			CoreUtilitiesMocks.CreateMimeTypeUtility(("png", "image/png"), ("jpg", "image/jpg")),
			CoreUtilitiesMocks.CreateGenericTypeConverter(),
			CreateAuthorizationHandlerRegistry());
#pragma warning restore CA2000

		innerProvider.InitializeOptions(options);

		return new DataversePathAdapterProvider(innerProvider, options.DataverseClient, options.TableName);
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

	private static UmbrellaFileAuthorizationHandlerRegistry CreateAuthorizationHandlerRegistry() => new([]);

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
			DataversePathAdapterProvider => Assert.IsType<UmbrellaDataverseFileInfo>(file),
			_ => throw new InvalidOperationException("Unsupported provider."),
		};
	}

	private sealed class DataversePathAdapterProvider(
		IUmbrellaFileStorageProvider inner,
		IOrganizationServiceAsync2 dataverseClient,
		string tableName) : IUmbrellaFileStorageProvider, IAsyncDisposable
	{
		private readonly Dictionary<string, string> _dirGuidCache = new(StringComparer.OrdinalIgnoreCase);

		public async ValueTask DisposeAsync()
		{
			foreach (string guid in _dirGuidCache.Values)
			{
				try
				{
					await dataverseClient.DeleteAsync(tableName, Guid.Parse(guid), CancellationToken.None).ConfigureAwait(false);
				}
				catch { /* best-effort */ }
			}
		}

		private static string NormalizeKey(string path)
		{
			path = path.TrimStart('~').Replace('\\', '/');
			return string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
		}

		private string GetDirGuid(string normalizedDirKey)
		{
			if (!_dirGuidCache.TryGetValue(normalizedDirKey, out string? guid))
			{
				guid = Guid.NewGuid().ToString();
				_dirGuidCache[normalizedDirKey] = guid;
			}

			return guid;
		}

		private string AdaptFilePath(string path)
		{
			string normalized = NormalizeKey(path);
			string[] parts = normalized.Split('/');
			string fileName = parts[^1];
			string dirKey = parts.Length > 1 ? string.Join("/", parts[..^1]) : "root";
			return $"/{tableName}/{GetDirGuid(dirKey)}/{fileName}";
		}

		private string AdaptDirPath(string path)
		{
			string normalized = NormalizeKey(path);
			return $"/{tableName}/{GetDirGuid(normalized)}";
		}

		public void InitializeOptions(UmbrellaFileStorageProviderOptionsBase options) { }

		public Task<IUmbrellaFileInfo> CreateAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.CreateAsync(AdaptFilePath(subpath), cancellationToken);

		public Task<IUmbrellaFileInfo?> GetAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.GetAsync(AdaptFilePath(subpath), cancellationToken);

		public Task<bool> DeleteAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.DeleteAsync(AdaptFilePath(subpath), cancellationToken);

		public Task<bool> DeleteAsync(IUmbrellaFileInfo fileInfo, CancellationToken cancellationToken = default)
			=> inner.DeleteAsync(fileInfo, cancellationToken);

		public Task<IUmbrellaFileInfo> CopyAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default)
			=> inner.CopyAsync(AdaptFilePath(sourceSubpath), AdaptFilePath(destinationSubpath), cancellationToken);

		public Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default)
			=> inner.CopyAsync(sourceFile, AdaptFilePath(destinationSubpath), cancellationToken);

		public Task<IUmbrellaFileInfo> CopyAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
			=> inner.CopyAsync(sourceFile, destinationFile, cancellationToken);

		public Task<IUmbrellaFileInfo> SaveAsync(string subpath, byte[] bytes, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> inner.SaveAsync(AdaptFilePath(subpath), bytes, bufferSizeOverride, cancellationToken);

		public Task<IUmbrellaFileInfo> SaveAsync(string subpath, Stream stream, int? bufferSizeOverride = null, CancellationToken cancellationToken = default)
			=> inner.SaveAsync(AdaptFilePath(subpath), stream, bufferSizeOverride, cancellationToken);

		public Task<bool> ExistsAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.ExistsAsync(AdaptFilePath(subpath), cancellationToken);

		public Task DeleteDirectoryAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.DeleteDirectoryAsync(AdaptDirPath(subpath), cancellationToken);

		public Task<IUmbrellaFileInfo> MoveAsync(string sourceSubpath, string destinationSubpath, CancellationToken cancellationToken = default)
			=> inner.MoveAsync(AdaptFilePath(sourceSubpath), AdaptFilePath(destinationSubpath), cancellationToken);

		public Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, string destinationSubpath, CancellationToken cancellationToken = default)
			=> inner.MoveAsync(sourceFile, AdaptFilePath(destinationSubpath), cancellationToken);

		public Task<IUmbrellaFileInfo> MoveAsync(IUmbrellaFileInfo sourceFile, IUmbrellaFileInfo destinationFile, CancellationToken cancellationToken = default)
			=> inner.MoveAsync(sourceFile, destinationFile, cancellationToken);

		public Task<IReadOnlyCollection<IUmbrellaFileInfo>> EnumerateDirectoryAsync(string subpath, CancellationToken cancellationToken = default)
			=> inner.EnumerateDirectoryAsync(AdaptDirPath(subpath), cancellationToken);
	}
}
