using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Umbrella.FileSystem.Abstractions;

namespace Umbrella.FileSystem.SharePoint.Test;

public class UmbrellaSharePointServiceCollectionExtensionsTest
{
	[Fact]
	public void AddUmbrellaSharePointFileStorageProvider_RegistersExpectedServices()
	{
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddUmbrellaUtilities();
		_ = services.AddUmbrellaFileSystemCore();
		_ = services.AddUmbrellaSharePointFileStorageProvider((_, options) =>
		{
			options.SiteId = "contoso.sharepoint.com:/sites/TestSite:";
			options.DriveName = "Shared Documents";
			options.GraphServiceClient = new GraphServiceClient(new FakeTokenCredential());
		});

		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		IUmbrellaSharePointFileStorageProvider sharePointProvider = serviceProvider.GetRequiredService<IUmbrellaSharePointFileStorageProvider>();
		IUmbrellaFileStorageProvider defaultProvider = serviceProvider.GetRequiredService<IUmbrellaFileStorageProvider>();
		IUmbrellaFileStorageProviderOptions providerOptions = serviceProvider.GetRequiredService<IUmbrellaFileStorageProviderOptions>();

		_ = Assert.IsType<UmbrellaSharePointFileStorageProvider>(sharePointProvider);
		Assert.Same(sharePointProvider, defaultProvider);
		_ = Assert.IsType<UmbrellaSharePointFileStorageProviderOptions>(providerOptions);
	}

	private sealed class FakeTokenCredential : TokenCredential
	{
		private static readonly AccessToken _accessToken = new("test-token", DateTimeOffset.UtcNow.AddHours(1));

		public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> _accessToken;

		public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> ValueTask.FromResult(_accessToken);
	}
}
