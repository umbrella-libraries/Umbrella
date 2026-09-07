using Testcontainers.Azurite;
using Testcontainers.ServiceBus;

namespace Umbrella.AzureServiceBus.Test;

public sealed class ServiceBusContainerFixture : IAsyncLifetime
{
	private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";
	private const string ServiceBusImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1";
	private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder(AzuriteImage)
		.WithInMemoryPersistence()
		.Build();
	private readonly ServiceBusContainer _serviceBusContainer = new ServiceBusBuilder(ServiceBusImage)
		.WithAcceptLicenseAgreement(true)
		.WithConfig(Path.Combine(AppContext.BaseDirectory, "ServiceBusEmulatorConfig.json"))
		.Build();

	public string StorageConnectionString => _azuriteContainer.GetConnectionString();

	public string ServiceBusConnectionString => _serviceBusContainer.GetConnectionString();

	public async ValueTask InitializeAsync()
	{
		await _azuriteContainer.StartAsync();
		await _serviceBusContainer.StartAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _serviceBusContainer.DisposeAsync();
		await _azuriteContainer.DisposeAsync();
		GC.SuppressFinalize(this);
	}
}
