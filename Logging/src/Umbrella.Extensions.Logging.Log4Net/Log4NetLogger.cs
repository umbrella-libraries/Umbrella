
using System.Globalization;
using System.Text;
using log4net;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities;

namespace Umbrella.Extensions.Logging.Log4Net;

/// <summary>
/// A log4net specific implementation of the Microsoft <see cref="ILogger"/>.
/// </summary>
public class Log4NetLogger : ILogger
{
	#region Private Members
	private readonly ILog _logger;
	#endregion

	#region Constructors
	/// <summary>
	/// Creates a new instance of <see cref="Log4NetLogger"/>.
	/// </summary>
	/// <param name="repositoryName">The repo name.</param>
	/// <param name="loggerName">The logger name.</param>
	public Log4NetLogger(string repositoryName, string loggerName)
	{
		_logger = LogManager.GetLogger(repositoryName, loggerName);
	}
	#endregion

	#region ILogger Members
	/// <inheritdoc />
#if NET8_0_OR_GREATER
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyDisposable.Instance;
#else
	public IDisposable BeginScope<TState>(TState state) => EmptyDisposable.Instance;
#endif

	/// <inheritdoc />
	public bool IsEnabled(LogLevel logLevel) => logLevel switch
	{
		LogLevel.Trace => _logger.IsDebugEnabled,
		LogLevel.Debug => _logger.IsDebugEnabled,
		LogLevel.Information => _logger.IsInfoEnabled,
		LogLevel.Warning => _logger.IsWarnEnabled,
		LogLevel.Error => _logger.IsErrorEnabled,
		LogLevel.Critical => _logger.IsFatalEnabled,
		LogLevel.None => false,
		_ => throw new ArgumentException($"Unknown log level {logLevel}.", nameof(logLevel)),
	};

	/// <inheritdoc />
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		=> LogInner(logLevel, eventId, state, exception, formatter);

	private void LogInner<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter, int recursiveLevel = 0)
	{
		// Checking how many levels deep we are here to prevent a possible StackOverflowException.
		if (recursiveLevel > 5)
		{
			if (IsEnabled(LogLevel.Warning))
				_logger.Warn("Whilst logging an exception the recursion level exceeded 5. This indicates a problem with the way the AggregateException has been constructed.");

			return;
		}

		if (!IsEnabled(logLevel))
			return;

		if (formatter is null)
			throw new InvalidOperationException();

		// If the eventId is 0 check if the Name has a value as we have hijacked this to allow for recursive calls
		// to this method to use the same id for correlating messages.
		string messageId = eventId.Id is 0
			? string.IsNullOrWhiteSpace(eventId.Name) ? "Correlation Id: " + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) : eventId.Name!
			: eventId.Id.ToString(CultureInfo.InvariantCulture);

#if NET6_0_OR_GREATER
		var messageBuider = new StringBuilder()
			.AppendLine(CultureInfo.InvariantCulture, $"{messageId}")
			.AppendLine(CultureInfo.InvariantCulture, $"{DateTime.UtcNow} UTC")
			.Append(formatter(state, exception));
#else
		var messageBuider = new StringBuilder()
			.AppendLine(messageId)
			.AppendLine($"{DateTime.UtcNow} UTC")
			.Append(formatter(state, exception));
#endif

		if (state is IEnumerable<KeyValuePair<string, string>> stateDictionary)
		{
			var entries = stateDictionary.ToArray();

			_ = messageBuider.AppendLine();
			_ = messageBuider.AppendLine();
			_ = messageBuider.AppendLine("--------------- Begin State ---------------");

			if (entries.Length > 0)
			{
				foreach (var entry in stateDictionary)
				{
#if NET6_0_OR_GREATER
					_ = messageBuider.AppendLine(CultureInfo.InvariantCulture, $"{entry.Key}: {entry.Value}");
#else
					_ = messageBuider.AppendLine($"{entry.Key}: {entry.Value}");
#endif
				}
			}

			_ = messageBuider.AppendLine("--------------- End State ---------------");
		}

		string message = messageBuider.ToString();

		switch (logLevel)
		{
			case LogLevel.Trace:
			case LogLevel.Debug:
				_logger.Debug(message, exception);
				break;
			case LogLevel.Information:
				_logger.Info(message, exception);
				break;
			case LogLevel.Warning:
				_logger.Warn(message, exception);
				break;
			case LogLevel.Error:
				_logger.Error(message, exception);
				break;
			case LogLevel.Critical:
				_logger.Fatal(message, exception);
				break;
			default:
				_logger.Warn($"Encountered unknown log level {logLevel}, writing out as Info.");
				_logger.Info(message, exception);
				break;
		}

		// Log4Net doesn't seem to log AggregateExceptions properly so handling them manually here
		if (exception is not null)
		{
			AggregateException? aggregate = null;

			if (exception is not AggregateException)
				aggregate = exception.InnerException as AggregateException;

			if (aggregate?.InnerExceptions?.Count > 0)
			{
				foreach (Exception innerException in aggregate.InnerExceptions)
				{
					LogInner(logLevel, new EventId(0, messageId), state, exception, formatter, ++recursiveLevel);

					// The call returned here so we can reduce by 1 to indicate we have moved back up.
					recursiveLevel--;
				}
			}
		}
	}
	#endregion
}