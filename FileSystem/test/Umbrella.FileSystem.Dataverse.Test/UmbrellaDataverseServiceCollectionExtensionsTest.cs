using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client;
using Moq;
using Umbrella.FileSystem.Abstractions;
using Xunit;

namespace Umbrella.FileSystem.Dataverse.Test;

public class UmbrellaDataverseServiceCollectionExtensionsTest
{
	[Fact]
	public void AddUmbrellaDataverseFileStorageProvider_RegistersExpectedServices()
	{
		var services = new ServiceCollection();
		var mockClient = new Mock<IOrganizationServiceAsync2>();

		_ = services.AddLogging();
		_ = services.AddUmbrellaUtilities();
		_ = services.AddUmbrellaFileSystemCore();
		_ = services.AddUmbrellaDataverseFileStorageProvider((_, options) =>
		{
			options.DataverseClient = mockClient.Object;
			options.TableName = "note";
			options.IdColumnName = "noteid";
			options.DataColumnName = "notetext";
			options.FileNameColumnName = "filename";
		});

		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		IUmbrellaDataverseFileStorageProvider dataverseProvider = serviceProvider.GetRequiredService<IUmbrellaDataverseFileStorageProvider>();
		IUmbrellaFileStorageProvider defaultProvider = serviceProvider.GetRequiredService<IUmbrellaFileStorageProvider>();
		IUmbrellaFileStorageProviderOptions providerOptions = serviceProvider.GetRequiredService<IUmbrellaFileStorageProviderOptions>();

		Assert.IsType<UmbrellaDataverseFileStorageProvider>(dataverseProvider);
		Assert.Same(dataverseProvider, defaultProvider);
		Assert.IsType<UmbrellaDataverseFileStorageProviderOptions>(providerOptions);
	}
}
