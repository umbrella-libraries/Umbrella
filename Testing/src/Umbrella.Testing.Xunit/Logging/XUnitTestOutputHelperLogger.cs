using Microsoft.Extensions.Logging;
using Xunit;

namespace Umbrella.Testing.Xunit.Logging;

/// <summary>
/// Writes <see cref="ILogger"/> output to <see cref="TestContext.Current"/>.
/// </summary>
public sealed class XUnitTestOutputHelperLogger : ILogger
{
	private readonly string _categoryName;

	/// <summary>
	/// Initializes a new instance of the <see cref="XUnitTestOutputHelperLogger"/> class.
	/// </summary>
	/// <param name="categoryName">The logger category name.</param>
	public XUnitTestOutputHelperLogger(string categoryName)
	{
		ArgumentNullException.ThrowIfNull(categoryName);

		_categoryName = categoryName;
	}

	/// <inheritdoc />
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

	/// <inheritdoc />
	public bool IsEnabled(LogLevel logLevel) => true;

	/// <inheritdoc />
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		ArgumentNullException.ThrowIfNull(formatter);

		string message = formatter(state, exception);

		TestContext.Current.TestOutputHelper?.WriteLine("[{0}] {1}: {2}", logLevel, _categoryName, message);

		if (exception is not null)
			TestContext.Current.TestOutputHelper?.WriteLine(exception.ToString());
	}

	private sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new();

		public void Dispose()
		{
		}
	}
}
