using Microsoft.Extensions.Logging;
using Microsoft.Owin;

namespace Umbrella.Legacy.WebUtilities.Middleware;

/// <summary>
/// Middleware to log information about the current request for debugging purposes.
/// If this middleware is included in the OWIN pipeline, it will output debug log messages if that log level
/// is enabled for the application.
/// </summary>
/// <seealso cref="OwinMiddleware" />
public class DebugRequestMiddleware : OwinMiddleware
{
	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DebugRequestMiddleware"/> class.
	/// </summary>
	/// <param name="next">The next <see cref="OwinMiddleware"/> in the pipeline.</param>
	/// <param name="logger">The logger.</param>
	public DebugRequestMiddleware(OwinMiddleware next,
		ILogger<DebugRequestMiddleware> logger)
		: base(next)
	{
		Logger = logger;
	}

	/// <summary>
	/// Process an individual request.
	/// </summary>
	/// <param name="context">The current Owin Context.</param>
	/// <returns>A <see cref="Task"/> which can be awaited whilst the middleware is invoked.</returns>
	public override Task Invoke(IOwinContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.Request.CallCancelled.ThrowIfCancellationRequested();

		try
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				var requestData = new
				{
					PathBase = context.Request.PathBase.Value,
					Path = context.Request.Path.Value,
					context.Request.Headers
				};

				Logger.WriteDebug(requestData, "Debug request state");
			}

			return Next.Invoke(context);
		}
		catch (Exception exc) when (Logger.WriteError(exc))
		{
			throw;
		}
	}
}
