using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace Umbrella.Testing.AspNetCore;

/// <summary>
/// ASP.NET Core integration test factory backed by a SQL Server Testcontainer and an EF Core <see cref="DbContext"/>.
/// </summary>
/// <typeparam name="TProgram">The application entry point type.</typeparam>
/// <typeparam name="TDbContext">The EF Core database context type.</typeparam>
public abstract class UmbrellaSqlServerWebApplicationFactory<TProgram, TDbContext> : UmbrellaWebApplicationFactory<TProgram>, IAsyncLifetime
	where TProgram : class
	where TDbContext : DbContext
{
	private MsSqlContainer? _databaseContainer;

	/// <summary>
	/// Gets the SQL Server Testcontainer.
	/// </summary>
	protected MsSqlContainer DatabaseContainer => _databaseContainer ??= CreateMsSqlBuilder().Build();

	/// <summary>
	/// Gets the SQL Server connection string for the current Testcontainer.
	/// </summary>
	public string ConnectionString => DatabaseContainer.GetConnectionString();

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
	public virtual async ValueTask InitializeAsync() => await DatabaseContainer.StartAsync();

	/// <inheritdoc />
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();

		if (_databaseContainer is not null)
			await _databaseContainer.DisposeAsync();

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
