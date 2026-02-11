using System.Security.Cryptography;
using Azure;
using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CommunityToolkit.Diagnostics;
using Microsoft.Extensions.Logging;
using Umbrella.AzureServiceBus.Abstractions;
using Umbrella.AzureServiceBus.Exceptions;

namespace Umbrella.AzureServiceBus.Plugins;

/// <summary>
/// An implementation of <see cref="IUmbrellaAzureServiceBusPlugin"/> that uses the Claim Check pattern to store large messages in Azure Blob Storage.
/// </summary>
public class UmbrellaAzureServiceBusClaimCheckPlugin : IUmbrellaAzureServiceBusPlugin
{
	private static readonly BinaryData _emptyBinaryData = new([]);

	private readonly ILogger _logger;
	private readonly string _storageConnectionString;
	private readonly string _blobContainerName;
	private readonly string _blobMetadataReferenceKey;
	private readonly int _maximumMessageSizeInBytes;

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaAzureServiceBusClaimCheckPlugin"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="storageConnectionString">The storage connection string.</param>
	/// <param name="blobContainerName">The blob container name.</param>
	/// <param name="blobMetadataReferenceKey">The blob metadata reference key.</param>
	/// <param name="maximumMessageSizeInBytes">The maximum message size in bytes.</param>
	public UmbrellaAzureServiceBusClaimCheckPlugin(
		ILogger<UmbrellaAzureServiceBusClaimCheckPlugin> logger,
		string storageConnectionString,
		string blobContainerName,
		string blobMetadataReferenceKey = "BlobReference",
		int maximumMessageSizeInBytes = 200 * 1024)
	{
		Guard.IsNotNullOrWhiteSpace(storageConnectionString);
		Guard.IsNotNullOrWhiteSpace(blobContainerName);
		Guard.IsNotNullOrWhiteSpace(blobMetadataReferenceKey);
		Guard.IsGreaterThan(maximumMessageSizeInBytes, 0);

		_logger = logger;
		_storageConnectionString = storageConnectionString;
		_blobContainerName = blobContainerName;
		_blobMetadataReferenceKey = blobMetadataReferenceKey;
		_maximumMessageSizeInBytes = maximumMessageSizeInBytes;
	}

	/// <inheritdoc />
	public async Task OnSendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(message);

		try
		{
			if (message.Body.ToArray().Length > _maximumMessageSizeInBytes)
			{
				string blobName = Guid.NewGuid().ToString();

				message.ApplicationProperties.Add(_blobMetadataReferenceKey, blobName);

				BlobClient blobClient = await GetBlobClientAsync(blobName, cancellationToken).ConfigureAwait(false);

				Response<BlobContentInfo> result = await blobClient.UploadAsync(message.Body, cancellationToken);

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
				if (!result.Value.ContentHash.SequenceEqual(MD5.HashData(message.Body.ToArray())))
					throw new InvalidOperationException("The blob was not uploaded successfully.");
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

				message.Body = _emptyBinaryData;
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message.MessageId }))
		{
			throw new UmbrellaAzureServiceBusException("There has been a problem invoking this plugin when sending a message.", exc);
		}
	}

	/// <inheritdoc />
	public async Task OnReceiveMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(message);

		try
		{
			if (message.ApplicationProperties.TryGetValue(_blobMetadataReferenceKey, out object? blobReference) && blobReference is string blobName)
			{
				BlobClient blobClient = await GetBlobClientAsync(blobName, cancellationToken).ConfigureAwait(false);

				if (!await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false))
					throw new InvalidOperationException($"The blob '{blobName}' does not exist in the container '{_blobContainerName}'.");

				using MemoryStream memoryStream = new();
				_ = await blobClient.DownloadToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

				AmqpAnnotatedMessage rawMessage = message.GetRawAmqpMessage();

				rawMessage.Body = new AmqpMessageBody(new List<ReadOnlyMemory<byte>> { memoryStream.ToArray().AsMemory() });
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message.MessageId }))
		{
			throw new UmbrellaAzureServiceBusException("There has been a problem invoking this plugin when receiving a message.", exc);
		}
	}

	/// <inheritdoc />
	public async Task OnCompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		Guard.IsNotNull(message);

		try
		{
			if (message.ApplicationProperties.TryGetValue(_blobMetadataReferenceKey, out object? blobReference) && blobReference is string blobName)
			{
				BlobClient blobClient = await GetBlobClientAsync(blobName, cancellationToken).ConfigureAwait(false);

				_ = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { message.MessageId }))
		{
			throw new UmbrellaAzureServiceBusException("There has been a problem invoking this plugin when completing a message.", exc);
		}
	}

	private async Task<BlobClient> GetBlobClientAsync(string blobName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		BlobServiceClient blobServiceClient = new(_storageConnectionString);
		BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(_blobContainerName);

		_ = await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

		BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

		return blobClient;
	}
}