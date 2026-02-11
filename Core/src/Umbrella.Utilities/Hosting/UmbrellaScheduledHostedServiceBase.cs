


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using Umbrella.Utilities.Dating.Abstractions;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Threading.Abstractions;

namespace Umbrella.Utilities.Hosting;

/// <summary>
/// A base class containing core logic for hosted services.
/// </summary>
/// <seealso cref="IHostedService" />
/// <seealso cref="IDisposable" />
public abstract class UmbrellaScheduledHostedServiceBase : IHostedService, IDisposable
{
	private CancellationTokenSource? _cancellationTokenSource;
	private bool _disposedValue;

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger Logger { get; }

	/// <summary>
	/// Gets the service scope factory.
	/// </summary>
	protected IServiceScopeFactory ServiceScopeFactory { get; }

	/// <summary>
	/// Gets the synchronization manager.
	/// </summary>
	protected ISynchronizationManager SynchronizationManager { get; }

	/// <summary>
	/// Gets the date time provider.
	/// </summary>
	protected IDateTimeProvider DateTimeProvider { get; }

	/// <summary>
	/// Gets the next run date and time.
	/// </summary>
	protected DateTime NextRun { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="UmbrellaScheduledHostedServiceBase"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="serviceScopeFactory">The service scope factory.</param>
	/// <param name="synchronizationManager">The synchronization manager.</param>
	/// <param name="dateTimeProvider">The date time provider.</param>
	protected UmbrellaScheduledHostedServiceBase(
		ILogger logger,
		IServiceScopeFactory serviceScopeFactory,
		ISynchronizationManager synchronizationManager,
		IDateTimeProvider dateTimeProvider)
	{
		Logger = logger;
		ServiceScopeFactory = serviceScopeFactory;
		SynchronizationManager = synchronizationManager;
		DateTimeProvider = dateTimeProvider;
	}

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		try
		{
			_cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			string scheduleString = await GetCrontabScheduleUtcStringAsync(cancellationToken);
			var crontabSchedule = CrontabSchedule.Parse(scheduleString, new CrontabSchedule.ParseOptions { IncludingSeconds = true });
			NextRun = crontabSchedule.GetNextOccurrence(DateTimeProvider.UtcNow);

			Logger.WriteInformation(new { NextRun }, "First run is scheduled.");

			_ = Task.Run(async () =>
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					await Task.Delay(Math.Max(0, (int)NextRun.Subtract(DateTimeProvider.UtcNow).TotalMilliseconds));

					Logger.WriteInformation(new { Date = DateTimeProvider.UtcNow }, "Running.");

					try
					{
						using var serviceScope = ServiceScopeFactory.CreateScope();

						await ExecuteInternalAsync(serviceScope, cancellationToken);
					}
					catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
					{
						// Mask the exception to allow the job to retry on the next run
					}

					NextRun = crontabSchedule.GetNextOccurrence(DateTimeProvider.UtcNow);

					Logger.WriteInformation(new { NextRun }, "Next run is scheduled.");
				}
			},
			cancellationToken);
		}
		catch (Exception exc) when (exc is not OperationCanceledException && Logger.WriteError(exc))
		{
			throw new UmbrellaException("There has been a problem with this service during startup.", exc);
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

#if NET8_0_OR_GREATER
		if(_cancellationTokenSource is not null)
			await _cancellationTokenSource.CancelAsync();
#else
		_cancellationTokenSource?.Cancel();
		await Task.Yield();
#endif
	}

	/// <summary>
	/// Executes the core logic for this service.
	/// </summary>
	/// <param name="serviceScope">The service scope from which services can be resolved internally.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> used to await execution.</returns>
	protected abstract Task ExecuteAsync(IServiceScope serviceScope, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the crontab schedule string.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The schedule string.</returns>
	protected abstract Task<string> GetCrontabScheduleUtcStringAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Executes the internal logic for this service.
	/// </summary>
	/// <param name="serviceScope">The service scope.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>An awaitable <see cref="Task"/> used to await execution.</returns>
	private protected virtual Task ExecuteInternalAsync(IServiceScope serviceScope, CancellationToken cancellationToken) => ExecuteAsync(serviceScope, cancellationToken);

	/// <inheritdoc />
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_cancellationTokenSource?.Dispose();
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}