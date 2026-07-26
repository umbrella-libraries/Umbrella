using System.Globalization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;

namespace Umbrella.Extensions.Logging.ApplicationInsights;

/// <summary>
/// /// A ApplicationInsights specific implementation of the Microsoft <see cref="ILoggerProvider"/>.
/// </summary>
public class ApplicationInsightsLogger : ILogger
{
	private readonly string _categoryName;
	private readonly TelemetryClient _telemetryClient;
	private readonly LogLevel _minLevel;
	private readonly bool _trackExceptionsAsExceptionTelemetry;
	private readonly bool _includeEventId;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApplicationInsightsLogger"/> class.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <param name="telemetryClient">The telemetry client.</param>
	/// <param name="minLevel">The minimum level.</param>
	/// <param name="trackExceptionsAsExceptionTelemetry">if set to <c>true</c> tracks exceptions as exception telemetry.</param>
	/// <param name="includeEventId">if set to <c>true</c> includes the event id on logging output..</param>
	public ApplicationInsightsLogger(string name, TelemetryClient telemetryClient, LogLevel minLevel, bool trackExceptionsAsExceptionTelemetry, bool includeEventId)
	{
		_categoryName = name;
		_telemetryClient = telemetryClient;
		_minLevel = minLevel;
		_trackExceptionsAsExceptionTelemetry = trackExceptionsAsExceptionTelemetry;
		_includeEventId = includeEventId;
	}

	/// <inheritdoc />
#if NET8_0_OR_GREATER
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyDisposable.Instance;
#else
	public IDisposable BeginScope<TState>(TState state) => EmptyDisposable.Instance;
#endif

	/// <inheritdoc />
	public bool IsEnabled(LogLevel logLevel) => _telemetryClient is not null && logLevel >= _minLevel && _telemetryClient.IsEnabled();

	/// <inheritdoc />
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (IsEnabled(logLevel))
		{
#if NET8_0_OR_GREATER
			ArgumentNullException.ThrowIfNull(formatter);
#else
			if (formatter is null)
				throw new ArgumentNullException(nameof(formatter));
#endif

			var stateDictionary = state as IReadOnlyList<KeyValuePair<string, object>>;

			if (exception is null || !_trackExceptionsAsExceptionTelemetry)
			{
				var traceTelemetry = new TraceTelemetry(formatter(state, exception), GetSeverityLevel(logLevel));
				PopulateTelemetry(traceTelemetry, stateDictionary, eventId);
				_telemetryClient.TrackTrace(traceTelemetry);
			}
			else
			{
				var exceptionTelemetry = new ExceptionTelemetry(exception)
				{
					Message = formatter(state, exception),
					SeverityLevel = GetSeverityLevel(logLevel)
				};
				exceptionTelemetry.Properties["Exception"] = exception.ToString();
				PopulateTelemetry(exceptionTelemetry, stateDictionary, eventId);
				_telemetryClient.TrackException(exceptionTelemetry);
			}
		}
	}

	private void PopulateTelemetry(ITelemetry telemetry, IReadOnlyList<KeyValuePair<string, object>>? stateDictionary, in EventId eventId)
	{
		if (telemetry is ISupportProperties telemetryWithProperties)
		{
			IDictionary<string, string?> dict = telemetryWithProperties.Properties;
			dict["CategoryName"] = _categoryName;

			if (_includeEventId)
			{
				if (eventId.Id != 0)
				{
					dict["EventId"] = eventId.Id.ToString(CultureInfo.InvariantCulture);
				}

				if (!string.IsNullOrEmpty(eventId.Name))
				{
					dict["EventName"] = eventId.Name!;
				}
			}

			if (stateDictionary is not null)
			{
				foreach (KeyValuePair<string, object> item in stateDictionary)
				{
					dict[item.Key] = Convert.ToString(item.Value, CultureInfo.InvariantCulture);
				}
			}
		}
	}

	private static SeverityLevel GetSeverityLevel(LogLevel logLevel) => logLevel switch
	{
		LogLevel.Critical => SeverityLevel.Critical,
		LogLevel.Error => SeverityLevel.Error,
		LogLevel.Warning => SeverityLevel.Warning,
		LogLevel.Information => SeverityLevel.Information,
		_ => SeverityLevel.Verbose,
	};
}
