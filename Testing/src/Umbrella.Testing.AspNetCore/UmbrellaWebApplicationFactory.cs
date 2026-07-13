using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
		string? applicationEnvironmentName = GetApplicationEnvironmentName();

		if (!string.IsNullOrWhiteSpace(environmentName))
			_ = builder.UseEnvironment(environmentName);

		_ = builder.ConfigureLogging(ConfigureLogging);

		ConfigureWebHostBuilder(builder);

		_ = builder.ConfigureServices(services =>
		{
			ConfigureApplicationEnvironment(services, applicationEnvironmentName);
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
	/// Gets an optional environment name to expose through <see cref="IHostEnvironment"/> and
	/// <see cref="IWebHostEnvironment"/> after application startup has been configured.
	/// </summary>
	/// <remarks>
	/// Override this when startup must use one environment (for example, <c>Development</c> to avoid production-only
	/// cloud dependencies) while application services such as controller exception filters must observe a different,
	/// non-development environment. Returning <see langword="null"/> preserves the host environment configured by
	/// <see cref="GetEnvironmentName"/>.
	/// </remarks>
	/// <returns>The application service environment name, or <see langword="null"/> to preserve the host environment.</returns>
	protected virtual string? GetApplicationEnvironmentName() => null;

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

	private static void ConfigureApplicationEnvironment(IServiceCollection services, string? applicationEnvironmentName)
	{
		if (string.IsNullOrWhiteSpace(applicationEnvironmentName))
			return;

		ServiceDescriptor? descriptor = services.LastOrDefault(x => x.ServiceType == typeof(IWebHostEnvironment));

		if (descriptor?.ImplementationInstance is not IWebHostEnvironment webHostEnvironment)
		{
			throw new InvalidOperationException(
				$"Cannot expose application environment '{applicationEnvironmentName}' because the registered {nameof(IWebHostEnvironment)} is not an implementation instance.");
		}

		if (string.Equals(webHostEnvironment.EnvironmentName, applicationEnvironmentName, StringComparison.Ordinal))
			return;

		var applicationEnvironment = new UmbrellaTestWebHostEnvironment(webHostEnvironment, applicationEnvironmentName);

		_ = services.RemoveAll<IWebHostEnvironment>();
		_ = services.RemoveAll<IHostEnvironment>();
		_ = services.AddSingleton<IWebHostEnvironment>(applicationEnvironment);
		_ = services.AddSingleton<IHostEnvironment>(applicationEnvironment);
	}
}
