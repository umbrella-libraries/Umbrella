using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;

#if !AZUREDEVOPS
using Microsoft.Extensions.Configuration;
#endif
using Umbrella.AzureServiceBus.Abstractions;
using Umbrella.AzureServiceBus.Plugins;
using Umbrella.Internal.Mocks;

namespace Umbrella.AzureServiceBus.Test;

public class UmbrellaAzureServiceBusClientTest
{
	private const string TestSmallQueueName = "test-small";
	private const string TestLargeQueueName = "test-large";
	private const string BlobContainerName = "service-bus-claim-check";
	private const string BlobMetadataReferenceKey = "BlobReference";

#if AZUREDEVOPS
    private static readonly string _storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString")!;
    private static readonly string _serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")!;
#else
#pragma warning disable CA1802 // Use literals where appropriate
	private static readonly string _storageConnectionString = "UseDevelopmentStorage=true";
	private static readonly string _serviceBusConnectionString = "";
#pragma warning restore CA1802 // Use literals where appropriate
#endif

	private static readonly ReadOnlyMemory<byte> _smallMessageBody = GetRandomBuffer(1024 * 200);
	private static readonly ReadOnlyMemory<byte> _largeMessageBody = GetRandomBuffer(1024 * 1024 * 2);

#if !AZUREDEVOPS
#pragma warning disable CA1810 // Initialize reference type static fields inline
	static UmbrellaAzureServiceBusClientTest()
#pragma warning restore CA1810 // Initialize reference type static fields inline
	{
		var builder = new ConfigurationBuilder().AddUserSecrets<UmbrellaAzureServiceBusClientTest>();
		var config = builder.Build();

		_serviceBusConnectionString = config["ServiceBusConnectionString"]!;
	}
#endif

	[Fact]
	public async Task CreateSendReceieveCompletePluginMessageSmallAsync()
	{
		await using UmbrellaAzureServiceBusClient client = CreateClient(true);
		await using UmbrellaAzureServiceBusSender sender = client.CreateSender(TestSmallQueueName);

		ServiceBusMessage message = new(_smallMessageBody);

		await sender.SendMessageAsync(message, TestContext.Current.CancellationToken);

		Assert.False(message.ApplicationProperties.ContainsKey(BlobMetadataReferenceKey));

		await using UmbrellaAzureServiceBusReceiver receiver = client.CreateReceiver(TestSmallQueueName);
		ServiceBusReceivedMessage receivedMessage = await receiver.ReceiveMessageAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(_smallMessageBody.Length, receivedMessage.Body.ToMemory().Length);

		await receiver.CompleteMessageAsync(receivedMessage, TestContext.Current.CancellationToken);

		var nextMessage = await receiver.ReceiveMessageAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.True(nextMessage is null || nextMessage.MessageId != message.MessageId);
	}

	[Fact]
	public async Task CreateSendReceiveCompletePluginMessageLargeAsync()
	{
		await using UmbrellaAzureServiceBusClient client = CreateClient(true);
		await using UmbrellaAzureServiceBusSender sender = client.CreateSender(TestLargeQueueName);

		ServiceBusMessage message = new(_largeMessageBody);

		await sender.SendMessageAsync(message, TestContext.Current.CancellationToken);

		Assert.True(message.ApplicationProperties.ContainsKey(BlobMetadataReferenceKey));

		await using UmbrellaAzureServiceBusReceiver receiver = client.CreateReceiver(TestLargeQueueName);
		ServiceBusReceivedMessage receivedMessage = await receiver.ReceiveMessageAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(_largeMessageBody.Length, receivedMessage.Body.ToMemory().Length);

		var blobClient = await GetBlobClientAsync((string)receivedMessage.ApplicationProperties[BlobMetadataReferenceKey], CancellationToken.None);

		Assert.True(await blobClient.ExistsAsync(TestContext.Current.CancellationToken));

		var blobProperties = await blobClient.GetPropertiesAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.Equal(_largeMessageBody.Length, blobProperties.Value.ContentLength);

		await receiver.CompleteMessageAsync(receivedMessage, TestContext.Current.CancellationToken);

		Assert.False(await blobClient.ExistsAsync(TestContext.Current.CancellationToken));

		var nextMessage = await receiver.ReceiveMessageAsync(cancellationToken: TestContext.Current.CancellationToken);

		Assert.True(nextMessage is null || nextMessage.MessageId != message.MessageId);
	}

	private static UmbrellaAzureServiceBusClient CreateClient(bool addClaimCheckPlugin)
	{
		// Define Plugins
		IReadOnlyCollection<IUmbrellaAzureServiceBusPlugin> plugins = addClaimCheckPlugin ?
		[
			new UmbrellaAzureServiceBusClaimCheckPlugin(
				CoreUtilitiesMocks.CreateLogger<UmbrellaAzureServiceBusClaimCheckPlugin>(),
				_storageConnectionString,
				BlobContainerName,
				BlobMetadataReferenceKey)
		] : [];

#pragma warning disable CA2000 // Dispose objects before losing scope
		return new UmbrellaAzureServiceBusClient(
			CoreUtilitiesMocks.CreateLoggerFactory<UmbrellaAzureServiceBusClient>(),
			_serviceBusConnectionString,
			plugins: plugins);
#pragma warning restore CA2000 // Dispose objects before losing scope
	}

	private static byte[] GetRandomBuffer(long size)
	{
		char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

		byte[] buffer = new byte[size];

#pragma warning disable CA5394 // Do not use insecure randomness
		Random.Shared.NextBytes(buffer);
#pragma warning restore CA5394 // Do not use insecure randomness

		byte[] text = new byte[size];

		for (int i = 0; i < size; i++)
		{
			int idx = buffer[i] % chars.Length;
			text[i] = (byte)chars[idx];
		}

		return text;
	}

	private static async Task<BlobClient> GetBlobClientAsync(string blobName, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		BlobServiceClient blobServiceClient = new(_storageConnectionString);
		BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);

		_ = await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

		BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

		return blobClient;
	}
}