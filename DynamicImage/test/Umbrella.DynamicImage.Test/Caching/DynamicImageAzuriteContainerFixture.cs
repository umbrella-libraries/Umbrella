using Testcontainers.Azurite;

namespace Umbrella.DynamicImage.Test.Caching;

public sealed class DynamicImageAzuriteContainerFixture : IAsyncLifetime
{
	private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";
	private readonly AzuriteContainer _container = new AzuriteBuilder(AzuriteImage)
		.WithInMemoryPersistence()
		.Build();

	public string ConnectionString => _container.GetConnectionString();

	public async ValueTask InitializeAsync() => await _container.StartAsync();

	public async ValueTask DisposeAsync()
	{
		await _container.DisposeAsync();
		GC.SuppressFinalize(this);
	}
}
