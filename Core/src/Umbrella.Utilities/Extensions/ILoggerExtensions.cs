
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Logging;

/// <summary>
/// A set of extension methods for <see cref="ILogger"/> instances which provide more comprehensive logging functionality than the built-in <see cref="ILogger.Log"/>
/// method and the Log* extension methods provided by Microsoft.
/// </summary>
public static class ILoggerExtensions
{
	private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _typePropertyInfoDictionary = new();

	/// <summary>
	/// Writes a <see cref="LogLevel.Debug"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	public static void WriteDebug(this ILogger log, object? state = null, string? message = null, in EventId eventId = default, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		=> LogDetails(log, LogLevel.Debug, null, state, message, in eventId, methodName, filePath, lineNumber, true);

	/// <summary>
	/// Writes a <see cref="LogLevel.Debug"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	public static void WriteTrace(this ILogger log, object? state = null, string? message = null, in EventId eventId = default, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		=> LogDetails(log, LogLevel.Trace, null, state, message, in eventId, methodName, filePath, lineNumber, true);

	/// <summary>
	/// Writes a <see cref="LogLevel.Information"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	public static void WriteInformation(this ILogger log, object? state = null, string? message = null, in EventId eventId = default, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		=> LogDetails(log, LogLevel.Information, null, state, message, in eventId, methodName, filePath, lineNumber, true);

	/// <summary>
	/// Writes a <see cref="LogLevel.Warning"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="exc">The exception.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="returnValue">
	/// The return value of this method will be this value.
	/// This is primarily to allowed these log methods to be called as a side-effect of exception filters.
	/// This return value can then control whether or not the associated catch block is entered.
	/// </param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	/// <param name="ignoreCancellationExceptions">Specifies whether <see cref="TaskCanceledException" /> and <see cref="OperationCanceledException" /> exceptions should be ignored.</param>
	/// <returns>The specified <paramref name="returnValue"/>.</returns>
	/// <remarks>
	/// When using this method with a try...catch block, this method should be called as side-effect of exception filters.
	/// </remarks>
	public static bool WriteWarning(this ILogger log, Exception? exc = null, object? state = null, string? message = null, in EventId eventId = default, bool returnValue = true, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, bool ignoreCancellationExceptions = true)
		=> LogDetails(log, LogLevel.Warning, exc, state, message, in eventId, methodName, filePath, lineNumber, ignoreCancellationExceptions) ?? returnValue;

	/// <summary>
	/// Writes a <see cref="LogLevel.Error"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="exc">The exception.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="returnValue">
	/// The return value of this method will be this value.
	/// This is primarily to allowed these log methods to be called as a side-effect of exception filters.
	/// This return value can then control whether or not the associated catch block is entered.
	/// </param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	/// <param name="ignoreCancellationExceptions">Specifies whether <see cref="TaskCanceledException" /> and <see cref="OperationCanceledException" /> exceptions should be ignored.</param>
	/// <returns>The specified <paramref name="returnValue"/>.</returns>
	/// <remarks>
	/// When using this method with a try...catch block, this method should be called as side-effect of exception filters.
	/// </remarks>
	public static bool WriteError(this ILogger log, Exception? exc = null, object? state = null, string? message = null, in EventId eventId = default, bool returnValue = true, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, bool ignoreCancellationExceptions = true)
		=> LogDetails(log, LogLevel.Error, exc, state, message, in eventId, methodName, filePath, lineNumber, ignoreCancellationExceptions) ?? returnValue;

	/// <summary>
	/// Writes a <see cref="LogLevel.Critical"/> message to the specified <paramref name="log"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="exc">The exception.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="returnValue">
	/// The return value of this method will be this value.
	/// This is primarily to allowed these log methods to be called as a side-effect of exception filters.
	/// This return value can then control whether or not the associated catch block is entered.
	/// </param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	/// <param name="ignoreCancellationExceptions">Specifies whether <see cref="TaskCanceledException" /> and <see cref="OperationCanceledException" /> exceptions should be ignored.</param>
	/// <returns>The specified <paramref name="returnValue"/>.</returns>
	/// <remarks>
	/// When using this method with a try...catch block, this method should be called as side-effect of exception filters.
	/// </remarks>
	public static bool WriteCritical(this ILogger log, Exception? exc = null, object? state = null, string? message = null, in EventId eventId = default, bool returnValue = true, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, bool ignoreCancellationExceptions = true)
		=> LogDetails(log, LogLevel.Critical, exc, state, message, in eventId, methodName, filePath, lineNumber, ignoreCancellationExceptions) ?? returnValue;

	/// <summary>
	/// Writes a message to the specified <paramref name="log"/> with the specified <paramref name="level"/>.
	/// </summary>
	/// <param name="log">The log.</param>
	/// <param name="level">The log level.</param>
	/// <param name="exc">The exception.</param>
	/// <param name="state">The state.</param>
	/// <param name="message">The message.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="returnValue">
	/// The return value of this method will be this value.
	/// This is primarily to allowed these log methods to be called as a side-effect of exception filters.
	/// This return value can then control whether or not the associated catch block is entered.
	/// </param>
	/// <param name="methodName">Name of the method.</param>
	/// <param name="filePath">The file path.</param>
	/// <param name="lineNumber">The line number.</param>
	/// <param name="ignoreCancellationExceptions">Specifies whether <see cref="TaskCanceledException" /> and <see cref="OperationCanceledException" /> exceptions should be ignored.</param>
	/// <returns>The specified <paramref name="returnValue"/>.</returns>
	/// <remarks>
	/// When using this method with a try...catch block, this method should be called as side-effect of exception filters.
	/// </remarks>
	public static bool Write(this ILogger log, LogLevel level, Exception? exc = null, object? state = null, string? message = null, in EventId eventId = default, bool returnValue = true, [CallerMemberName] string methodName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0, bool ignoreCancellationExceptions = true)
		=> LogDetails(log, level, exc, state, message, in eventId, methodName, filePath, lineNumber, ignoreCancellationExceptions) ?? returnValue;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Logging is dynamic here so creating and caching delegates would be cumbersome.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging is dynamic here so creating and caching these expressions would be cumbersome.")]
	private static bool? LogDetails(ILogger log, LogLevel level, Exception? exc, object? state, string? message, in EventId eventId, string methodName, string filePath, int lineNumber, bool ignoreCancellationExceptions)
	{
		if (!log.IsEnabled(level))
			return null;

		var messageBuilder = new StringBuilder(methodName);

		var stateDictionary = new List<KeyValuePair<string, string>>
		{
			new(nameof(methodName), methodName),
			new(nameof(filePath), filePath),
			new(nameof(lineNumber), lineNumber.ToString(CultureInfo.InvariantCulture))
		};

		if (state is not null)
		{
			PropertyInfo[] propertyInfos = _typePropertyInfoDictionary.GetOrAdd(state.GetType(), x => x.GetProperties());

			foreach (var pi in propertyInfos)
			{
				stateDictionary.Add(new KeyValuePair<string, string>(pi.Name, Convert.ToString(pi.GetValue(state) ?? string.Empty, CultureInfo.InvariantCulture) ?? string.Empty));
			}
		}

		if (level >= LogLevel.Error)
			_ = messageBuilder.Append(" failed");

		if (!string.IsNullOrEmpty(message))
			_ = messageBuilder.Append(" - " + message);

#if NET6_0_OR_GREATER
		_ = messageBuilder.Append(CultureInfo.InvariantCulture, $" on Line: {lineNumber}, Path: {filePath}");
#else
		_ = messageBuilder.Append($" on Line: {lineNumber}, Path: {filePath}");
#endif

		stateDictionary.Insert(0, new("LoggerMessage", messageBuilder.ToString()));

		string logTemplate = string.Join(", ", stateDictionary.Select(x => $"{x.Key}: {{{x.Key}}}"));
		object[] logArgs = stateDictionary.Select(x => x.Value).ToArray();

		log.Log(level, eventId, exc, logTemplate, logArgs);

		return exc is TaskCanceledException or OperationCanceledException && ignoreCancellationExceptions ? false : null;
	}
}