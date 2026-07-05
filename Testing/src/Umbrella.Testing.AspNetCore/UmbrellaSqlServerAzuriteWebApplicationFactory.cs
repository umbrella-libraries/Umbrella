using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace Umbrella.Testing.AspNetCore;

/// <summary>
/// ASP.NET Core integration test factory backed by a SQL Server Testcontainer, an optional Azurite Testcontainer, and
/// an EF Core <see cref="DbContext"/>.
/// </summary>
/// <typeparam name="TProgram">The application entry point type.</typeparam>
/// <typeparam name="TDbContext">The EF Core database context type.</typeparam>
public abstract class UmbrellaSqlServerAzuriteWebApplicationFactory<TProgram, TDbContext> : UmbrellaWebApplicationFactory<TProgram>, IAsyncLifetime
	where TProgram : class
	where TDbContext : DbContext
{
	private const int DefaultAzuriteBlobPort = 10000;
	private const string DefaultAzuriteAccountName = "umbrellatests";
	private const string DefaultAzuriteAccountKey = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkJSYnKCkqKywtLi8wMTIzNDU2Nzg5Ojs8PT4/QA==";

	private MsSqlContainer? _databaseContainer;
	private IContainer? _azuriteContainer;

	/// <summary>
	/// Gets the SQL Server Testcontainer.
	/// </summary>
	protected MsSqlContainer DatabaseContainer => _databaseContainer ??= CreateMsSqlBuilder().Build();

	/// <summary>
	/// Gets the Azurite Testcontainer.
	/// </summary>
	protected IContainer AzuriteContainer => _azuriteContainer ??= CreateAzuriteContainer();

	/// <summary>
	/// Gets the SQL Server connection string for the current Testcontainer.
	/// </summary>
	public string ConnectionString => DatabaseContainer.GetConnectionString();

	/// <summary>
	/// Gets the Azure Storage connection string for the current Azurite Testcontainer.
	/// </summary>
	protected string AzuriteConnectionString
	{
		get
		{
			ushort blobPort = AzuriteContainer.GetMappedPublicPort(AzuriteBlobPort);

			return $"DefaultEndpointsProtocol=http;AccountName={AzuriteAccountName};AccountKey={AzuriteAccountKey};BlobEndpoint=http://127.0.0.1:{blobPort}/{AzuriteAccountName};";
		}
	}

	/// <summary>
	/// Gets a value indicating whether the Azurite Testcontainer should be started and configured.
	/// </summary>
	protected virtual bool UseAzurite => true;

	/// <summary>
	/// Gets the Azurite Docker image.
	/// </summary>
	protected virtual string AzuriteImage => "mcr.microsoft.com/azure-storage/azurite:latest";

	/// <summary>
	/// Gets the Azurite blob service container port.
	/// </summary>
	protected virtual int AzuriteBlobPort => DefaultAzuriteBlobPort;

	/// <summary>
	/// Gets the Azurite account name.
	/// </summary>
	protected virtual string AzuriteAccountName => DefaultAzuriteAccountName;

	/// <summary>
	/// Gets the Azurite account key.
	/// </summary>
	protected virtual string AzuriteAccountKey => DefaultAzuriteAccountKey;

	/// <inheritdoc />
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		base.ConfigureWebHost(builder);

		if (UseAzurite)
			_ = builder.ConfigureAppConfiguration((_, configurationBuilder) => ConfigureAzuriteConfiguration(configurationBuilder, AzuriteConnectionString));
	}

	/// <inheritdoc />
	protected override void ConfigureServices(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		base.ConfigureServices(services);

		ReplaceDbContextRegistration(services);

		_ = services.AddDbContext<TDbContext>(options => ConfigureDbContext(options, ConnectionString));
	}

	/// <inheritdoc />
	protected override IHost CreateHost(IHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		IHost host = base.CreateHost(builder);

		InitializeDatabase(host.Services);

		return host;
	}

	/// <inheritdoc />
	public virtual async ValueTask InitializeAsync()
	{
		await DatabaseContainer.StartAsync();

		if (UseAzurite)
			await AzuriteContainer.StartAsync();
	}

	/// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();

		if (_databaseContainer is not null)
			await _databaseContainer.DisposeAsync();

		if (_azuriteContainer is not null)
			await _azuriteContainer.DisposeAsync();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates the SQL Server Testcontainer builder.
	/// </summary>
	/// <returns>The configured SQL Server Testcontainer builder.</returns>
	protected virtual MsSqlBuilder CreateMsSqlBuilder() =>
		new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU16-ubuntu-22.04")
			.WithPassword("yourStrong(!)Password");

	/// <summary>
	/// Creates the Azurite Testcontainer.
	/// </summary>
	/// <returns>The configured Azurite Testcontainer.</returns>
	protected virtual IContainer CreateAzuriteContainer() =>
		new ContainerBuilder(AzuriteImage)
			.WithCommand("azurite", "--blobHost", "0.0.0.0", "--skipApiVersionCheck")
			.WithEnvironment("AZURITE_ACCOUNTS", $"{AzuriteAccountName}:{AzuriteAccountKey}")
			.WithPortBinding(AzuriteBlobPort, assignRandomHostPort: true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(AzuriteBlobPort))
			.Build();

	/// <summary>
	/// Configures the test host with the Azurite connection string.
	/// </summary>
	/// <param name="configurationBuilder">The configuration builder.</param>
	/// <param name="connectionString">The Azurite connection string.</param>
	protected virtual void ConfigureAzuriteConfiguration(IConfigurationBuilder configurationBuilder, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(configurationBuilder);
		ArgumentNullException.ThrowIfNull(connectionString);
	}

	/// <summary>
	/// Replaces the application's existing <typeparamref name="TDbContext"/> registration with the test container
	/// registration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	protected virtual void ReplaceDbContextRegistration(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.RemoveAll<DbContextOptions<TDbContext>>();
		_ = services.RemoveAll<TDbContext>();
	}

	/// <summary>
	/// Configures EF Core to use the SQL Server Testcontainer connection string.
	/// </summary>
	/// <param name="optionsBuilder">The EF Core options builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	protected virtual void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
	{
		ArgumentNullException.ThrowIfNull(optionsBuilder);
		ArgumentNullException.ThrowIfNull(connectionString);

		_ = optionsBuilder.UseSqlServer(connectionString, ConfigureSqlServerOptions);
	}

	/// <summary>
	/// Configures SQL Server-specific EF Core options.
	/// </summary>
	/// <param name="optionsBuilder">The SQL Server options builder.</param>
	protected virtual void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder optionsBuilder)
	{
		ArgumentNullException.ThrowIfNull(optionsBuilder);
	}

	/// <summary>
	/// Initializes the database after the test host has been created.
	/// </summary>
	/// <param name="serviceProvider">The application service provider.</param>
	protected virtual void InitializeDatabase(IServiceProvider serviceProvider)
	{
		ArgumentNullException.ThrowIfNull(serviceProvider);

		using IServiceScope scope = serviceProvider.CreateScope();
		TDbContext dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

		dbContext.Database.Migrate();
	}
}
