using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.DataProtection;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for the <see cref="IDataProtectionBuilder"/> type.
/// </summary>
public static class IDataProtectionBuilderExtensions
{
	/// <summary>
	/// Persists the data protection keys to Azure Blob Storage and protects them with Azure Key Vault.
	/// </summary>
	/// <param name="builder">The <see cref="IDataProtectionBuilder"/> instance.</param>
	/// <param name="keyVaultName">The name of the Azure Key Vault.</param>
	/// <param name="storageConnectionString">The connection string for the Azure Blob Storage.</param>
	/// <param name="dpapiKeysContainerName">The name of the container in Azure Blob Storage to store the keys.</param>
	/// <param name="keysFileName">The name of the file in the container to store the keys.</param>
	/// <returns>The <see cref="IDataProtectionBuilder"/> instance.</returns>
	/// <remarks>
	/// The specified Azure Blob Storage container will be created if it does not already exist.
	/// The keys file will also be created if it does not already exist in the container with an empty repository element.
	/// </remarks>
	public static async Task<IDataProtectionBuilder> PersistKeysToBlobStorageAndProtectWithKeyVaultAsync(
		this IDataProtectionBuilder builder,
		string? keyVaultName,
		string? storageConnectionString,
		string dpapiKeysContainerName = "dpapi-keys",
		string keysFileName = "keys.xml")
	{
		Guard.IsNotNull(builder);
		Guard.IsNotNullOrEmpty(keyVaultName);
		Guard.IsNotNullOrEmpty(storageConnectionString);

		BlobServiceClient serviceClient = new(storageConnectionString);
		BlobContainerClient container = serviceClient.GetBlobContainerClient(dpapiKeysContainerName);
		BlobClient blobClient = container.GetBlobClient(keysFileName);

		_ = await container.CreateIfNotExistsAsync().ConfigureAwait(false);

		if (!await blobClient.ExistsAsync().ConfigureAwait(false))
		{
			_ = await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(
				"""
				<?xml version="1.0" encoding="utf-8"?>
				<repository>
				</repository>
				"""), false), true).ConfigureAwait(false);
		}

		return builder
			.PersistKeysToAzureBlobStorage(blobClient)
			.ProtectKeysWithAzureKeyVault(new Uri($"https://{keyVaultName}.vault.azure.net/keys/DataProtectionKey"), new DefaultAzureCredential());
	}
}