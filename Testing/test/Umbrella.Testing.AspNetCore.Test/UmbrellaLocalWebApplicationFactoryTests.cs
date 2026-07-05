using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbrella.Testing.AspNetCore.TestApp;

namespace Umbrella.Testing.AspNetCore.Test;

public sealed class UmbrellaLocalWebApplicationFactoryTests
{
	[Fact]
	public async Task CreateClientUsesConfiguredEnvironmentAndServices()
	{
		await using var factory = new SmokeLocalWebApplicationFactory();

		using HttpClient client = factory.CreateClient();

		using HttpResponseMessage response = await client.GetAsync("/", TestContext.Current.CancellationToken);
		string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("Smoke", content);
		Assert.True(factory.ConfigureServicesCalled);
	}

	[Fact]
	public void ConfigureLoggingUsesWarningMinimumLevel()
	{
		using var factory = new SmokeLocalWebApplicationFactory();
		var services = new ServiceCollection();

		_ = services.AddLogging(factory.ConfigureLoggingForTest);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		LoggerFilterOptions options = serviceProvider.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

		Assert.Equal(LogLevel.Warning, options.MinLevel);
	}

	private sealed class SmokeLocalWebApplicationFactory : UmbrellaLocalWebApplicationFactory<SmokeProgram>
	{
		public bool ConfigureServicesCalled { get; private set; }

		public void ConfigureLoggingForTest(ILoggingBuilder logging) => ConfigureLogging(logging);

		protected override string GetEnvironmentName() => "Smoke";

		protected override void ConfigureAuthentication(IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);
		}

		protected override void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services);

			ConfigureServicesCalled = true;
		}
	}
}
