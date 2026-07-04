using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbrella.Testing.Xunit.Logging;

namespace Umbrella.Testing.Xunit.Test;

public sealed class XUnitTestOutputHelperLoggerTests
{
	[Fact]
	public void AddXUnitTestOutputHelperLoggingRegistersProvider()
	{
		var services = new ServiceCollection();

		_ = services.AddLogging(logging => logging.AddXUnitTestOutputHelperLogging());

		using ServiceProvider serviceProvider = services.BuildServiceProvider();

		IEnumerable<ILoggerProvider> providers = serviceProvider.GetServices<ILoggerProvider>();

		Assert.Contains(providers, provider => provider is XUnitTestOutputHelperLoggerProvider);
	}

	[Fact]
	public void LoggerWritesMessageAndExceptionToCurrentTestOutput()
	{
		using var provider = new XUnitTestOutputHelperLoggerProvider();
		ILogger logger = provider.CreateLogger("Smoke");

		logger.LogInformation("Hello from Umbrella.Testing.Xunit");
		logger.LogError(new InvalidOperationException("Smoke exception"), "Something happened");
	}
}