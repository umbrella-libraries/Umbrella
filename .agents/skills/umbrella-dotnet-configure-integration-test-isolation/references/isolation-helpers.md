# Isolation helper sketches

Create these only when the repo has no helper doing the same job. Put them in the shared testing library (for example `IndyRecords.Web.Server.Testing`) so every test project uses the same ones. Adjust names and namespaces to the repo.

## Error log recorder

```csharp
using System.Collections.Concurrent;

/// <summary>
/// Records the errors a host logs, so a test can check nothing failed behind a successful response.
/// </summary>
public sealed class ErrorLogRecorder : ILoggerProvider
{
	private readonly ConcurrentQueue<string> _errors = new();

	public IReadOnlyCollection<string> Logged => [.. _errors];

	public void Clear() => _errors.Clear();

	public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _errors);

	public void Dispose()
	{
	}

	private sealed class Logger(string categoryName, ConcurrentQueue<string> errors) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
			=> null;

		public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(logLevel))
				errors.Enqueue($"{categoryName}: {(exception is null ? formatter(state, exception) : $"{exception.GetType().Name}: {exception.Message}")}");
		}
	}
}
```

Each entry carries the logger category, so a test that records a known failure can assert on it, for example `Assert.Contains(_factory.Errors.Logged, x => x.Contains("SearchService", StringComparison.Ordinal))`.

## Docker availability

```csharp
public static class DockerAvailability
{
	private static readonly Lazy<bool> _isEngineReachable = new(Probe);

	public const string SkipReason = "A Docker engine running Linux containers is not available, so the container-backed tests cannot start.";

	// Azure Pipelines sets TF_BUILD=True. Add the CI system's own variable if it differs.
	public static bool IsRequired { get; } = string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "True", StringComparison.OrdinalIgnoreCase);

	public static bool IsAvailable => IsRequired || _isEngineReachable.Value;

	public static void EnsureEngineReachable()
	{
		if (!_isEngineReachable.Value)
			throw new InvalidOperationException($"{SkipReason} The tests are not skipped in CI, so the agent must provide one.");
	}

	private static bool Probe()
	{
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			string? serverOs = TryGetServerOs(TimeSpan.FromSeconds(30));
			if (serverOs is not null)
				return string.Equals(serverOs, "linux", StringComparison.OrdinalIgnoreCase);
		}

		return false;
	}

	private static string? TryGetServerOs(TimeSpan timeout)
	{
		try
		{
			using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Os}}")
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			});

			if (process is null)
				return null;

			if (!process.WaitForExit(timeout))
			{
				process.Kill(entireProcessTree: true);
				return null;
			}

			string serverOs = process.StandardOutput.ReadToEnd().Trim();
			return process.ExitCode is 0 && serverOs.Length > 0 ? serverOs : null;
		}
		catch (Exception exc) when (exc is Win32Exception or InvalidOperationException)
		{
			return null; // The Docker CLI is not installed or could not be started.
		}
	}
}
```

The factory uses it in `InitializeAsync`:

```csharp
protected virtual bool ShouldStart => DockerAvailability.IsAvailable;

public async ValueTask InitializeAsync()
{
	if (!ShouldStart)
		return;

	DockerAvailability.EnsureEngineReachable();
	// Start containers, build the host, seed.
}
```

## Docker-gated attributes

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class IndyRecordsFactAttribute : FactAttribute
{
	public IndyRecordsFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
		: base(sourceFilePath, sourceLineNumber)
	{
		if (!DockerAvailability.IsAvailable)
			Skip = DockerAvailability.SkipReason;
	}
}
```

Add the matching `IndyRecordsTheoryAttribute : TheoryAttribute` with the same body.

## Stop-once hosted service decorator

```csharp
/// <summary>
/// Wraps a hosted service so it is stopped only once, however many times the host is stopped.
/// </summary>
/// <remarks>
/// When Program calls Run(), WebApplicationFactory stops the host and Run() stops it again concurrently. Each stop
/// calls every hosted service's StopAsync, and a shutdown that is not idempotent then throws during disposal.
/// </remarks>
internal sealed class SingleStopHostedService : IHostedService
{
	private readonly IHostedService _inner;
	private readonly object _stopLock = new();
	private Task? _stopTask;

	private SingleStopHostedService(IHostedService inner) => _inner = inner;

	public Task StartAsync(CancellationToken cancellationToken) => _inner.StartAsync(cancellationToken);

	// A later caller waits for the first stop and shares its outcome.
	public Task StopAsync(CancellationToken cancellationToken)
	{
		lock (_stopLock)
		{
			return _stopTask ??= _inner.StopAsync(cancellationToken);
		}
	}

	public static void Decorate(IServiceCollection services, string implementationTypeName)
	{
		ServiceDescriptor descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType?.FullName == implementationTypeName)
			?? throw new InvalidOperationException($"The hosted service '{implementationTypeName}' is not registered, so it cannot be protected from being stopped twice.");

		Type implementationType = descriptor.ImplementationType!;

		services[services.IndexOf(descriptor)] = ServiceDescriptor.Singleton<IHostedService>(
			serviceProvider => new SingleStopHostedService((IHostedService)ActivatorUtilities.CreateInstance(serviceProvider, implementationType)));
	}
}
```

Call `Decorate` after `base.ConfigureServices(services)`, so the app's registration already exists. If the hosted service is registered with a factory delegate rather than a type, find the descriptor another way (for example by resolving the services once and matching the instance type) and wrap its `ImplementationFactory` instead of calling `ActivatorUtilities`. Keep the "not registered" exception either way.
