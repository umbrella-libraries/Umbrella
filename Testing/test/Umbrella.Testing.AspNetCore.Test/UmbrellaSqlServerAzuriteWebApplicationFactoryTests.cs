using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbrella.Testing.AspNetCore.TestApp;

namespace Umbrella.Testing.AspNetCore.Test;

public sealed class UmbrellaSqlServerAzuriteWebApplicationFactoryTests
{
	[Fact]
	public void ConfigureServicesReplacesExistingDbContextRegistration()
	{
		var services = new ServiceCollection();
		var existingOptions = new DbContextOptionsBuilder<SmokeDbContext>().Options;

		_ = services.AddSingleton(existingOptions);
		_ = services.AddSingleton<SmokeDbContext>();

		using var factory = new SmokeSqlServerAzuriteWebApplicationFactory();

		factory.ConfigureServicesForTest(services);

		Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(DbContextOptions<SmokeDbContext>));
		Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(SmokeDbContext) && descriptor.Lifetime == ServiceLifetime.Singleton);
		Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SmokeDbContext) && descriptor.Lifetime == ServiceLifetime.Scoped);
	}

	[Fact]
	public async Task SqlServerFactoryCanStartContainerAndCreateDatabaseWhenEnabled()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("UMBRELLA_RUN_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
			Assert.Skip("Set UMBRELLA_RUN_TESTCONTAINERS=true to run this Docker-backed smoke test.");

		await using var factory = new SmokeSqlServerAzuriteDisabledWebApplicationFactory();

		await factory.InitializeAsync();

		using HttpClient client = factory.CreateClient();

		using HttpResponseMessage response = await client.GetAsync("/db", TestContext.Current.CancellationToken);
		string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("1", content);
	}

	[Fact]
	public async Task SqlServerAzuriteFactoryCanStartContainersWhenEnabled()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("UMBRELLA_RUN_TESTCONTAINERS"), "true", StringComparison.OrdinalIgnoreCase))
			Assert.Skip("Set UMBRELLA_RUN_TESTCONTAINERS=true to run this Docker-backed smoke test.");

		await using var factory = new SmokeSqlServerAzuriteWebApplicationFactory();

		await factory.InitializeAsync();

		Assert.False(string.IsNullOrWhiteSpace(factory.GetAzuriteConnectionStringForTest()));
	}

	private sealed class SmokeSqlServerAzuriteDisabledWebApplicationFactory : SmokeSqlServerAzuriteWebApplicationFactory
	{
		protected override bool UseAzurite => false;
	}

	private class SmokeSqlServerAzuriteWebApplicationFactory : UmbrellaSqlServerAzuriteWebApplicationFactory<SmokeProgram, SmokeDbContext>
	{
		public void ConfigureServicesForTest(IServiceCollection services) => ConfigureServices(services);

		public string GetAzuriteConnectionStringForTest() => AzuriteConnectionString;

		protected override string GetEnvironmentName() => "Smoke";

		protected override void ConfigureAuthentication(IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);
		}

		protected override void InitializeDatabase(IServiceProvider serviceProvider)
		{
			using IServiceScope scope = serviceProvider.CreateScope();
			SmokeDbContext dbContext = scope.ServiceProvider.GetRequiredService<SmokeDbContext>();

			_ = dbContext.Database.EnsureCreated();
		}
	}
}
