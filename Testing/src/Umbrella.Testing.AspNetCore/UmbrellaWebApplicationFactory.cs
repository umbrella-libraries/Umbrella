using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Umbrella.Testing.AspNetCore;

/// <summary>
/// Base <see cref="WebApplicationFactory{TEntryPoint}"/> for ASP.NET Core integration tests.
/// </summary>
/// <typeparam name="TProgram">The application entry point type.</typeparam>
public abstract class UmbrellaWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
	where TProgram : class
{
	/// <inheritdoc />
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		string environmentName = GetEnvironmentName();

		if (!string.IsNullOrWhiteSpace(environmentName))
			_ = builder.UseEnvironment(environmentName);

		_ = builder.ConfigureLogging(ConfigureLogging);

		ConfigureWebHostBuilder(builder);

		_ = builder.ConfigureServices(services =>
		{
			ConfigureServices(services);
			ConfigureAuthentication(services);
		});
	}

	/// <summary>
	/// Gets the environment name used by the test host.
	/// </summary>
	/// <returns>The environment name. The default value is <c>Development</c>.</returns>
	protected virtual string GetEnvironmentName() => "Development";

	/// <summary>
	/// Configures the underlying web host builder before services are configured.
	/// </summary>
	/// <param name="builder">The web host builder.</param>
	protected virtual void ConfigureWebHostBuilder(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
	}

	/// <summary>
	/// Configures logging for the test host.
	/// </summary>
	/// <param name="logging">The logging builder.</param>
	protected virtual void ConfigureLogging(ILoggingBuilder logging)
	{
		ArgumentNullException.ThrowIfNull(logging);

		_ = logging.ClearProviders();
		_ = logging.AddXUnitTestOutputHelperLogging();
		_ = logging.SetMinimumLevel(GetMinimumLogLevel());
	}

	/// <summary>
	/// Gets the minimum log level used by the test host.
	/// </summary>
	/// <returns>The minimum log level. The default value is <see cref="LogLevel.Warning"/>.</returns>
	protected virtual LogLevel GetMinimumLogLevel() => LogLevel.Warning;

	/// <summary>
	/// Configures project-specific test services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	protected virtual void ConfigureServices(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
	}

	/// <summary>
	/// Configures project-specific authentication for the test host.
	/// </summary>
	/// <param name="services">The service collection.</param>
	protected abstract void ConfigureAuthentication(IServiceCollection services);
}
