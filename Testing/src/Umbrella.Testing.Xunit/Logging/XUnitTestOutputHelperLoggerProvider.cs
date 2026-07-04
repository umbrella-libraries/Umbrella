using Microsoft.Extensions.Logging;

namespace Umbrella.Testing.Xunit.Logging;

/// <summary>
/// Provides <see cref="ILogger"/> instances that write log messages to the current xUnit v3 test output helper.
/// </summary>
public sealed class XUnitTestOutputHelperLoggerProvider : ILoggerProvider
{
	/// <inheritdoc />
	public ILogger CreateLogger(string categoryName) => new XUnitTestOutputHelperLogger(categoryName);

	/// <inheritdoc />
	public void Dispose()
	{
	}
}
