


using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Umbrella.AppFramework.Exceptions;
using Umbrella.AppFramework.Services.Abstractions;
using Umbrella.AppFramework.Services.Enumerations;
using Umbrella.AppFramework.Services.Messages;

namespace Umbrella.AppFramework.Services;

/// <summary>
/// A utility used to show and hide an application loading screen.
/// </summary>
public class LoadingScreenService : ILoadingScreenService, IDisposable
{
	private readonly ILogger _logger;
	private CancellationTokenSource? _cancellationTokenSource;
	private bool _disposedValue;

	/// <inheritdoc />
	public event Action<LoadingScreenState> OnStateChanged
	{
		add
		{
			Guard.IsNotNull(value?.Target);
			WeakReferenceMessenger.Default.TryRegister<LoadingScreenStateChangedMessage>(value.Target, (_, args) => value(args.Value));
		}
		remove
		{
			Guard.IsNotNull(value?.Target);
			WeakReferenceMessenger.Default.Unregister<LoadingScreenStateChangedMessage>(value.Target);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LoadingScreenService"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	public LoadingScreenService(ILogger<LoadingScreenService> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public void Show(int delayMilliseconds = 500)
	{
		try
		{
			if (_cancellationTokenSource is not null)
				return;

			_cancellationTokenSource = new CancellationTokenSource();

			_ = WeakReferenceMessenger.Default.Send(new LoadingScreenStateChangedMessage(LoadingScreenState.Requested));

			var token = _cancellationTokenSource.Token;

			_ = Task.Delay(delayMilliseconds, token).ContinueWith(x =>
			{
				if (!token.IsCancellationRequested)
					_ = WeakReferenceMessenger.Default.Send(new LoadingScreenStateChangedMessage(LoadingScreenState.Visible));
			},
			TaskScheduler.Default);
		}
		catch (Exception exc) when (_logger.WriteError(exc, new { delayMilliseconds }))
		{
			throw new UmbrellaAppFrameworkException("There has been a problem showing the loading screen.", exc);
		}
	}

	/// <inheritdoc />
	public void Hide()
	{
		try
		{
			_cancellationTokenSource?.Cancel();
			_ = WeakReferenceMessenger.Default.Send(new LoadingScreenStateChangedMessage(LoadingScreenState.Hidden));
		}
		catch (Exception exc)
		{
			throw new UmbrellaAppFrameworkException("There has been a problem hiding the loading screen.", exc);
		}
		finally
		{
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
		}
	}

	/// <summary>
	/// Releases unmanaged and - optionally - managed resources.
	/// </summary>
	/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_cancellationTokenSource?.Dispose();
				_cancellationTokenSource = null;
			}

			_disposedValue = true;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}